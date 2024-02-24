// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

public static class OpenTelemetryBuilderOtlpExporterExtensions
{
    public static IOpenTelemetryBuilder AddOtlpExporter(
        this IOpenTelemetryBuilder builder)
        => AddOtlpExporter(builder, name: null, configure: null);

    public static IOpenTelemetryBuilder AddOtlpExporter(
        this IOpenTelemetryBuilder builder,
        Action<OtlpExporterBuilderOptions>? configure)
        => AddOtlpExporter(builder, name: null, configure);

    public static IOpenTelemetryBuilder AddOtlpExporter(
        this IOpenTelemetryBuilder builder,
        string? name,
        Action<OtlpExporterBuilderOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        builder.Services.RegisterOptionsFactory(configuration => new SdkLimitOptions(configuration));
        builder.Services.RegisterOptionsFactory(configuration => new ExperimentalOptions(configuration));

        builder.Services.RegisterOptionsFactory(
            (sp, config, name) =>
            new OtlpExporterBuilderOptions(
                config,
                sp.GetRequiredService<IOptionsMonitor<ActivityExportProcessorOptions>>().Get(name)));

        name ??= Options.DefaultName;

        if (configure != null)
        {
            builder.Services.Configure(name, configure);
        }

        builder.Services.ConfigureOpenTelemetryLoggerProvider(
            (sp, logging) =>
            {
                var builderOptions = GetOptions(sp, name);

                if (!builderOptions.AddToLoggerProvider)
                {
                    return;
                }

                logging.AddProcessor(
                    OtlpLogExporterHelperExtensions.BuildOtlpLogExporter(
                        sp,
                        OtlpExporterOptions.Merge(builderOptions.DefaultOptions, builderOptions.LoggingOptions),
                        sp.GetRequiredService<IOptionsMonitor<LogRecordExportProcessorOptions>>().Get(name),
                        sp.GetRequiredService<IOptionsMonitor<SdkLimitOptions>>().CurrentValue,
                        sp.GetRequiredService<IOptionsMonitor<ExperimentalOptions>>().Get(name)));
            });

        builder.Services.ConfigureOpenTelemetryMeterProvider(
            (sp, metrics) =>
            {
                var builderOptions = GetOptions(sp, name);

                if (!builderOptions.AddToMeterProvider)
                {
                    return;
                }

                metrics.AddReader(
                    OtlpMetricExporterExtensions.BuildOtlpExporterMetricReader(
                        OtlpExporterOptions.Merge(builderOptions.DefaultOptions, builderOptions.MetricsOptions),
                        sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(name),
                        sp));
            });

        builder.Services.ConfigureOpenTelemetryTracerProvider(
            (sp, tracing) =>
            {
                var builderOptions = GetOptions(sp, name);

                if (!builderOptions.AddToTracerProvider)
                {
                    return;
                }

                tracing.AddProcessor(
                    OtlpTraceExporterHelperExtensions.BuildOtlpExporterProcessor(
                        OtlpExporterOptions.Merge(builderOptions.DefaultOptions, builderOptions.TracingOptions),
                        sp.GetRequiredService<IOptionsMonitor<SdkLimitOptions>>().CurrentValue,
                        sp));
            });

        return builder;

        static OtlpExporterBuilderOptions GetOptions(IServiceProvider sp, string name)
        {
            return sp.GetRequiredService<IOptionsMonitor<OtlpExporterBuilderOptions>>().Get(name);
        }
    }
}
