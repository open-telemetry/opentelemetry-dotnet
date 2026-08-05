// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Maui.TestApp;

/// <summary>
/// Builds the MAUI application host. This is the code under test: MAUI's own
/// Android startup calls it, and the on-device tests then resolve the providers
/// it configured from <see cref="MauiApp.Services"/>.
/// </summary>
/// <remarks>
/// The SDK is registered the way a MAUI app would register it, through the
/// application host's service collection, so this exercises
/// <c>OpenTelemetry.Extensions.Hosting</c> in addition to the SDK itself. Note
/// that a MAUI app is not an <c>IHost</c> and so never runs the
/// <c>IHostedService</c> that would otherwise start the providers - they are
/// created when they are first resolved instead.
/// </remarks>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder.UseMauiApp<App>();

        builder.Services.AddSingleton<InstrumentationSource>();

        builder.Services.AddOpenTelemetry()
            .ConfigureResource((resource) => resource.AddService(InstrumentationSource.ServiceName))
            .WithTracing((tracing) => tracing
                .AddSource(InstrumentationSource.ActivitySourceName)
                .SetSampler(new AlwaysOnSampler())
                .AddOtlpExporter((options) => ConfigureOtlp(options, "v1/traces")))
            .WithMetrics((metrics) => metrics
                .AddMeter(InstrumentationSource.MeterName)
                .AddOtlpExporter((options) => ConfigureOtlp(options, "v1/metrics")));

        builder.Logging.AddOpenTelemetry((options) =>
        {
            // Logging is configured separately from ConfigureResource() above, so
            // the resource has to be set again for the log records to carry it.
            options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(InstrumentationSource.ServiceName));
            options.IncludeFormattedMessage = true;

            options.AddOtlpExporter((exporterOptions, processorOptions) =>
            {
                ConfigureOtlp(exporterOptions, "v1/logs");

                // Export each record as it is emitted. The ILoggerFactory is owned
                // by the container, so the tests cannot dispose it to flush the
                // pipeline, and there is no equivalent of TracerProvider.ForceFlush
                // reachable from the resolved ILoggerFactory.
                processorOptions.ExportProcessorType = ExportProcessorType.Simple;
            });
        });

        return builder.Build();
    }

    private static void ConfigureOtlp(OtlpExporterOptions options, string signalPath)
    {
        options.Protocol = OtlpExportProtocol.HttpProtobuf;
        options.Endpoint = new(new Uri(InstrumentationSource.OtlpEndpoint), signalPath);
    }
}
