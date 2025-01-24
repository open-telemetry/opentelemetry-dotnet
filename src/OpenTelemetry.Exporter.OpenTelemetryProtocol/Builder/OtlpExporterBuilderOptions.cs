// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter;

internal sealed class OtlpExporterBuilderOptions
{
    internal readonly SdkLimitOptions SdkLimitOptions;
    internal readonly ExperimentalOptions ExperimentalOptions;
    internal readonly LogRecordExportProcessorOptions? LogRecordExportProcessorOptions;
    internal readonly MetricReaderOptions? MetricReaderOptions;
    internal readonly ActivityExportProcessorOptions? ActivityExportProcessorOptions;

    internal readonly OtlpExporterOptions DefaultOptionsInstance;
    internal readonly OtlpExporterOptions LoggingOptionsInstance;
    internal readonly OtlpExporterOptions MetricsOptionsInstance;
    internal readonly OtlpExporterOptions TracingOptionsInstance;

    internal OtlpExporterBuilderOptions(
        IConfiguration configuration,
        SdkLimitOptions sdkLimitOptions,
        ExperimentalOptions experimentalOptions,
        LogRecordExportProcessorOptions? logRecordExportProcessorOptions,
        MetricReaderOptions? metricReaderOptions,
        ActivityExportProcessorOptions? activityExportProcessorOptions)
    {
        this.SdkLimitOptions = sdkLimitOptions;
        this.ExperimentalOptions = experimentalOptions;
        this.LogRecordExportProcessorOptions = logRecordExportProcessorOptions;
        this.MetricReaderOptions = metricReaderOptions;
        this.ActivityExportProcessorOptions = activityExportProcessorOptions;

        var defaultBatchOptions = this.ActivityExportProcessorOptions!.BatchExportProcessorOptions;

        this.DefaultOptionsInstance = new OtlpExporterOptions(configuration, OtlpExporterOptionsConfigurationType.Default, defaultBatchOptions);

        this.LoggingOptionsInstance = new OtlpExporterOptions(configuration, OtlpExporterOptionsConfigurationType.Logs, defaultBatchOptions);

        this.MetricsOptionsInstance = new OtlpExporterOptions(configuration, OtlpExporterOptionsConfigurationType.Metrics, defaultBatchOptions);

        this.TracingOptionsInstance = new OtlpExporterOptions(configuration, OtlpExporterOptionsConfigurationType.Traces, defaultBatchOptions);
    }

    public IOtlpExporterOptions DefaultOptions => this.DefaultOptionsInstance;

    public IOtlpExporterOptions LoggingOptions => this.LoggingOptionsInstance;

    public IOtlpExporterOptions MetricsOptions => this.MetricsOptionsInstance;

    public IOtlpExporterOptions TracingOptions => this.TracingOptionsInstance;
}
