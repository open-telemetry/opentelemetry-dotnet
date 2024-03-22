// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Contains periodic metric reader options.
/// </summary>
/// <remarks>
/// Note: OTEL_METRIC_EXPORT_INTERVAL and OTEL_METRIC_EXPORT_TIMEOUT environment
/// variables are parsed during object construction.
/// </remarks>
public class PeriodicExportingMetricReaderOptions
{
    internal const string OTelMetricExportIntervalEnvVarKey = "OTEL_METRIC_EXPORT_INTERVAL";
    internal const string OTelMetricExportTimeoutEnvVarKey = "OTEL_METRIC_EXPORT_TIMEOUT";

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicExportingMetricReaderOptions"/> class.
    /// </summary>
    public PeriodicExportingMetricReaderOptions()
        : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal PeriodicExportingMetricReaderOptions(IConfiguration configuration)
    {
        if (configuration.TryGetIntValue(OTelMetricExportIntervalEnvVarKey, out var interval))
        {
            this.ExportIntervalMilliseconds = interval;
        }

        if (configuration.TryGetIntValue(OTelMetricExportTimeoutEnvVarKey, out var timeout))
        {
            this.ExportTimeoutMilliseconds = timeout;
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
}
