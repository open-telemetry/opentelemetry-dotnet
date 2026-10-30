// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Contains periodic metric reader options.
/// </summary>
/// <remarks>
/// Note: OTEL_METRIC_EXPORT_INTERVAL, OTEL_METRIC_EXPORT_TIMEOUT and
/// OTEL_METRIC_EXPORT_MAX_BATCH_SIZE environment variables are parsed during
/// object construction.
/// </remarks>
public class PeriodicExportingMetricReaderOptions
{
    internal const string OTelMetricExportIntervalEnvVarKey = "OTEL_METRIC_EXPORT_INTERVAL";
    internal const string OTelMetricExportTimeoutEnvVarKey = "OTEL_METRIC_EXPORT_TIMEOUT";
    internal const string OTelMetricExportMaxBatchSizeEnvVarKey = "OTEL_METRIC_EXPORT_MAX_BATCH_SIZE";

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicExportingMetricReaderOptions"/> class.
    /// </summary>
    public PeriodicExportingMetricReaderOptions()
        : this(EnvironmentVariablesConfiguration.Create())
    {
    }

    internal PeriodicExportingMetricReaderOptions(IConfiguration configuration)
    {
        if (configuration.TryGetIntValue(OpenTelemetrySdkEventSource.Log, OTelMetricExportIntervalEnvVarKey, out var interval))
        {
            this.ExportIntervalMilliseconds = interval;
        }

        if (configuration.TryGetIntValue(OpenTelemetrySdkEventSource.Log, OTelMetricExportTimeoutEnvVarKey, out var timeout))
        {
            this.ExportTimeoutMilliseconds = timeout;
        }

        if (configuration.TryGetIntValue(OpenTelemetrySdkEventSource.Log, OTelMetricExportMaxBatchSizeEnvVarKey, out var maxExportBatchSize))
        {
            if (maxExportBatchSize > 0)
            {
                this.MaxExportBatchSize = maxExportBatchSize;
            }
            else
            {
                OpenTelemetrySdkEventSource.Log.InvalidConfigurationValue(OTelMetricExportMaxBatchSizeEnvVarKey, configuration[OTelMetricExportMaxBatchSizeEnvVarKey]);
            }
        }
    }

    /// <summary>
    /// Gets or sets the metric export interval in milliseconds.
    /// If not set, the default value depends on the type of metric exporter
    /// associated with the metric reader.
    /// </summary>
    public int? ExportIntervalMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets the metric export timeout in milliseconds.
    /// If not set, the default value depends on the type of metric exporter
    /// associated with the metric reader.
    /// </summary>
    public int? ExportTimeoutMilliseconds { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of metric points in a batch provided
    /// to a single export. If not set, the number of metric points in a batch
    /// is not limited.
    /// </summary>
    /// <remarks>
    /// When set, the metric points of a single collection are split into
    /// batches of at most this many metric points, which may split up the
    /// metric points of a single metric into different batches.
    /// </remarks>
    public int? MaxExportBatchSize { get; set; }
}
