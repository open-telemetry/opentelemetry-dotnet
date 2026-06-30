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
    /// Gets or sets a value indicating whether the scope information (name, version, schema URL) is added to the scrape response.
    /// Default value: <see langword="true"/>.
    /// </summary>
    public bool ScopeInfoEnabled { get; set; } = true;

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
    public bool TargetInfoEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets a predicate used to select which resource attributes are added to each metric as constant labels.
    /// The predicate is invoked with the resource attribute key and should return <see langword="true"/> to include the
    /// attribute. Default value: <see langword="null"/> (no resource attributes are added as metric labels).
    /// </summary>
    /// <remarks>
    /// Note: Resource attributes copied as metric labels are always included in the <c>target_info</c> metric
    /// regardless of this predicate.
    /// </remarks>
    public Func<string, bool>? ResourceConstantLabels { get; set; }

    /// <summary>
    /// Gets or sets an optional callback to apply custom configuration for the
    /// <see cref="System.Net.HttpListener"/> instance used by the exporter.
    /// </summary>
    /// <remarks>
    /// This callback is invoked after an <see cref="System.Net.HttpListener"/>
    /// instance has been created and other configuration options have already
    /// been applied to it.
    /// </remarks>
    public Action<PrometheusHttpListenerOptions, System.Net.HttpListener>? ConfigureHttpListener { get; set; }
}
