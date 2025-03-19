// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of the Console exporter.
/// </summary>
public static class ConsoleExporterMetricsExtensions
{
    private const int DefaultExportIntervalMilliseconds = 10000;
    private const int DefaultExportTimeoutMilliseconds = Timeout.Infinite;

    /// <summary>
    /// Adds <see cref="ConsoleMetricExporter"/> to the <see cref="MeterProviderBuilder"/> using default options.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddConsoleExporter(this MeterProviderBuilder builder)
        => AddConsoleExporter(builder, name: null, configureExporter: null);

    /// <summary>
    /// Adds <see cref="ConsoleMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="configureExporter">Callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddConsoleExporter(this MeterProviderBuilder builder, Action<ConsoleExporterOptions> configureExporter)
        => AddConsoleExporter(builder, name: null, configureExporter);

    /// <summary>
    /// Adds <see cref="ConsoleMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configureExporter">Optional callback action for configuring <see cref="ConsoleExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddConsoleExporter(
        this MeterProviderBuilder builder,
        string? name,
        Action<ConsoleExporterOptions>? configureExporter)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        if (configureExporter != null)
        {
            builder.ConfigureServices(services => services.Configure(name, configureExporter));
        }

        return builder.AddReader(sp =>
        {
            return BuildConsoleExporterMetricReader(
                sp.GetRequiredService<IOptionsMonitor<ConsoleExporterOptions>>().Get(name),
                sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(name));
        });
    }

    /// <summary>
    /// Adds <see cref="ConsoleMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="configureExporterAndMetricReader">Callback action for
    /// configuring <see cref="ConsoleExporterOptions"/> and <see
    /// cref="MetricReaderOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddConsoleExporter(
        this MeterProviderBuilder builder,
        Action<ConsoleExporterOptions, MetricReaderOptions>? configureExporterAndMetricReader)
        => AddConsoleExporter(builder, name: null, configureExporterAndMetricReader);

    /// <summary>
    /// Adds <see cref="ConsoleMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configureExporterAndMetricReader">Callback action for
    /// configuring <see cref="ConsoleExporterOptions"/> and <see
    /// cref="MetricReaderOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddConsoleExporter(
        this MeterProviderBuilder builder,
        string? name,
        Action<ConsoleExporterOptions, MetricReaderOptions>? configureExporterAndMetricReader)
    {
        Guard.ThrowIfNull(builder);

        name ??= Options.DefaultName;

        return builder.AddReader(sp =>
        {
            var exporterOptions = sp.GetRequiredService<IOptionsMonitor<ConsoleExporterOptions>>().Get(name);
            var metricReaderOptions = sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(name);

            configureExporterAndMetricReader?.Invoke(exporterOptions, metricReaderOptions);

            return BuildConsoleExporterMetricReader(exporterOptions, metricReaderOptions);
        });
    }

    private static MetricReader BuildConsoleExporterMetricReader(
        ConsoleExporterOptions exporterOptions,
        MetricReaderOptions metricReaderOptions)
    {
#pragma warning disable CA2000 // Dispose objects before losing scope
        var metricExporter = new ConsoleMetricExporter(exporterOptions);
#pragma warning restore CA2000 // Dispose objects before losing scope

        return PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(
            metricExporter,
            metricReaderOptions,
            DefaultExportIntervalMilliseconds,
            DefaultExportTimeoutMilliseconds);
    }
}
