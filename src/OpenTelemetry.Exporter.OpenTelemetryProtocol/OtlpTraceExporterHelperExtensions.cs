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

using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace
{
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

            name ??= Options.DefaultName;

            if (configure != null)
            {
                builder.ConfigureServices(services => services.Configure(name, configure));
            }

            return builder.ConfigureBuilder((sp, builder) =>
            {
                AddOtlpExporter(
                    builder,
                    sp.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get(name),
                    sp.GetRequiredService<IOptionsMonitor<ExportActivityProcessorOptions>>().Get(name),
                    sp);
            });
        }

        /// <summary>
        /// Adds OpenTelemetry Protocol (OTLP) exporter to the TracerProvider.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
        /// <param name="configureExporterAndProcessor">Callback action for configuring <see cref="OtlpExporterOptions"/> and <see cref="ExportActivityProcessorOptions"/>.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddOtlpExporter(this TracerProviderBuilder builder, Action<OtlpExporterOptions, ExportActivityProcessorOptions> configureExporterAndProcessor)
            => AddOtlpExporter(builder, name: null, configureExporterAndProcessor);

        /// <summary>
        /// Adds OpenTelemetry Protocol (OTLP) exporter to the TracerProvider.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
        /// <param name="configureExporterAndProcessor">Callback action for configuring <see cref="OtlpExporterOptions"/> and <see cref="ExportActivityProcessorOptions"/>.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddOtlpExporter(this TracerProviderBuilder builder, string name, Action<OtlpExporterOptions, ExportActivityProcessorOptions> configureExporterAndProcessor)
        {
            Guard.ThrowIfNull(builder);

            name ??= Options.DefaultName;

            return builder.ConfigureBuilder((sp, builder) =>
            {
                var exporterOptions = sp.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get(name);
                var processorOptions = sp.GetRequiredService<IOptionsMonitor<ExportActivityProcessorOptions>>().Get(name);

                configureExporterAndProcessor?.Invoke(exporterOptions, processorOptions);

                AddOtlpExporter(builder, exporterOptions, processorOptions, sp);
            });
        }

        internal static TracerProviderBuilder AddOtlpExporter(
            TracerProviderBuilder builder,
            OtlpExporterOptions exporterOptions,
            ExportActivityProcessorOptions processorOptions,
            IServiceProvider serviceProvider,
            Func<BaseExporter<Activity>, BaseExporter<Activity>> configureExporterInstance = null)
        {
            exporterOptions.TryEnableIHttpClientFactoryIntegration(serviceProvider, "OtlpTraceExporter");

            BaseExporter<Activity> otlpExporter = new OtlpTraceExporter(exporterOptions);

            if (configureExporterInstance != null)
            {
                otlpExporter = configureExporterInstance(otlpExporter);
            }

            processorOptions = CoalesceProcessorOptions(exporterOptions, processorOptions);

            if (processorOptions.ExportProcessorType == ExportProcessorType.Simple)
            {
                return builder.AddProcessor(new SimpleActivityExportProcessor(otlpExporter));
            }
            else
            {
                return builder.AddProcessor(new BatchActivityExportProcessor(
                    otlpExporter,
                    processorOptions.BatchExportProcessorOptions.MaxQueueSize,
                    processorOptions.BatchExportProcessorOptions.ScheduledDelayMilliseconds,
                    processorOptions.BatchExportProcessorOptions.ExporterTimeoutMilliseconds,
                    processorOptions.BatchExportProcessorOptions.MaxExportBatchSize));
            }
        }

        private static ExportActivityProcessorOptions CoalesceProcessorOptions(OtlpExporterOptions exporterOptions, ExportActivityProcessorOptions processorOptions)
        {
#pragma warning disable CS0618 // Using obsolete members
            var defaultBatchOptions = new BatchExportActivityProcessorOptions();
            var actualBatchOptions = exporterOptions.BatchExportProcessorOptions;

            if (exporterOptions.ExportProcessorType == ExportProcessorType.Simple
                || defaultBatchOptions.ExporterTimeoutMilliseconds != actualBatchOptions.ExporterTimeoutMilliseconds
                || defaultBatchOptions.MaxExportBatchSize != actualBatchOptions.MaxExportBatchSize
                || defaultBatchOptions.MaxQueueSize != actualBatchOptions.MaxQueueSize
                || defaultBatchOptions.ScheduledDelayMilliseconds != actualBatchOptions.ScheduledDelayMilliseconds)
            {
                processorOptions = new ExportActivityProcessorOptions();
                processorOptions.ExportProcessorType = exporterOptions.ExportProcessorType;
                processorOptions.BatchExportProcessorOptions = new BatchExportActivityProcessorOptions
                {
                    ExporterTimeoutMilliseconds = exporterOptions.BatchExportProcessorOptions.ExporterTimeoutMilliseconds,
                    MaxExportBatchSize = exporterOptions.BatchExportProcessorOptions.MaxExportBatchSize,
                    MaxQueueSize = exporterOptions.BatchExportProcessorOptions.MaxQueueSize,
                    ScheduledDelayMilliseconds = exporterOptions.BatchExportProcessorOptions.ScheduledDelayMilliseconds,
                };
            }

            return processorOptions;
#pragma warning restore CS0618 // Using obsolete members
        }
    }
}
