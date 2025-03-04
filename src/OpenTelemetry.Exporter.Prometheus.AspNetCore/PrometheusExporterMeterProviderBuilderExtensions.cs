// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering a PrometheusExporter.
/// </summary>
public static class PrometheusExporterMeterProviderBuilderExtensions
{
    /// <summary>
    /// Adds <see cref="PrometheusExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddPrometheusExporter(this MeterProviderBuilder builder)
        => AddPrometheusExporter(builder, name: null, configure: null);

    /// <summary>
    /// Adds <see cref="PrometheusExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="PrometheusAspNetCoreOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddPrometheusExporter(
        this MeterProviderBuilder builder,
        Action<PrometheusAspNetCoreOptions> configure)
        => AddPrometheusExporter(builder, name: null, configure);

    /// <summary>
    /// Adds <see cref="PrometheusExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configure">Optional callback action for configuring <see cref="PrometheusAspNetCoreOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddPrometheusExporter(
        this MeterProviderBuilder builder,
        string? name,
        Action<PrometheusAspNetCoreOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        if (configure != null)
        {
            builder.ConfigureServices(services => services.Configure(name, configure));
        }

        return builder.AddReader(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<PrometheusAspNetCoreOptions>>().Get(name);

            return BuildPrometheusExporterMetricReader(options);
        });
    }

    private static BaseExportingMetricReader BuildPrometheusExporterMetricReader(PrometheusAspNetCoreOptions options)
    {
#pragma warning disable CA2000
        var exporter = new PrometheusExporter(options.ExporterOptions);
#pragma warning restore CA2000

        return new BaseExportingMetricReader(exporter)
        {
            TemporalityPreference = MetricReaderTemporalityPreference.Cumulative,
        };
    }
}
