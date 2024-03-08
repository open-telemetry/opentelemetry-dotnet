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

/// <summary>
/// Contains extension methods to facilitate registration of the OpenTelemetry
/// Protocol (OTLP) exporter into an <see cref="IOpenTelemetryBuilder"/>
/// instance.
/// </summary>
public static class OpenTelemetryBuilderOtlpExporterExtensions
{
    /// <summary>
    /// Uses OpenTelemetry Protocol (OTLP) exporter for all signals.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>Calling this method automatically enables logging, metrics, and
    /// tracing.</item>
    /// <item>The exporter registered by this method will be added as the last
    /// processor in the pipeline established for logging and tracing.</item>
    /// <item>This method can only be called once. Subsequent calls will results
    /// in a <see cref="NotSupportedException"/> being thrown.</item>
    /// <item>This method cannot be called in addition to signal-specific
    /// <c>AddOtlpExporter</c> methods. If this method is called signal-specific
    /// <c>AddOtlpExporter</c> calls will result in a <see
    /// cref="NotSupportedException"/> being thrown.</item>
    /// </list>
    /// </remarks>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <returns>Supplied <see cref="IOpenTelemetryBuilder"/> for chaining calls.</returns>
    public static IOpenTelemetryBuilder UseOtlpExporter(
        this IOpenTelemetryBuilder builder)
        => UseOtlpExporter(builder, name: null);

    /// <summary><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)"/></summary>
    /// <remarks><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
    /// <returns><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)" path="/returns"/></returns>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <param name="baseEndpoint">The base endpoint to use. A signal-specific
    /// path will be appended to the base endpoint for each signal
    /// automatically.</param>
    public static IOpenTelemetryBuilder UseOtlpExporter(
        this IOpenTelemetryBuilder builder,
        Uri baseEndpoint)
        => UseOtlpExporter(builder, OtlpExportProtocol.Grpc, baseEndpoint);

    /// <summary><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)"/></summary>
    /// <remarks><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
    /// <returns><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder)" path="/returns"/></returns>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <param name="protocol"><see cref="OtlpExportProtocol"/>.</param>
    /// <param name="baseEndpoint"><inheritdoc cref="UseOtlpExporter(IOpenTelemetryBuilder, Uri)" path="/param[@name='baseEndpoint']"/></param>
    public static IOpenTelemetryBuilder UseOtlpExporter(
        this IOpenTelemetryBuilder builder,
        OtlpExportProtocol protocol,
        Uri baseEndpoint)
    {
        Guard.ThrowIfNull(baseEndpoint);

        return UseOtlpExporter(builder, name: null, configure: otlpBuilder =>
        {
            otlpBuilder.ConfigureDefaultExporterOptions(o =>
            {
                o.Protocol = protocol;
                if (baseEndpoint != null)
                {
                    o.Endpoint = baseEndpoint;
                }
            });
        });
    }

    internal static IOpenTelemetryBuilder AddOtlpExporter(
        this IOpenTelemetryBuilder builder,
        Action<OtlpExporterBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        return UseOtlpExporter(builder, name: null, configure: configure);
    }

    internal static IOpenTelemetryBuilder UseOtlpExporter(
        this IOpenTelemetryBuilder builder,
        IConfiguration configuration)
    {
        Guard.ThrowIfNull(configuration);

        return UseOtlpExporter(builder, name: null, configuration: configuration);
    }

    internal static IOpenTelemetryBuilder UseOtlpExporter(
        this IOpenTelemetryBuilder builder,
        string? name = null,
        IConfiguration? configuration = null,
        Action<OtlpExporterBuilder>? configure = null,
        int processorPipelineWeight = 10_000)
    {
        Guard.ThrowIfNull(builder);

        if (configuration == null && string.IsNullOrEmpty(name))
        {
            name = "otlp";
        }

        var otlpBuilder = new OtlpExporterBuilder(builder.Services, name, configuration);

        configure?.Invoke(otlpBuilder);

        UseOtlpExporterInternal(builder, name, processorPipelineWeight);

        return builder;
    }

    private static void UseOtlpExporterInternal(IOpenTelemetryBuilder builder, string? name, int processorPipelineWeight)
    {
        builder
            .WithLogging()
            .WithMetrics()
            .WithTracing();

        var services = builder.Services;

        services.AddSingleton<UseOtlpExporterRegistration>();

        services.RegisterOptionsFactory(configuration => new SdkLimitOptions(configuration));
        services.RegisterOptionsFactory(configuration => new ExperimentalOptions(configuration));
        services.RegisterOptionsFactory((sp, configuration, name) => new OtlpExporterBuilderOptions(
            configuration,
            sp.GetRequiredService<IOptionsMonitor<SdkLimitOptions>>().CurrentValue,
            sp.GetRequiredService<IOptionsMonitor<ExperimentalOptions>>().CurrentValue,
            sp.GetService<IOptionsMonitor<LogRecordExportProcessorOptions>>()?.Get(name),
            sp.GetService<IOptionsMonitor<MetricReaderOptions>>()?.Get(name),
            sp.GetService<IOptionsMonitor<ActivityExportProcessorOptions>>()?.Get(name)));

        name ??= Options.DefaultName;

        services.ConfigureOpenTelemetryLoggerProvider(
            (sp, logging) =>
            {
                var builderOptions = GetBuilderOptionsAndValidateRegistrations(sp, name);

                if (!builderOptions.Signals.HasFlag(OtlpExporterSignals.Logs))
                {
                    return;
                }

                var processor = OtlpLogExporterHelperExtensions.BuildOtlpLogExporter(
                    sp,
                    builderOptions.LoggingOptionsInstance.ApplyDefaults(builderOptions.DefaultOptionsInstance),
                    builderOptions.LogRecordExportProcessorOptions ?? throw new NotSupportedException(),
                    builderOptions.SdkLimitOptions,
                    builderOptions.ExperimentalOptions);

                processor.PipelineWeight = processorPipelineWeight;

                logging.AddProcessor(processor);
            });

        services.ConfigureOpenTelemetryMeterProvider(
            (sp, metrics) =>
            {
                var builderOptions = GetBuilderOptionsAndValidateRegistrations(sp, name);

                if (!builderOptions.Signals.HasFlag(OtlpExporterSignals.Metrics))
                {
                    return;
                }

                metrics.AddReader(
                    OtlpMetricExporterExtensions.BuildOtlpExporterMetricReader(
                        builderOptions.MetricsOptionsInstance.ApplyDefaults(builderOptions.DefaultOptionsInstance),
                        builderOptions.MetricReaderOptions ?? throw new NotSupportedException(),
                        sp));
            });

        services.ConfigureOpenTelemetryTracerProvider(
            (sp, tracing) =>
            {
                var builderOptions = GetBuilderOptionsAndValidateRegistrations(sp, name);

                if (!builderOptions.Signals.HasFlag(OtlpExporterSignals.Traces))
                {
                    return;
                }

                var processorOptions = builderOptions.ActivityExportProcessorOptions ?? throw new NotSupportedException();

                var processor = OtlpTraceExporterHelperExtensions.BuildOtlpExporterProcessor(
                    builderOptions.TracingOptionsInstance.ApplyDefaults(builderOptions.DefaultOptionsInstance),
                    builderOptions.SdkLimitOptions,
                    processorOptions.ExportProcessorType,
                    processorOptions.BatchExportProcessorOptions,
                    sp);

                processor.PipelineWeight = processorPipelineWeight;

                tracing.AddProcessor(processor);
            });

        static OtlpExporterBuilderOptions GetBuilderOptionsAndValidateRegistrations(IServiceProvider sp, string name)
        {
            sp.EnsureSingleUseOtlpExporterRegistration();

            return sp.GetRequiredService<IOptionsMonitor<OtlpExporterBuilderOptions>>().Get(name);
        }
    }
}
