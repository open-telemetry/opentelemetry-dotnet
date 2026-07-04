// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.Prometheus;

/// <summary>
/// Prometheus exporter options.
/// </summary>
internal sealed class PrometheusExporterOptions
{
    /// <summary>
    /// The initial scrape response buffer size in bytes.
    /// The buffer is always allocated at this size, so it is also the
    /// smallest meaningful value for <see cref="MaxScrapeResponseSizeBytes"/>.
    /// </summary>
    public const int InitialScrapeResponseSizeBytes = 85_000; // Encourage the object to live in the Large Object Heap (LOH).

    /// <summary>
    /// The default maximum scrape response size in bytes (~166 MiB).
    /// </summary>
    /// <remarks>
    /// The response buffer starts at <see cref="InitialScrapeResponseSizeBytes"/>
    /// and grows on demand by doubling. This default is equal to that size
    /// multiplied by <c>2^11</c>, i.e. the largest size reachable by that doubling
    /// sequence, so the whole budget is usable and none is left as an unreachable
    /// remainder.
    /// </remarks>
    public const int DefaultMaxScrapeResponseSizeBytes = InitialScrapeResponseSizeBytes * 2048;

    public PrometheusExporterOptions()
    {
        this.ScopeInfoEnabled = true;
        this.ScrapeResponseCacheDurationMilliseconds = 300;
        this.TargetInfoEnabled = true;
        this.MaxScrapeResponseSizeBytes = DefaultMaxScrapeResponseSizeBytes;
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
    /// Gets or sets a value indicating whether to include a <c>target_info</c> metric in the scrape response.
    /// Default value: <see langword="true"/>.
    /// </summary>
    public bool TargetInfoEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether addition of _total suffix for counter metric names is disabled. Default value: <see langword="false"/>.
    /// </summary>
    public bool DisableTotalNameSuffixForCounters { get; set; }

    /// <summary>
    /// Gets or sets the maximum size in bytes that a single scrape response is
    /// allowed to grow to. Default value: <see cref="DefaultMaxScrapeResponseSizeBytes"/> (~166 MiB).
    /// </summary>
    /// <remarks>
    /// Increase this value when exposing a very large number of time series.
    /// </remarks>
    public int MaxScrapeResponseSizeBytes
    {
        get;
        set
        {
            Guard.ThrowIfOutOfRange(value, min: InitialScrapeResponseSizeBytes);
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets a predicate used to select which resource attributes are added to each metric as constant labels.
    /// The predicate is invoked with the resource attribute key and should return <see langword="true"/> to include the
    /// attribute. Default value: <see langword="null"/> (no resource attributes are added as metric labels).
    /// </summary>
    public Func<string, bool>? ResourceConstantLabels { get; set; }
}
