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
    /// Gets or sets a value indicating whether exemplar labels are emitted when
    /// using OpenMetrics. The default value: <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// When disabled, OpenMetrics exemplars are still exported but their
    /// label sets are emitted as empty, suppressing trace/span context and
    /// any tags filtered out of the metric label set.
    /// </remarks>
    public bool EnableExemplarLabels
    {
        get => this.ExporterOptions.EnableExemplarLabels;
        set => this.ExporterOptions.EnableExemplarLabels = value;
    }

    internal PrometheusExporterOptions ExporterOptions { get; } = new();
}
