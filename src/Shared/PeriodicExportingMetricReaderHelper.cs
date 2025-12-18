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
        var periodicOptions = new PeriodicExportingMetricReaderOptions
        {
            ExportIntervalMilliseconds = options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds ?? defaultExportIntervalMilliseconds,
            ExportTimeoutMilliseconds = options.PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds ?? defaultExportTimeoutMilliseconds,
        };

        var metricReader = new PeriodicExportingMetricReader(exporter, periodicOptions)
        {
            TemporalityPreference = options.TemporalityPreference,
        };

        return metricReader;
    }
}
