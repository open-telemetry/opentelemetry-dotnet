// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

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

    public OtlpExporterBuilderOptions(
        IConfiguration configuration,
        SdkLimitOptions sdkLimitOptions,
        ExperimentalOptions experimentalOptions,
        LogRecordExportProcessorOptions? logRecordExportProcessorOptions,
        MetricReaderOptions? metricReaderOptions,
        ActivityExportProcessorOptions? activityExportProcessorOptions)
    {
        Debug.Assert(configuration != null, "configuration was null");
        Debug.Assert(sdkLimitOptions != null, "sdkLimitOptions was null");
        Debug.Assert(experimentalOptions != null, "experimentalOptions was null");

        this.SdkLimitOptions = sdkLimitOptions!;
        this.ExperimentalOptions = experimentalOptions!;
        this.LogRecordExportProcessorOptions = logRecordExportProcessorOptions;
        this.MetricReaderOptions = metricReaderOptions;
        this.ActivityExportProcessorOptions = activityExportProcessorOptions;

        var defaultBatchOptions = this.ActivityExportProcessorOptions!.BatchExportProcessorOptions;

        this.DefaultOptionsInstance = new OtlpExporterOptions(configuration!, OtlpExporterSignals.None, defaultBatchOptions);

        this.LoggingOptionsInstance = new OtlpExporterOptions(configuration!, OtlpExporterSignals.Logs, defaultBatchOptions);

        this.MetricsOptionsInstance = new OtlpExporterOptions(configuration!, OtlpExporterSignals.Metrics, defaultBatchOptions);

        this.TracingOptionsInstance = new OtlpExporterOptions(configuration!, OtlpExporterSignals.Traces, defaultBatchOptions);
    }

    public OtlpExporterSignals Signals { get; set; } = OtlpExporterSignals.All;

    public IOtlpExporterOptions DefaultOptions => this.DefaultOptionsInstance;

    public IOtlpExporterOptions LoggingOptions => this.LoggingOptionsInstance;

    public IOtlpExporterOptions MetricsOptions => this.MetricsOptionsInstance;

    public IOtlpExporterOptions TracingOptions => this.TracingOptionsInstance;
}
