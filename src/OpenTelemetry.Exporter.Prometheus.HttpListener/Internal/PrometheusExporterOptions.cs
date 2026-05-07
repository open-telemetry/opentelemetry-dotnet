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
        this.ScrapeResponseCacheDurationMilliseconds = 300;
    }

    /// <summary>
    /// Gets or sets the cache duration in milliseconds for scrape responses. Default value: 300.
    /// </summary>
    /// <remarks>
    /// Note: Specify 0 to disable response caching.
    /// </remarks>
    public int ScrapeResponseCacheDurationMilliseconds
    {
        get => field;
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
