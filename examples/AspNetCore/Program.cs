// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using Examples.AspNetCore;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var appBuilder = WebApplication.CreateBuilder(args);

// Note: Switch between Zipkin/OTLP/Console by setting UseTracingExporter in appsettings.json.
var tracingExporter = appBuilder.Configuration.GetValue("UseTracingExporter", defaultValue: "console").ToLowerInvariant();

// Note: Switch between Prometheus/OTLP/Console by setting UseMetricsExporter in appsettings.json.
var metricsExporter = appBuilder.Configuration.GetValue("UseMetricsExporter", defaultValue: "console").ToLowerInvariant();

// Note: Switch between Console/OTLP by setting UseLogExporter in appsettings.json.
var logExporter = appBuilder.Configuration.GetValue("UseLogExporter", defaultValue: "console").ToLowerInvariant();

// Note: Switch between Explicit/Exponential by setting HistogramAggregation in appsettings.json
var histogramAggregation = appBuilder.Configuration.GetValue("HistogramAggregation", defaultValue: "explicit").ToLowerInvariant();

// Create a service to expose ActivitySource, and Metric Instruments
// for manual instrumentation
appBuilder.Services.AddSingleton<Instrumentation>();

// Clear default logging providers used by WebApplication host.
appBuilder.Logging.ClearProviders();

// Configure OpenTelemetry logging, metrics, & tracing with auto-start using the
// AddOpenTelemetry extension from OpenTelemetry.Extensions.Hosting.
appBuilder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r
        .AddService(
            serviceName: appBuilder.Configuration.GetValue("ServiceName", defaultValue: "otel-test")!,
            serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
            serviceInstanceId: Environment.MachineName))
    .WithTracing(builder =>
    {
        // Tracing

        // Ensure the TracerProvider subscribes to any custom ActivitySources.
        builder
            .AddSource(Instrumentation.ActivitySourceName)
            .SetSampler(new AlwaysOnSampler())
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation();

        // Use IConfiguration binding for AspNetCore instrumentation options.
        appBuilder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(appBuilder.Configuration.GetSection("AspNetCoreInstrumentation"));

        switch (tracingExporter)
        {
            case "zipkin":
                builder.AddZipkinExporter();

                builder.ConfigureServices(services =>
                {
                    // Use IConfiguration binding for Zipkin exporter options.
                    services.Configure<ZipkinExporterOptions>(appBuilder.Configuration.GetSection("Zipkin"));
                });
                break;

            case "otlp":
                builder.AddOtlpExporter(otlpOptions =>
                {
                    // Use IConfiguration directly for Otlp exporter endpoint option.
                    otlpOptions.Endpoint = new Uri(appBuilder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317"));
                });
                break;

            default:
                builder.AddConsoleExporter();
                break;
        }
    })
    .WithMetrics(builder =>
    {
        // Metrics

        // Ensure the MeterProvider subscribes to any custom Meters.
        builder
            .AddMeter(Instrumentation.MeterName)
            .SetExemplarFilter(ExemplarFilterType.TraceBased)
            .AddRuntimeInstrumentation()
            .AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation();

        switch (histogramAggregation)
        {
            case "exponential":
                builder.AddView(instrument =>
                {
                    return instrument.GetType().GetGenericTypeDefinition() == typeof(Histogram<>)
                        ? new Base2ExponentialBucketHistogramConfiguration()
                        : null;
                });
                break;
            default:
                // Explicit bounds histogram is the default.
                // No additional configuration necessary.
                break;
        }

        switch (metricsExporter)
        {
            case "prometheus":
                builder.AddPrometheusExporter();
                break;
            case "otlp":
                builder.AddOtlpExporter(otlpOptions =>
                {
                    // Use IConfiguration directly for Otlp exporter endpoint option.
                    otlpOptions.Endpoint = new Uri(appBuilder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317")!);
                });
                break;
            default:
                builder.AddConsoleExporter();
                break;
        }
    })
    .WithLogging(builder =>
    {
        // Note: See appsettings.json Logging:OpenTelemetry section for configuration.

        switch (logExporter)
        {
            case "otlp":
                builder.AddOtlpExporter(otlpOptions =>
                {
                    // Use IConfiguration directly for Otlp exporter endpoint option.
                    otlpOptions.Endpoint = new Uri(appBuilder.Configuration.GetValue("Otlp:Endpoint", defaultValue: "http://localhost:4317"));
                });
                break;
            default:
                builder.AddConsoleExporter();
                break;
        }
    });

appBuilder.Services.AddControllers();

appBuilder.Services.AddEndpointsApiExplorer();

appBuilder.Services.AddSwaggerGen();

var app = appBuilder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Configure OpenTelemetry Prometheus AspNetCore middleware scrape endpoint if enabled.
if (metricsExporter.Equals("prometheus", StringComparison.OrdinalIgnoreCase))
{
    app.UseOpenTelemetryPrometheusScrapingEndpoint();
}

app.Run();
