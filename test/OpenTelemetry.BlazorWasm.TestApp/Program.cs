// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using OpenTelemetry;
using OpenTelemetry.BlazorWasm.TestApp;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");

Environment.SetEnvironmentVariable("OTEL_BSP_SCHEDULE_DELAY", "1000");
Environment.SetEnvironmentVariable("OTEL_BLRP_SCHEDULE_DELAY", "1000");
Environment.SetEnvironmentVariable("OTEL_METRIC_EXPORT_INTERVAL", "1000");

// The OTLP receiver is served from the same origin as the application, so the
// export endpoints are resolved relative to the host base address. This keeps
// the export a genuine cross-process HTTP/protobuf call while avoiding CORS.
var baseAddress = new Uri(builder.HostEnvironment.BaseAddress);

var resourceBuilder = ResourceBuilder.CreateDefault()
    .AddService(InstrumentationSource.ServiceName);

using var instrumentation = new InstrumentationSource();
builder.Services.AddSingleton(instrumentation);

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = baseAddress });

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddSource(InstrumentationSource.ActivitySourceName)
    .AddSource("System.Net.Http")
    .SetSampler(new AlwaysOnSampler())
    .AddOtlpExporter((options) =>
    {
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        options.Endpoint = new Uri(baseAddress, "v1/traces");
    })
    .Build();

builder.Services.AddSingleton(tracerProvider);

var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(resourceBuilder)
    .AddMeter(InstrumentationSource.MeterName)
    .AddMeter("System.Net.Http")
    .AddOtlpExporter((options) =>
    {
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        options.Endpoint = new Uri(baseAddress, "v1/metrics");
    })
    .Build();

builder.Services.AddSingleton(meterProvider);

builder.Logging.AddOpenTelemetry((options) =>
{
    options.SetResourceBuilder(resourceBuilder);
    options.IncludeFormattedMessage = true;

    options.AddOtlpExporter((exporterOptions) =>
    {
        exporterOptions.Protocol = OtlpExportProtocol.HttpProtobuf;
        exporterOptions.Endpoint = new Uri(baseAddress, "v1/logs");
    });
});

var app = builder.Build();

await app.RunAsync().ConfigureAwait(false);
