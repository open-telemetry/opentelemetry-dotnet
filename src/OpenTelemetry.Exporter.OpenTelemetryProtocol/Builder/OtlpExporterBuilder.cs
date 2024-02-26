// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter;

public sealed class OtlpExporterBuilder
{
    private readonly string? name;

    internal OtlpExporterBuilder(
        IServiceCollection services,
        string? name,
        IConfiguration? configuration)
    {
        Debug.Assert(services != null, "services was null");

        this.name = name;
        this.Services = services;

        if (configuration != null)
        {
            Debug.Assert(!string.IsNullOrEmpty(name), "name was null or empty");

            BindConfigurationToOptions(services, name!, configuration);
        }
    }

    public IServiceCollection Services { get; }

    public OtlpExporterBuilder SetSignalsToConfigure(OtlpExporterSignals signalsToConfigure)
    {
        this.Services.Configure<OtlpExporterBuilderOptions>(
            this.name,
            o => o.Signals = signalsToConfigure);
        return this;
    }

    public OtlpExporterBuilder ConfigureDefaultOtlpExporterOptions(
        Action<OtlpExporterOptionsBase> configure)
    {
        Guard.ThrowIfNull(configure);

        this.Services.Configure<OtlpExporterBuilderOptions>(
            this.name,
            o => configure(o.DefaultOptions));
        return this;
    }

    public OtlpExporterBuilder ConfigureLoggingOtlpExporterOptions(
        Action<OtlpExporterOptionsBase> configure)
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

    public OtlpExporterBuilder ConfigureMetricsOtlpExporterOptions(
        Action<OtlpExporterOptionsBase> configure)
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

    public OtlpExporterBuilder ConfigureTracingOtlpExporterOptions(
        Action<OtlpExporterOptionsBase> configure)
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

var json = """
{
    "Signals": "Logs, Metrics",
    "DefaultOptions": {
    },
    "LoggingOptions": {
        "ExportProcessorType": Batch,
        "BatchExportProcessorOptions": {
            "ScheduledDelayMilliseconds": 1000
        }
    },
    "MetricsOptions": {
    },
    "TracingOptions": {
    }
}
""";

        services.Configure<OtlpExporterBuilderOptions>(name, configuration);

        services.Configure<LogRecordExportProcessorOptions>(
            name, configuration.GetSection(nameof(OtlpExporterBuilderOptions.LoggingOptions)));

        services.Configure<MetricReaderOptions>(
            name, configuration.GetSection(nameof(OtlpExporterBuilderOptions.MetricsOptions)));

        services.Configure<ActivityExportProcessorOptions>(
            name, configuration.GetSection(nameof(OtlpExporterBuilderOptions.TracingOptions)));
    }
}
