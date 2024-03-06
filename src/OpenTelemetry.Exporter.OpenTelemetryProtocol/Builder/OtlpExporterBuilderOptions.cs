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

        this.DefaultOptions = new OtlpExporterOptionsBase(configuration!, OtlpExporterSignals.None);

        this.LoggingOptions = new OtlpExporterOptionsBase(configuration!, OtlpExporterSignals.Logs);

        this.MetricsOptions = new OtlpExporterOptionsBase(configuration!, OtlpExporterSignals.Metrics);

        this.TracingOptions = new OtlpExporterOptionsBase(configuration!, OtlpExporterSignals.Traces);
    }

    public OtlpExporterSignals Signals { get; set; } = OtlpExporterSignals.All;

    public OtlpExporterOptionsBase DefaultOptions { get; }

    public OtlpExporterOptionsBase LoggingOptions { get; }

    public OtlpExporterOptionsBase MetricsOptions { get; }

    public OtlpExporterOptionsBase TracingOptions { get; }
}
