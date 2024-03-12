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
    private const int DefaultProcessorPipelineWeight = 10_000;

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
    /// automatically if the protocol is set to <see cref="OtlpExportProtocol.HttpProtobuf"/>.</param>
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
                o.Endpoint = baseEndpoint;
            });
        });
    }

    internal static IOpenTelemetryBuilder UseOtlpExporter(
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
        Action<OtlpExporterBuilder>? configure = null)
    {
        Guard.ThrowIfNull(builder);

        if (configuration != null && string.IsNullOrEmpty(name))
        {
            name = "otlp";
        }

        var otlpBuilder = new OtlpExporterBuilder(builder.Services, name, configuration);

        configure?.Invoke(otlpBuilder);

        UseOtlpExporterInternal(builder, name);

        return builder;
    }

    private static void UseOtlpExporterInternal(IOpenTelemetryBuilder builder, string? name)
    {
        name ??= Options.DefaultName;

        // Note: We automatically turn on signals for "UseOtlpExporter"
        builder
            .WithLogging()
            .WithMetrics()
            .WithTracing();

        var services = builder.Services;

        // Note: UseOtlpExporterRegistration is added to the service collection
        // to detect repeated calls to "UseOtlpExporter" and to throw if
        // "AddOtlpExporter" extensions are called
        services.AddSingleton<UseOtlpExporterRegistration>();

        services.RegisterOptionsFactory(configuration => new SdkLimitOptions(configuration));
        services.RegisterOptionsFactory(configuration => new ExperimentalOptions(configuration));
        services.RegisterOptionsFactory((sp, configuration, name) => new OtlpExporterBuilderOptions(
            configuration,
            sp.GetRequiredService<IOptionsMonitor<SdkLimitOptions>>().CurrentValue,
            sp.GetRequiredService<IOptionsMonitor<ExperimentalOptions>>().CurrentValue,
            /* Note: We allow LogRecordExportProcessorOptions,
            MetricReaderOptions, & ActivityExportProcessorOptions to be null
            because those only exist if the corresponding signal is turned on.
            Currently this extension turns on all signals so they will always be
            there but that may change in the future so it is handled
            defensively. */
            sp.GetService<IOptionsMonitor<LogRecordExportProcessorOptions>>()?.Get(name),
            sp.GetService<IOptionsMonitor<MetricReaderOptions>>()?.Get(name),
            sp.GetService<IOptionsMonitor<ActivityExportProcessorOptions>>()?.Get(name)));

        services.ConfigureOpenTelemetryLoggerProvider(
            (sp, logging) =>
            {
                var builderOptions = GetBuilderOptionsAndValidateRegistrations(sp, name);

                var processor = OtlpLogExporterHelperExtensions.BuildOtlpLogExporter(
                    sp,
                    builderOptions.LoggingOptionsInstance.ApplyDefaults(builderOptions.DefaultOptionsInstance),
                    builderOptions.LogRecordExportProcessorOptions ?? throw new InvalidOperationException("LogRecordExportProcessorOptions were missing with logging enabled"),
                    builderOptions.SdkLimitOptions,
                    builderOptions.ExperimentalOptions,
                    skipUseOtlpExporterRegistrationCheck: true);

                processor.PipelineWeight = DefaultProcessorPipelineWeight;

                logging.AddProcessor(processor);
            });

        services.ConfigureOpenTelemetryMeterProvider(
            (sp, metrics) =>
            {
                var builderOptions = GetBuilderOptionsAndValidateRegistrations(sp, name);

                metrics.AddReader(
                    OtlpMetricExporterExtensions.BuildOtlpExporterMetricReader(
                        sp,
                        builderOptions.MetricsOptionsInstance.ApplyDefaults(builderOptions.DefaultOptionsInstance),
                        builderOptions.MetricReaderOptions ?? throw new InvalidOperationException("MetricReaderOptions were missing with metrics enabled"),
                        skipUseOtlpExporterRegistrationCheck: true));
            });

        services.ConfigureOpenTelemetryTracerProvider(
            (sp, tracing) =>
            {
                var builderOptions = GetBuilderOptionsAndValidateRegistrations(sp, name);

                var processorOptions = builderOptions.ActivityExportProcessorOptions ?? throw new InvalidOperationException("ActivityExportProcessorOptions were missing with tracing enabled");

                var processor = OtlpTraceExporterHelperExtensions.BuildOtlpExporterProcessor(
                    sp,
                    builderOptions.TracingOptionsInstance.ApplyDefaults(builderOptions.DefaultOptionsInstance),
                    builderOptions.SdkLimitOptions,
                    processorOptions.ExportProcessorType,
                    processorOptions.BatchExportProcessorOptions,
                    skipUseOtlpExporterRegistrationCheck: true);

                processor.PipelineWeight = DefaultProcessorPipelineWeight;

                tracing.AddProcessor(processor);
            });

        static OtlpExporterBuilderOptions GetBuilderOptionsAndValidateRegistrations(IServiceProvider sp, string name)
        {
            sp.EnsureSingleUseOtlpExporterRegistration();

            return sp.GetRequiredService<IOptionsMonitor<OtlpExporterBuilderOptions>>().Get(name);
        }
    }
}
