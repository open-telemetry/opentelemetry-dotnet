// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter.Kafka;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods to simplify registering of the Kafka exporter.
/// </summary>
public static class KafkaMetricsExporterExtensions
{
    internal const string MetricExporterTemporalityPreferenceEnvVarKey = "OTEL_EXPORTER_KAFKA_METRICS_TEMPORALITY_PREFERENCE";

    /// <summary>
    /// Adds <see cref="KafkaMetricsExporter"/> to the <see cref="MeterProviderBuilder"/> using default options.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddKafkaExporter(this MeterProviderBuilder builder)
        => AddKafkaExporter(builder, name: null, configure: null);

    /// <summary>
    /// Adds <see cref="KafkaMetricsExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="KafkaExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddKafkaExporter(this MeterProviderBuilder builder, Action<KafkaExporterOptions> configure)
        => AddKafkaExporter(builder, name: null, configure);

    /// <summary>
    /// Adds <see cref="KafkaMetricsExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configure">Optional callback action for configuring <see cref="KafkaExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddKafkaExporter(
        this MeterProviderBuilder builder,
        string? name,
        Action<KafkaExporterOptions>? configure)
    {
        Guard.ThrowIfNull(builder);

        var finalOptionsName = name ?? Options.DefaultName;

        builder.ConfigureServices(services =>
        {
            if (name != null && configure != null)
            {
                // If we are using named options we register the
                // configuration delegate into options pipeline.
                services.Configure(finalOptionsName, configure);
            }

            KafkaExporterOptions.RegisterKafkaExporterOptionsFactory(services);

            services.AddOptions<MetricReaderOptions>(finalOptionsName).Configure<IConfiguration>(
                (readerOptions, config) =>
                {
                    var temporalityPreference = config[MetricExporterTemporalityPreferenceEnvVarKey];
                    if (!string.IsNullOrWhiteSpace(temporalityPreference)
                        && Enum.TryParse<MetricReaderTemporalityPreference>(temporalityPreference, ignoreCase: true, out var enumValue))
                    {
                        readerOptions.TemporalityPreference = enumValue;
                    }
                });
        });

        return builder.AddReader(sp =>
        {
            KafkaExporterOptions exporterOptions;

            if (name == null)
            {
                // If we are NOT using named options we create a new
                // instance always. The reason for this is
                // KafkaExporterOptions is shared by all signals. Without a
                // name, delegates for all signals will mix together. See:
                // https://github.com/open-telemetry/opentelemetry-dotnet/issues/4043
                exporterOptions = sp.GetRequiredService<IOptionsFactory<KafkaExporterOptions>>().Create(finalOptionsName);

                // Configuration delegate is executed inline on the fresh instance.
                configure?.Invoke(exporterOptions);
            }
            else
            {
                // When using named options we can properly utilize Options
                // API to create or reuse an instance.
                exporterOptions = sp.GetRequiredService<IOptionsMonitor<KafkaExporterOptions>>().Get(finalOptionsName);
            }

            return BuildKafkaExporterMetricReader(
                exporterOptions,
                sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(finalOptionsName),
                sp);
        });
    }

    /// <summary>
    /// Adds <see cref="KafkaMetricsExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="configureExporterAndMetricReader">Callback action for
    /// configuring <see cref="KafkaExporterOptions"/> and <see
    /// cref="MetricReaderOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddKafkaExporter(
        this MeterProviderBuilder builder,
        Action<KafkaExporterOptions, MetricReaderOptions> configureExporterAndMetricReader)
        => AddKafkaExporter(builder, name: null, configureExporterAndMetricReader);

    /// <summary>
    /// Adds <see cref="KafkaMetricsExporter"/> to the <see cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configureExporterAndMetricReader">Optional callback action
    /// for configuring <see cref="KafkaExporterOptions"/> and <see
    /// cref="MetricReaderOptions"/>.</param>
    /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
    public static MeterProviderBuilder AddKafkaExporter(
        this MeterProviderBuilder builder,
        string? name,
        Action<KafkaExporterOptions, MetricReaderOptions>? configureExporterAndMetricReader)
    {
        Guard.ThrowIfNull(builder);

        var finalOptionsName = name ?? Options.DefaultName;

        builder.ConfigureServices(services =>
        {
            KafkaExporterOptions.RegisterKafkaExporterOptionsFactory(services);

            services.AddOptions<MetricReaderOptions>(finalOptionsName).Configure<IConfiguration>(
                (readerOptions, config) =>
                {
                    var temporalityPreference = config[MetricExporterTemporalityPreferenceEnvVarKey];
                    if (!string.IsNullOrWhiteSpace(temporalityPreference)
                        && Enum.TryParse<MetricReaderTemporalityPreference>(temporalityPreference, ignoreCase: true, out var enumValue))
                    {
                        readerOptions.TemporalityPreference = enumValue;
                    }
                });
        });

        return builder.AddReader(sp =>
        {
            KafkaExporterOptions exporterOptions;
            if (name == null)
            {
                // If we are NOT using named options we create a new
                // instance always. The reason for this is
                // KafkaExporterOptions is shared by all signals. Without a
                // name, delegates for all signals will mix together.
                exporterOptions = sp.GetRequiredService<IOptionsFactory<KafkaExporterOptions>>().Create(finalOptionsName);
            }
            else
            {
                exporterOptions = sp.GetRequiredService<IOptionsMonitor<KafkaExporterOptions>>().Get(finalOptionsName);
            }

            var metricReaderOptions = sp.GetRequiredService<IOptionsMonitor<MetricReaderOptions>>().Get(finalOptionsName);

            configureExporterAndMetricReader?.Invoke(exporterOptions, metricReaderOptions);

            return BuildKafkaExporterMetricReader(exporterOptions, metricReaderOptions, sp);
        });
    }

    internal static MetricReader BuildKafkaExporterMetricReader(
        KafkaExporterOptions exporterOptions,
        MetricReaderOptions metricReaderOptions,
        IServiceProvider serviceProvider,
        Func<BaseExporter<Metric>, BaseExporter<Metric>>? configureExporterInstance = null)
    {
        BaseExporter<Metric> metricExporter = new KafkaMetricsExporter(exporterOptions);

        if (configureExporterInstance != null)
        {
            metricExporter = configureExporterInstance(metricExporter);
        }

        return PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(
            metricExporter,
            metricReaderOptions);
    }
}
