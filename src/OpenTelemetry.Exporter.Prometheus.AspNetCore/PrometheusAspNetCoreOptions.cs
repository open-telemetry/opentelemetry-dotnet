// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Prometheus;

namespace OpenTelemetry.Exporter;

/// <summary>
/// Prometheus exporter options.
/// </summary>
public class PrometheusAspNetCoreOptions
{
    internal const string DefaultScrapeEndpointPath = "/metrics";

    /// <summary>
    /// Gets or sets the path to use for the scraping endpoint. Default value: "/metrics".
    /// </summary>
    public string? ScrapeEndpointPath { get; set; } = DefaultScrapeEndpointPath;

    /// <summary>
    /// Gets or sets a value indicating whether addition of _total suffix for counter metric names is disabled. Default value: <see langword="false"/>.
    /// </summary>
    public bool DisableTotalNameSuffixForCounters
    {
        get => this.ExporterOptions.DisableTotalNameSuffixForCounters;
        set => this.ExporterOptions.DisableTotalNameSuffixForCounters = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the scope information (name, version, schema URL) is added to the scrape response.
    /// Default value: <see langword="true"/>.
    /// </summary>
    public bool ScopeInfoEnabled
    {
        get => this.ExporterOptions.ScopeInfoEnabled;
        set => this.ExporterOptions.ScopeInfoEnabled = value;
    }

    /// <summary>
    /// Gets or sets the cache duration in milliseconds for scrape responses. Default value: 300.
    /// </summary>
    /// <remarks>
    /// Note: Specify 0 to disable response caching.
    /// </remarks>
    public int ScrapeResponseCacheDurationMilliseconds
    {
        get => this.ExporterOptions.ScrapeResponseCacheDurationMilliseconds;
        set => this.ExporterOptions.ScrapeResponseCacheDurationMilliseconds = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether to include a <c>target_info</c> metric in the scrape response.
    /// Default value: <see langword="true"/>.
    /// </summary>
    public bool TargetInfoEnabled
    {
        get => this.ExporterOptions.TargetInfoEnabled;
        set => this.ExporterOptions.TargetInfoEnabled = value;
    }

    internal PrometheusExporterOptions ExporterOptions { get; } = new();
}
