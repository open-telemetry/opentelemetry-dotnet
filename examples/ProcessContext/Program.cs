// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

// Publishes the SDK resource as the process context (OTEP 4719) so that the
// OpenTelemetry eBPF Profiler, running outside this process, attaches the same
// service.name and service.instance.id to the profiles it collects.
//
// The service name and instance id are set in code on purpose: an external
// reader cannot see them unless the process publishes them. Run with
//
//   OTEL_EXPERIMENTAL_PROCESS_CONTEXT_ENABLED=true dotnet run
//
// (Linux on x64 or arm64 only; elsewhere nothing is published) and inspect
// /proc/<pid>/maps for the OTEL_CTX mapping.
//
// Arguments: [run seconds, default 600] [seconds of work before the providers
// are built, default 0]. A delay shows late publication: an external reader
// which already knows the process has to notice the new mapping.
var seconds = args.Length > 0 && int.TryParse(args[0], System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 600;
var delaySeconds = args.Length > 1 && int.TryParse(args[1], System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
var stopAt = DateTime.UtcNow.AddSeconds(seconds);
var checksum = 0UL;

var publishAt = DateTime.UtcNow.AddSeconds(delaySeconds);
while (DateTime.UtcNow < publishAt)
{
    checksum += Burn(2_000_000);
}

using var activitySource = new ActivitySource(new ActivitySourceOptions("Examples.ProcessContext"));
using var meter = new Meter(new MeterOptions("Examples.ProcessContext"));
var iterations = meter.CreateCounter<long>("iterations");

var serviceInstanceId = Guid.NewGuid().ToString();

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(r => r.AddService("process-context-example", serviceVersion: "1.0.0", serviceInstanceId: serviceInstanceId))
    .AddSource(activitySource.Name)
    .AddProcessContext()
    .Build();

using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .ConfigureResource(r => r.AddService("process-context-example", serviceVersion: "1.0.0", serviceInstanceId: serviceInstanceId))
    .AddMeter(meter.Name)
    .AddProcessContext()
    .Build();

Console.WriteLine($"{DateTime.UtcNow:O} pid={Environment.ProcessId} service.instance.id={serviceInstanceId} supported={ProcessContextPublisher.IsSupported}");

// Keep a CPU busy so that the profiler has something to sample.
while (DateTime.UtcNow < stopAt)
{
    using var activity = activitySource.StartActivity("work");
    checksum += Burn(2_000_000);
    iterations.Add(1);
}

Console.WriteLine($"done checksum={checksum}");

static ulong Burn(int rounds)
{
    var x = 0x9E3779B97F4A7C15UL;
    for (var i = 0; i < rounds; i++)
    {
        x ^= x << 13;
        x ^= x >> 7;
        x ^= x << 17;
    }

    return x;
}
