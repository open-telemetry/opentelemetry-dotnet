// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

public class Program
{
    private static readonly ActivitySource MyActivitySource = new("MyCompany.MyProduct.MyLibrary");
    private static readonly Meter MyMeter = new("MyCompany.MyProduct.MyLibrary");
    private static readonly Counter<int> MyCounter = MyMeter.CreateCounter<int>("execution.count");

    public static void Main()
    {
        // Note: OpenTelemetrySdk.Create was added in 1.10.0

        // Configure OpenTelemetry to send logs, metrics, and distributed traces
        // via OTLP.
        var openTelemetrySdk = OpenTelemetrySdk.Create(builder => builder
            .ConfigureResource(resource => resource.AddService(serviceName: "ConsoleApp"))
            .WithTracing(tracing => tracing.AddSource(MyActivitySource.Name))
            .WithMetrics(metrics => metrics.AddMeter(MyMeter.Name))
            .UseOtlpExporter());

        var logger = openTelemetrySdk.GetLoggerFactory().CreateLogger<Program>();

        logger.LogInformation("Application starting");

        using (var activity = MyActivitySource.StartActivity("SayHello"))
        {
            if (activity?.IsAllDataRequested == true)
            {
                activity.SetTag("foo", 1);
                activity.SetTag("bar", "Hello, World!");
                activity.SetTag("baz", new int[] { 1, 2, 3 });
                activity.SetStatus(ActivityStatusCode.Ok);
            }
        }

        MyCounter.Add(1);

        logger.LogInformation("Application stopping");

        // Dispose openTelemetrySdk before the application ends.
        // This will flush any remaining telemetry and shutdown pipelines.
        openTelemetrySdk.Dispose();
    }
}
