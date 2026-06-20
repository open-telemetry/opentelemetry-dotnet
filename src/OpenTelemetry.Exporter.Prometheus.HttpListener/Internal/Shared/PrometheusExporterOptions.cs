// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Prometheus;

/// <summary>
/// Prometheus exporter options.
/// </summary>
internal sealed class PrometheusExporterOptions
{
    public PrometheusExporterOptions()
    {
        this.ScopeInfoEnabled = true;
        this.ScrapeResponseCacheDurationMilliseconds = 300;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the scope information (name, version, schema URL) is added to the scrape response.
    /// Default value: <see langword="true"/>.
    /// </summary>
    public bool ScopeInfoEnabled { get; set; }

    /// <summary>
    /// Gets or sets the cache duration in milliseconds for scrape responses. Default value: 300.
    /// </summary>
    /// <remarks>
    /// Note: Specify 0 to disable response caching.
    /// </remarks>
    public int ScrapeResponseCacheDurationMilliseconds
    {
        get;
        set
        {
            Guard.ThrowIfOutOfRange(value, min: 0);
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether addition of _total suffix for counter metric names is disabled. Default value: <see langword="false"/>.
    /// </summary>
    public bool DisableTotalNameSuffixForCounters { get; set; }
}
