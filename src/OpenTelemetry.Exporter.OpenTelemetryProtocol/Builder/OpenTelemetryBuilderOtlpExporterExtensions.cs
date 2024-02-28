// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter;

public static class OpenTelemetryBuilderOtlpExporterExtensions
{
    public static IOpenTelemetryBuilder AddOtlpExporter(
        this IOpenTelemetryBuilder builder)
        => AddOtlpExporter(builder, name: null);

    public static IOpenTelemetryBuilder AddOtlpExporter(
        this IOpenTelemetryBuilder builder,
        Uri endpoint)
        => AddOtlpExporter(builder, OtlpExportProtocol.Grpc, endpoint);

    public static IOpenTelemetryBuilder AddOtlpExporter(
        this IOpenTelemetryBuilder builder,
        OtlpExportProtocol protocol,
        Uri endpoint)
    {
        Guard.ThrowIfNull(endpoint);

        return AddOtlpExporter(builder, name: null, configure: otlpBuilder =>
        {
            otlpBuilder.ConfigureDefaultExporterOptions(o =>
            {
                o.Protocol = protocol;
                if (endpoint != null)
                {
                    o.Endpoint = endpoint;
                }
            });
        });
    }

    public static IOpenTelemetryBuilder AddOtlpExporter(
        this IOpenTelemetryBuilder builder,
        Action<OtlpExporterBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        return AddOtlpExporter(builder, name: null, configure: configure);
    }

    public static IOpenTelemetryBuilder AddOtlpExporter(
        this IOpenTelemetryBuilder builder,
        IConfiguration configuration)
    {
        Guard.ThrowIfNull(configuration);

        return AddOtlpExporter(builder, name: null, configuration: configuration);
    }

    public static IOpenTelemetryBuilder AddOtlpExporter(
        this IOpenTelemetryBuilder builder,
        string? name = null,
        IConfiguration? configuration = null,
        Action<OtlpExporterBuilder>? configure = null,
        bool addToEndOfPipeline = true)
    {
        Guard.ThrowIfNull(builder);

        if (configuration == null && string.IsNullOrEmpty(name))
        {
            name = "otlp";
        }

        var otlpBuilder = new OtlpExporterBuilder(builder.Services, name, configuration);

        configure?.Invoke(otlpBuilder);

        AddOtlpExporterInternal(builder, name, addToEndOfPipeline);

        return builder;
    }

    private static void AddOtlpExporterInternal(IOpenTelemetryBuilder builder, string? name, bool addToEndOfPipeline)
    {
        builder.Services.RegisterOptionsFactory(configuration => new SdkLimitOptions(configuration));
        builder.Services.RegisterOptionsFactory(configuration => new ExperimentalOptions(configuration));
        builder.Services.RegisterOptionsFactory((sp, configuration, name) => new OtlpExporterBuilderOptions(
            configuration,
            sp.GetRequiredService<IOptionsMonitor<SdkLimitOptions>>().CurrentValue,
            sp.GetRequiredService<IOptionsMonitor<ExperimentalOptions>>().CurrentValue,
            sp.GetService<IOptionsMonitor<LogRecordExportProcessorOptions>>()?.Get(name),
            sp.GetService<IOptionsMonitor<MetricReaderOptions>>()?.Get(name),
            sp.GetService<IOptionsMonitor<ActivityExportProcessorOptions>>()?.Get(name)));

        name ??= Options.DefaultName;

        builder.Services.ConfigureOpenTelemetryLoggerProvider(
            (sp, logging) =>
            {
                var builderOptions = GetBuilderOptions(sp, name);

                if (!builderOptions.Signals.HasFlag(OtlpExporterSignals.Logs))
                {
                    return;
                }

                var processor = OtlpLogExporterHelperExtensions.BuildOtlpLogExporter(
                    sp,
                    builderOptions.LoggingOptions.ApplyDefaults(builderOptions.DefaultOptions),
                    builderOptions.LogRecordExportProcessorOptions ?? throw new NotSupportedException(),
                    builderOptions.SdkLimitOptions,
                    builderOptions.ExperimentalOptions);

                processor.Weight = addToEndOfPipeline ? int.MaxValue : 0;

                logging.AddProcessor(processor);
            });

        builder.Services.ConfigureOpenTelemetryMeterProvider(
            (sp, metrics) =>
            {
                var builderOptions = GetBuilderOptions(sp, name);

                if (!builderOptions.Signals.HasFlag(OtlpExporterSignals.Metrics))
                {
                    return;
                }

                metrics.AddReader(
                    OtlpMetricExporterExtensions.BuildOtlpExporterMetricReader(
                        builderOptions.MetricsOptions.ApplyDefaults(builderOptions.DefaultOptions),
                        builderOptions.MetricReaderOptions ?? throw new NotSupportedException(),
                        sp));
            });

        builder.Services.ConfigureOpenTelemetryTracerProvider(
            (sp, tracing) =>
            {
                var builderOptions = GetBuilderOptions(sp, name);

                if (!builderOptions.Signals.HasFlag(OtlpExporterSignals.Traces))
                {
                    return;
                }

                var processorOptions = builderOptions.ActivityExportProcessorOptions ?? throw new NotSupportedException();

                var processor = OtlpTraceExporterHelperExtensions.BuildOtlpExporterProcessor(
                    builderOptions.TracingOptions.ApplyDefaults(builderOptions.DefaultOptions),
                    builderOptions.SdkLimitOptions,
                    processorOptions.ExportProcessorType,
                    processorOptions.BatchExportProcessorOptions,
                    sp);

                processor.Weight = addToEndOfPipeline ? int.MaxValue : 0;

                tracing.AddProcessor(processor);
            });

        static OtlpExporterBuilderOptions GetBuilderOptions(IServiceProvider sp, string name)
        {
            return sp.GetRequiredService<IOptionsMonitor<OtlpExporterBuilderOptions>>().Get(name);
        }
    }
}
