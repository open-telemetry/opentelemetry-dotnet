// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter.Kafka;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of the Kafka exporter.
/// </summary>
public static class KafkaTraceExporterExtensions
{
    /// <summary>
    /// Adds Kafka exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKafkaExporter(this TracerProviderBuilder builder)
        => AddKafkaExporter(builder, name: null, configure: null);

    /// <summary>
    /// Adds Kafka exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="KafkaExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKafkaExporter(this TracerProviderBuilder builder, Action<KafkaExporterOptions> configure)
        => AddKafkaExporter(builder, name: null, configure);

    /// <summary>
    /// Adds Kafka exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="configure">Optional callback action for configuring <see cref="KafkaExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddKafkaExporter(
        this TracerProviderBuilder builder,
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
            services.RegisterOptionsFactory(configuration => new SdkLimitOptions(configuration));
        });

        return builder.AddProcessor(sp =>
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

            // Note: Not using finalOptionsName here for SdkLimitOptions.
            // There should only be one provider for a given service
            // collection so SdkLimitOptions is treated as a single default
            // instance.
            var sdkOptionsManager = sp.GetRequiredService<IOptionsMonitor<SdkLimitOptions>>().CurrentValue;

            return BuildKafkaExporterProcessor(exporterOptions, sdkOptionsManager, sp);
        });
    }

    internal static BaseProcessor<Activity> BuildKafkaExporterProcessor(
        KafkaExporterOptions exporterOptions,
        SdkLimitOptions sdkLimitOptions,
        IServiceProvider serviceProvider,
        Func<BaseExporter<Activity>, BaseExporter<Activity>>? configureExporterInstance = null)
    {
        BaseExporter<Activity> kafkaExporter = new KafkaTraceExporter(exporterOptions, sdkLimitOptions, null, null);

        if (configureExporterInstance != null)
        {
            kafkaExporter = configureExporterInstance(kafkaExporter);
        }

        if (exporterOptions.ExportProcessorType == ExportProcessorType.Simple)
        {
            return new SimpleActivityExportProcessor(kafkaExporter);
        }
        else
        {
            var batchOptions = exporterOptions.BatchExportProcessorOptions ?? new BatchExportActivityProcessorOptions();

            return new BatchActivityExportProcessor(
                kafkaExporter,
                batchOptions.MaxQueueSize,
                batchOptions.ScheduledDelayMilliseconds,
                batchOptions.ExporterTimeoutMilliseconds,
                batchOptions.MaxExportBatchSize);
        }
    }
}
