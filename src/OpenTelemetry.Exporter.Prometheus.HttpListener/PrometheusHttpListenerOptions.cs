// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter.Prometheus;
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

    internal const string PrometheusHostEnvVar = "OTEL_EXPORTER_PROMETHEUS_HOST";
    internal const string PrometheusPortEnvVar = "OTEL_EXPORTER_PROMETHEUS_PORT";

    /// <summary>
    /// Initializes a new instance of the <see cref="PrometheusHttpListenerOptions"/> class.
    /// </summary>
    public PrometheusHttpListenerOptions()
         : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
    {
    }

    internal PrometheusHttpListenerOptions(IConfiguration configuration)
    {
        if (!configuration.TryGetStringValue(PrometheusHostEnvVar, out var host))
        {
            host = "localhost";
        }

        if (!configuration.TryGetIntValue(PrometheusExporterEventSource.Log, PrometheusPortEnvVar, out var port))
        {
            port = 9464;
        }
        else if (port is <= 0 or > ushort.MaxValue)
        {
            PrometheusExporterEventSource.Log.LogInvalidConfigurationValue(PrometheusPortEnvVar, port.ToString(CultureInfo.InvariantCulture));
            port = 9464;
        }

        this.Host = host;
        this.Port = port;
        this.ScrapeResponseCacheDurationMilliseconds = 300;
    }

    /// <summary>
    /// Gets or sets the Host name the HTTP listener will bind to. Defaults to <c>localhost</c>.
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Gets or sets the TCP port used by the HTTP listener. Defaults to <c>9464</c>.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Gets or sets the path to use for the scraping endpoint. Default value: "/metrics".
    /// </summary>
    public string? ScrapeEndpointPath { get; set; } = DefaultScrapeEndpointPath;

    /// <summary>
    /// Gets or sets a value indicating whether addition of _total suffix for counter metric names is disabled. Default value: <see langword="false"/>.
    /// </summary>
    public bool DisableTotalNameSuffixForCounters { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether exemplar labels are emitted when
    /// using OpenMetrics. The default value: <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// When disabled, OpenMetrics exemplars are still exported but their
    /// label sets are emitted as empty, suppressing trace/span context and
    /// any tags filtered out of the metric label set.
    /// </remarks>
    public bool EnableExemplarLabels { get; set; }

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
        get => field ?? ["http://localhost:9464/"];
        set
        {
            Guard.ThrowIfNull(value);
            if (value.Count == 0)
            {
                throw new ArgumentException("Empty list provided.", nameof(value));
            }

            field = value;
            this.UriPrefixesExplicitlySet = true;
        }
    }

    internal bool UriPrefixesExplicitlySet { get; private set; }
}
