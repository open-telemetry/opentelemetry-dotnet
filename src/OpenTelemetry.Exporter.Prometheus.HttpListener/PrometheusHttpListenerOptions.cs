// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter;

/// <summary>
/// <see cref="PrometheusHttpListener"/> options.
/// </summary>
public class PrometheusHttpListenerOptions
{
    internal const string DefaultScrapeEndpointPath = "/metrics";

    private IReadOnlyCollection<string> uriPrefixes = ["http://localhost:9464/"];

    /// <summary>
    /// Gets or sets the path to use for the scraping endpoint. Default value: "/metrics".
    /// </summary>
    public string? ScrapeEndpointPath { get; set; } = DefaultScrapeEndpointPath;

    /// <summary>
    /// Gets or sets a value indicating whether addition of _total suffix for counter metric names is disabled. Default value: <see langword="false"/>.
    /// </summary>
    public bool DisableTotalNameSuffixForCounters { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether timestamps should be disabled. Default value: <see langword="false"/>.
    /// </summary>
    public bool DisableTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the URI (Uniform Resource Identifier) prefixes to use for the http listener.
    /// Default value: <c>["http://localhost:9464/"]</c>.
    /// </summary>
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
        }
    }
}
