// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Prometheus;

/// <summary>
/// Prometheus exporter options.
/// </summary>
internal sealed class PrometheusExporterOptions
{
    private int scrapeResponseCacheDurationMilliseconds = 300;

    /// <summary>
    /// Gets or sets the cache duration in milliseconds for scrape responses. Default value: 300.
    /// </summary>
    /// <remarks>
    /// Note: Specify 0 to disable response caching.
    /// </remarks>
    public int ScrapeResponseCacheDurationMilliseconds
    {
        get => this.scrapeResponseCacheDurationMilliseconds;
        set
        {
            Guard.ThrowIfOutOfRange(value, min: 0);

            this.scrapeResponseCacheDurationMilliseconds = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to export scope info. Default value: true.
    /// </summary>
    public bool ScopeInfoEnabled { get; set; } = true;
}
