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
        Debug.Assert(configuration != null, "configuration was null");
        Debug.Assert(sdkLimitOptions != null, "sdkLimitOptions was null");
        Debug.Assert(experimentalOptions != null, "experimentalOptions was null");

        this.SdkLimitOptions = sdkLimitOptions!;
        this.ExperimentalOptions = experimentalOptions!;
        this.LogRecordExportProcessorOptions = logRecordExportProcessorOptions;
        this.MetricReaderOptions = metricReaderOptions;
        this.ActivityExportProcessorOptions = activityExportProcessorOptions;

        var defaultOptions = this.ActivityExportProcessorOptions!;

        this.DefaultOptionsInstance = new OtlpExporterOptions(configuration!, OtlpExporterOptionsConfigurationType.Default, defaultOptions);

        this.LoggingOptionsInstance = new OtlpExporterOptions(configuration!, OtlpExporterOptionsConfigurationType.Logs, defaultOptions);

        this.MetricsOptionsInstance = new OtlpExporterOptions(configuration!, OtlpExporterOptionsConfigurationType.Metrics, defaultOptions);

        this.TracingOptionsInstance = new OtlpExporterOptions(configuration!, OtlpExporterOptionsConfigurationType.Traces, defaultOptions);
    }

    public IOtlpExporterOptions DefaultOptions => this.DefaultOptionsInstance;

    public IOtlpExporterOptions LoggingOptions => this.LoggingOptionsInstance;

    public IOtlpExporterOptions MetricsOptions => this.MetricsOptionsInstance;

    public IOtlpExporterOptions TracingOptions => this.TracingOptionsInstance;
}
