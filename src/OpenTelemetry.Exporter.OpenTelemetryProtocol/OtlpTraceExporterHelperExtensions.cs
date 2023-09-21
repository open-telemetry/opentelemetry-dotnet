// <copyright file="OtlpTraceExporterHelperExtensions.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.ExporterOpenTelemetryProtocol.Implementation.Retry;
using OpenTelemetry.Internal;
using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods to simplify registering of the OpenTelemetry Protocol (OTLP) exporter.
/// </summary>
public static class OtlpTraceExporterHelperExtensions
{
    /// <summary>
    /// Adds OpenTelemetry Protocol (OTLP) exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddOtlpExporter(this TracerProviderBuilder builder)
        => AddOtlpExporter(builder, name: null, configure: null);

    /// <summary>
    /// Adds OpenTelemetry Protocol (OTLP) exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddOtlpExporter(this TracerProviderBuilder builder, Action<OtlpExporterOptions> configure)
        => AddOtlpExporter(builder, name: null, configure);

    /// <summary>
    /// Adds OpenTelemetry Protocol (OTLP) exporter to the TracerProvider.
    /// </summary>
    /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
    /// <param name="name">Name which is used when retrieving options.</param>
    /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
    public static TracerProviderBuilder AddOtlpExporter(
        this TracerProviderBuilder builder,
        string name,
        Action<OtlpExporterOptions> configure)
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

            OtlpExporterOptions.RegisterOtlpExporterOptionsFactory(services);
            services.RegisterOptionsFactory(configuration => new SdkLimitOptions(configuration));
            services.TryAddSingleton<OtlpExporterTransmissionHandler<OtlpCollector.ExportTraceServiceRequest>>();
        });

        return builder.AddProcessor(sp =>
        {
            OtlpExporterOptions exporterOptions;

            if (name == null)
            {
                // If we are NOT using named options we create a new
                // instance always. The reason for this is
                // OtlpExporterOptions is shared by all signals. Without a
                // name, delegates for all signals will mix together. See:
                // https://github.com/open-telemetry/opentelemetry-dotnet/issues/4043
                exporterOptions = sp.GetRequiredService<IOptionsFactory<OtlpExporterOptions>>().Create(finalOptionsName);

                // Configuration delegate is executed inline on the fresh instance.
                configure?.Invoke(exporterOptions);
            }
            else
            {
                // When using named options we can properly utilize Options
                // API to create or reuse an instance.
                exporterOptions = sp.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get(finalOptionsName);
            }

            // Note: Not using finalOptionsName here for SdkLimitOptions.
            // There should only be one provider for a given service
            // collection so SdkLimitOptions is treated as a single default
            // instance.
            var sdkOptionsManager = sp.GetRequiredService<IOptionsMonitor<SdkLimitOptions>>().CurrentValue;

            return BuildOtlpExporterProcessor(exporterOptions, sdkOptionsManager, sp);
        });
    }

    internal static BaseProcessor<Activity> BuildOtlpExporterProcessor(
        OtlpExporterOptions exporterOptions,
        SdkLimitOptions sdkLimitOptions,
        IServiceProvider serviceProvider,
        Func<BaseExporter<Activity>, BaseExporter<Activity>> configureExporterInstance = null)
    {
        exporterOptions.TryEnableIHttpClientFactoryIntegration(serviceProvider, "OtlpTraceExporter");

        BaseExporter<Activity> otlpExporter = new OtlpTraceExporter(
            exporterOptions,
            sdkLimitOptions,
            transmissionHandler: serviceProvider.GetRequiredService<OtlpExporterTransmissionHandler<OtlpCollector.ExportTraceServiceRequest>>());

        if (configureExporterInstance != null)
        {
            otlpExporter = configureExporterInstance(otlpExporter);
        }

        if (exporterOptions.ExportProcessorType == ExportProcessorType.Simple)
        {
            return new SimpleActivityExportProcessor(otlpExporter);
        }
        else
        {
            var batchOptions = exporterOptions.BatchExportProcessorOptions ?? new BatchExportActivityProcessorOptions();

            return new BatchActivityExportProcessor(
                otlpExporter,
                batchOptions.MaxQueueSize,
                batchOptions.ScheduledDelayMilliseconds,
                batchOptions.ExporterTimeoutMilliseconds,
                batchOptions.MaxExportBatchSize);
        }
    }
}
