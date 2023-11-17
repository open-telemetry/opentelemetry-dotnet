// <copyright file="PrometheusHttpListenerMeterProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering a PrometheusHttpListener.
/// </summary>
public static class PrometheusHttpListenerMeterProviderBuilderExtensions
{
    /// <summary>
    /// Adds PrometheusHttpListener to MeterProviderBuilder.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/>builder to use.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/>to chain calls.</returns>
    public static MeterProviderBuilder AddPrometheusHttpListener(this MeterProviderBuilder builder)
        => AddPrometheusHttpListener(builder, name: null, configure: null);

    /// <summary>
    /// Adds PrometheusHttpListener to MeterProviderBuilder.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/>builder to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="PrometheusHttpListenerOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/>to chain calls.</returns>
    public static MeterProviderBuilder AddPrometheusHttpListener(
        this MeterProviderBuilder builder,
        Action<PrometheusHttpListenerOptions> configure)
        => AddPrometheusHttpListener(builder, name: null, configure);

    /// <summary>
    /// Adds PrometheusHttpListener to MeterProviderBuilder.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/>builder to use.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configure">Callback action for configuring <see cref="PrometheusHttpListenerOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/>to chain calls.</returns>
    public static MeterProviderBuilder AddPrometheusHttpListener(
        this MeterProviderBuilder builder,
        string name,
        Action<PrometheusHttpListenerOptions> configure)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        if (configure != null)
        {
            builder.ConfigureServices(services => services.Configure(name, configure));
        }

        return builder.AddReader(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<PrometheusHttpListenerOptions>>().Get(name);

            return BuildPrometheusHttpListenerMetricReader(options);
        });
    }

    private static MetricReader BuildPrometheusHttpListenerMetricReader(
        PrometheusHttpListenerOptions options)
    {
        var exporter = new PrometheusExporter(new PrometheusExporterOptions { ScrapeResponseCacheDurationMilliseconds = 0 });

        var reader = new BaseExportingMetricReader(exporter)
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Cumulative,
        };

        try
        {
            var listener = new PrometheusHttpListener(exporter, options);
            exporter.OnDispose = () => listener.Dispose();
            listener.Start();
        }
        catch (Exception ex)
        {
            try
            {
                reader.Dispose();
            }
            catch
            {
            }

            throw new InvalidOperationException("PrometheusExporter HttpListener could not be started.", ex);
        }

        return reader;
    }
}
