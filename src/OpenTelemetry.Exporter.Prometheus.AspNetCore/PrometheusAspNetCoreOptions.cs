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
    public string ScrapeEndpointPath { get; set; } = DefaultScrapeEndpointPath;

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

    internal PrometheusExporterOptions ExporterOptions { get; } = new();
}
