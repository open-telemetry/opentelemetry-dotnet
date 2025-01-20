// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter;

internal sealed class OtlpExporterBuilder
{
    private const int DefaultProcessorPipelineWeight = 10_000;

    private readonly string name;

    internal OtlpExporterBuilder(
        IServiceCollection services,
        string? name,
        IConfiguration? configuration)
    {
        Debug.Assert(services != null, "services was null");

        if (configuration != null)
        {
            if (string.IsNullOrEmpty(name))
            {
                name = "otlp";
            }

            BindConfigurationToOptions(services!, name, configuration);
        }

        name ??= Options.DefaultName;

        RegisterOtlpExporterServices(services!, name);

        this.name = name;
        this.Services = services!;
    }

    public IServiceCollection Services { get; }

    public OtlpExporterBuilder ConfigureDefaultExporterOptions(
        Action<IOtlpExporterOptions> configure)
    {
        Guard.ThrowIfNull(configure);

        this.Services.Configure<OtlpExporterBuilderOptions>(
            this.name,
            o => configure(o.DefaultOptions));
        return this;
    }

    public OtlpExporterBuilder ConfigureLoggingExporterOptions(
        Action<IOtlpExporterOptions> configure)
    {
        Guard.ThrowIfNull(configure);

        this.Services.Configure<OtlpExporterBuilderOptions>(
            this.name,
            o => configure(o.LoggingOptions));
        return this;
    }

    public OtlpExporterBuilder ConfigureLoggingProcessorOptions(
        Action<LogRecordExportProcessorOptions> configure)
    {
        Guard.ThrowIfNull(configure);

        this.Services.Configure(this.name, configure);
        return this;
    }

    public OtlpExporterBuilder ConfigureMetricsExporterOptions(
        Action<IOtlpExporterOptions> configure)
    {
        Guard.ThrowIfNull(configure);

        this.Services.Configure<OtlpExporterBuilderOptions>(
             this.name,
             o => configure(o.MetricsOptions));
        return this;
    }

    public OtlpExporterBuilder ConfigureMetricsReaderOptions(
        Action<MetricReaderOptions> configure)
    {
        Guard.ThrowIfNull(configure);

        this.Services.Configure(this.name, configure);
        return this;
    }

    public OtlpExporterBuilder ConfigureTracingExporterOptions(
        Action<IOtlpExporterOptions> configure)
    {
        Guard.ThrowIfNull(configure);

        this.Services.Configure<OtlpExporterBuilderOptions>(
             this.name,
             o => configure(o.TracingOptions));
        return this;
    }

    public OtlpExporterBuilder ConfigureTracingProcessorOptions(
        Action<ActivityExportProcessorOptions> configure)
    {
        Guard.ThrowIfNull(configure);

        this.Services.Configure(this.name, configure);
        return this;
    }

    private static void BindConfigurationToOptions(IServiceCollection services, string name, IConfiguration configuration)
    {
        Debug.Assert(services != null, "services was null");
        Debug.Assert(!string.IsNullOrEmpty(name), "name was null or empty");
        Debug.Assert(configuration != null, "configuration was null");

        /* Config JSON structure is expected to be something like this:
            {
                "DefaultOptions": {
                    "Endpoint": "http://default_endpoint/"
                },
                "LoggingOptions": {
                    "Endpoint": "http://logs_endpoint/"
                    "ExportProcessorType": Batch,
                    "BatchExportProcessorOptions": {
                        "ScheduledDelayMilliseconds": 5000
                    }
                },
                "MetricsOptions": {
                    "Endpoint": "http://metrics_endpoint/",
                    "TemporalityPreference": "Delta",
                    "PeriodicExportingMetricReaderOptions": {
                        "ExportIntervalMilliseconds": 5000
                    }
                },
                "TracingOptions": {
                    "Endpoint": "http://trcing_endpoint/"
                    "ExportProcessorType": Batch,
                    "BatchExportProcessorOptions": {
                        "ScheduledDelayMilliseconds": 5000
                    }
                }
            }
        */

        services!.Configure<OtlpExporterBuilderOptions>(name, configuration!);

        services!.Configure<LogRecordExportProcessorOptions>(
            name, configuration!.GetSection(nameof(OtlpExporterBuilderOptions.LoggingOptions)));

        services!.Configure<MetricReaderOptions>(
            name, configuration.GetSection(nameof(OtlpExporterBuilderOptions.MetricsOptions)));

        services!.Configure<ActivityExportProcessorOptions>(
            name, configuration.GetSection(nameof(OtlpExporterBuilderOptions.TracingOptions)));
    }

    private static void RegisterOtlpExporterServices(IServiceCollection services, string name)
    {
        Debug.Assert(services != null, "services was null");
        Debug.Assert(name != null, "name was null");

        services!.AddOtlpExporterLoggingServices();
        services!.AddOtlpExporterMetricsServices(name!);
        services!.AddOtlpExporterTracingServices();

        // Note: UseOtlpExporterRegistration is added to the service collection
        // for each invocation to detect repeated calls to "UseOtlpExporter" and
        // to throw if "AddOtlpExporter" extensions are called
        services!.AddSingleton(UseOtlpExporterRegistration.Instance);

        services!.RegisterOptionsFactory((sp, configuration, name) => new OtlpExporterBuilderOptions(
            configuration,
            /* Note: We don't use name for SdkLimitOptions. There should only be
            one provider for a given service collection so SdkLimitOptions is
            treated as a single default instance. */
            sp.GetRequiredService<IOptionsMonitor<SdkLimitOptions>>().CurrentValue,
            sp.GetRequiredService<IOptionsMonitor<ExperimentalOptions>>().Get(name),
            /* Note: We allow LogRecordExportProcessorOptions,
            MetricReaderOptions, & ActivityExportProcessorOptions to be null
            because those only exist if the corresponding signal is turned on.
            Currently this extension turns on all signals so they will always be
            there but that may change in the future so it is handled
            defensively. */
            sp.GetService<IOptionsMonitor<LogRecordExportProcessorOptions>>()?.Get(name),
            sp.GetService<IOptionsMonitor<MetricReaderOptions>>()?.Get(name),
            sp.GetService<IOptionsMonitor<ActivityExportProcessorOptions>>()?.Get(name)));

        services!.ConfigureOpenTelemetryLoggerProvider(
            (sp, logging) =>
            {
                var builderOptions = GetBuilderOptionsAndValidateRegistrations(sp, name!);

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

        services!.ConfigureOpenTelemetryMeterProvider(
            (sp, metrics) =>
            {
                var builderOptions = GetBuilderOptionsAndValidateRegistrations(sp, name!);

                metrics.AddReader(
                    OtlpMetricExporterExtensions.BuildOtlpExporterMetricReader(
                        sp,
                        builderOptions.MetricsOptionsInstance.ApplyDefaults(builderOptions.DefaultOptionsInstance),
                        builderOptions.MetricReaderOptions ?? throw new InvalidOperationException("MetricReaderOptions were missing with metrics enabled"),
                        builderOptions.ExperimentalOptions,
                        skipUseOtlpExporterRegistrationCheck: true));
            });

        services!.ConfigureOpenTelemetryTracerProvider(
            (sp, tracing) =>
            {
                var builderOptions = GetBuilderOptionsAndValidateRegistrations(sp, name!);

                var processorOptions = builderOptions.ActivityExportProcessorOptions ?? throw new InvalidOperationException("ActivityExportProcessorOptions were missing with tracing enabled");

                var processor = OtlpTraceExporterHelperExtensions.BuildOtlpExporterProcessor(
                    sp,
                    builderOptions.TracingOptionsInstance.ApplyDefaults(builderOptions.DefaultOptionsInstance),
                    builderOptions.SdkLimitOptions,
                    builderOptions.ExperimentalOptions,
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
