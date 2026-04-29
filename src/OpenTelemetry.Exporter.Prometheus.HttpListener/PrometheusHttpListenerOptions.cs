// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter;

/// <summary>
/// <see cref="PrometheusHttpListener"/> options.
/// </summary>
public class PrometheusHttpListenerOptions
{
    /// <summary>
    /// Default path for Prometheus scrapes.
    /// </summary>
    internal const string DefaultScrapeEndpointPath = "/metrics";

    private IReadOnlyCollection<string> uriPrefixes = ["http://localhost:9464/"];

    /// <summary>
    /// Initializes a new instance of the <see cref="PrometheusHttpListenerOptions"/> class.
    /// </summary>
    public PrometheusHttpListenerOptions()
    {
        this.ScrapeResponseCacheDurationMilliseconds = 300;
    }

    /// <summary>
    /// Gets or sets the Host name the HTTP listener will bind to. Defaults to <c>localhost</c>.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// Gets or sets the TCP port used by the HTTP listener. Defaults to <c>9464</c>.
    /// </summary>
    public int Port { get; set; } = 9464;

    /// <summary>
    /// Gets or sets the path to use for the scraping endpoint. Default value: "/metrics".
    /// </summary>
    public string? ScrapeEndpointPath { get; set; } = DefaultScrapeEndpointPath;

    /// <summary>
    /// Gets or sets a value indicating whether addition of _total suffix for counter metric names is disabled. Default value: <see langword="false"/>.
    /// </summary>
    public bool DisableTotalNameSuffixForCounters { get; set; }

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
    /// Gets or sets the URI (Uniform Resource Identifier) prefixes to use for the http listener.
    /// Default value: <c>["http://localhost:9464/"]</c>.
    /// </summary>
    [Obsolete("UriPrefixes is deprecated. Use Host and Port. This will be removed in a future stable release.")]
    public IReadOnlyCollection<string> UriPrefixes
    {
        get => this.uriPrefixes;
        set
        {
            Guard.ThrowIfNull(value);
            if (value.Count == 0)
            {
                throw new ArgumentException("Empty list provided.", nameof(this.UriPrefixes));
            }

            this.uriPrefixes = value;
            this.UriPrefixesExplicitlySet = true;
        }
    }

    internal bool UriPrefixesExplicitlySet { get; private set; }
}
