// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

internal static class PeriodicExportingMetricReaderHelper
{
    internal const int DefaultExportIntervalMilliseconds = 60000;
    internal const int DefaultExportTimeoutMilliseconds = 30000;

    internal static PeriodicExportingMetricReader CreatePeriodicExportingMetricReader(
        BaseExporter<Metric> exporter,
        MetricReaderOptions options,
        int defaultExportIntervalMilliseconds = DefaultExportIntervalMilliseconds,
        int defaultExportTimeoutMilliseconds = DefaultExportTimeoutMilliseconds)
    {
        var exportIntervalMilliseconds = options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds ?? defaultExportIntervalMilliseconds;
        var exportTimeoutMilliseconds = options.PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds ?? defaultExportTimeoutMilliseconds;

        var metricReader = options.PeriodicExportingMetricReaderOptions.MaxExportBatchSize is { } maxExportBatchSize
            ? new PeriodicExportingMetricReader(exporter, exportIntervalMilliseconds, exportTimeoutMilliseconds, maxExportBatchSize)
            : new PeriodicExportingMetricReader(exporter, exportIntervalMilliseconds, exportTimeoutMilliseconds);

        metricReader.TemporalityPreference = options.TemporalityPreference;
        metricReader.DefaultHistogramAggregation = options.DefaultHistogramAggregation;

        return metricReader;
    }
}
