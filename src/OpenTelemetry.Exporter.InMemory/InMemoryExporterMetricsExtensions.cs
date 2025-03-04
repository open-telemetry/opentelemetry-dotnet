// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of the InMemory exporter.
/// </summary>
public static class InMemoryExporterMetricsExtensions
{
    private const int DefaultExportIntervalMilliseconds = Timeout.Infinite;
    private const int DefaultExportTimeoutMilliseconds = Timeout.Infinite;

    /// <summary>
    /// Adds InMemory metric exporter to the <see cref="MeterProviderBuilder"/> using default options.
    /// </summary>
    /// <remarks>
    /// Be aware that <see cref="Metric"/> may continue to be updated after export.
    /// </remarks>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="exportedItems">Collection which will be populated with the exported <see cref="Metric"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddInMemoryExporter(this MeterProviderBuilder builder, ICollection<Metric> exportedItems)
        => AddInMemoryExporter(builder, name: null, exportedItems, configureMetricReader: null);

    /// <summary>
    /// Adds InMemory metric exporter to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Be aware that <see cref="Metric"/> may continue to be updated after export.
    /// </remarks>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="exportedItems">Collection which will be populated with the exported <see cref="Metric"/>.</param>
    /// <param name="configureMetricReader">Callback action for configuring <see cref="MetricReaderOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddInMemoryExporter(
        this MeterProviderBuilder builder,
        ICollection<Metric> exportedItems,
        Action<MetricReaderOptions> configureMetricReader)
        => AddInMemoryExporter(builder, name: null, exportedItems, configureMetricReader);

    /// <summary>
    /// Adds InMemory metric exporter to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Be aware that <see cref="Metric"/> may continue to be updated after export.
    /// </remarks>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="exportedItems">Collection which will be populated with the exported <see cref="Metric"/>.</param>
    /// <param name="configureMetricReader">Optional callback action for configuring <see cref="MetricReaderOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddInMemoryExporter(
        this MeterProviderBuilder builder,
        string? name,
        ICollection<Metric> exportedItems,
        Action<MetricReaderOptions>? configureMetricReader)
    {
        Guard.ThrowIfNull(builder);
        Guard.ThrowIfNull(exportedItems);

        name ??= Options.DefaultName;

        if (configureMetricReader != null)
        {
            builder.ConfigureServices(services => services.Configure(name, configureMetricReader));
        }

        return builder.AddReader(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(name);

            return BuildInMemoryExporterMetricReader(exportedItems, options);
        });
    }

    /// <summary>
    /// Adds InMemory metric exporter to the <see cref="MeterProviderBuilder"/> using default options.
    /// The exporter will be setup to export <see cref="MetricSnapshot"/>.
    /// </summary>
    /// <remarks>
    /// Use this if you need a copy of <see cref="Metric"/> that will not be updated after export.
    /// </remarks>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="exportedItems">Collection which will be populated with the exported <see cref="Metric"/> represented as <see cref="MetricSnapshot"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddInMemoryExporter(
        this MeterProviderBuilder builder,
        ICollection<MetricSnapshot> exportedItems)
        => AddInMemoryExporter(builder, name: null, exportedItems, configureMetricReader: null);

    /// <summary>
    /// Adds InMemory metric exporter to the <see cref="MeterProviderBuilder"/>.
    /// The exporter will be setup to export <see cref="MetricSnapshot"/>.
    /// </summary>
    /// <remarks>
    /// Use this if you need a copy of <see cref="Metric"/> that will not be updated after export.
    /// </remarks>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="exportedItems">Collection which will be populated with the exported <see cref="Metric"/> represented as <see cref="MetricSnapshot"/>.</param>
    /// <param name="configureMetricReader">Callback action for configuring <see cref="MetricReaderOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddInMemoryExporter(
        this MeterProviderBuilder builder,
        ICollection<MetricSnapshot> exportedItems,
        Action<MetricReaderOptions> configureMetricReader)
        => AddInMemoryExporter(builder, name: null, exportedItems, configureMetricReader);

    /// <summary>
    /// Adds InMemory metric exporter to the <see cref="MeterProviderBuilder"/>.
    /// The exporter will be setup to export <see cref="MetricSnapshot"/>.
    /// </summary>
    /// <remarks>
    /// Use this if you need a copy of <see cref="Metric"/> that will not be updated after export.
    /// </remarks>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="exportedItems">Collection which will be populated with the exported <see cref="Metric"/> represented as <see cref="MetricSnapshot"/>.</param>
    /// <param name="configureMetricReader">Optional callback action for configuring <see cref="MetricReaderOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddInMemoryExporter(
        this MeterProviderBuilder builder,
        string? name,
        ICollection<MetricSnapshot> exportedItems,
        Action<MetricReaderOptions>? configureMetricReader)
    {
        Guard.ThrowIfNull(builder);
        Guard.ThrowIfNull(exportedItems);

        name ??= Options.DefaultName;

        if (configureMetricReader != null)
        {
            builder.ConfigureServices(services => services.Configure(name, configureMetricReader));
        }

        return builder.AddReader(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(name);

            return BuildInMemoryExporterMetricReader(exportedItems, options);
        });
    }

    private static PeriodicExportingMetricReader BuildInMemoryExporterMetricReader(
        ICollection<Metric> exportedItems,
        MetricReaderOptions metricReaderOptions)
    {
#pragma warning disable CA2000
        var metricExporter = new InMemoryExporter<Metric>(exportedItems);
#pragma warning restore CA2000

        return PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(
            metricExporter,
            metricReaderOptions,
            DefaultExportIntervalMilliseconds,
            DefaultExportTimeoutMilliseconds);
    }

    private static PeriodicExportingMetricReader BuildInMemoryExporterMetricReader(
        ICollection<MetricSnapshot> exportedItems,
        MetricReaderOptions metricReaderOptions)
    {
#pragma warning disable CA2000
        var metricExporter = new InMemoryExporter<Metric>(
            exportFunc: (in Batch<Metric> metricBatch) => ExportMetricSnapshot(in metricBatch, exportedItems));
#pragma warning restore CA2000

        return PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(
            metricExporter,
            metricReaderOptions,
            DefaultExportIntervalMilliseconds,
            DefaultExportTimeoutMilliseconds);
    }

    private static ExportResult ExportMetricSnapshot(in Batch<Metric> batch, ICollection<MetricSnapshot> exportedItems)
    {
        if (exportedItems == null)
        {
            return ExportResult.Failure;
        }

        foreach (var metric in batch)
        {
            exportedItems.Add(new MetricSnapshot(metric));
        }

        return ExportResult.Success;
    }
}
