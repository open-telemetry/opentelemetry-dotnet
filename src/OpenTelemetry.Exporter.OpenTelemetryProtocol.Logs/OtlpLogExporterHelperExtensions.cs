// <copyright file="OtlpLogExporterHelperExtensions.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Extension methods to simplify registering of the OpenTelemetry Protocol (OTLP) exporter.
    /// </summary>
    public static class OtlpLogExporterHelperExtensions
    {
        /// <summary>
        /// Adds OTLP exporter to the OpenTelemetryLoggerOptions.
        /// </summary>
        /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/>.</param>
        /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
        [Obsolete("Call the AddOtlpExporter extension using LoggerProviderBuilder instead this method will be removed in a future version.")]
        public static OpenTelemetryLoggerOptions AddOtlpExporter(this OpenTelemetryLoggerOptions loggerOptions)
            => AddOtlpExporter(loggerOptions, configure: null);

        /// <summary>
        /// Adds OTLP exporter to the OpenTelemetryLoggerOptions.
        /// </summary>
        /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/>.</param>
        /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
        /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
        [Obsolete("Call the AddOtlpExporter extension using LoggerProviderBuilder instead this method will be removed in a future version.")]
        public static OpenTelemetryLoggerOptions AddOtlpExporter(
            this OpenTelemetryLoggerOptions loggerOptions,
            Action<OtlpExporterOptions> configure)
        {
            var exporterOptions = new OtlpExporterOptions();
            configure?.Invoke(exporterOptions);

            var otlpExporter = new OtlpLogExporter(exporterOptions);

            if (exporterOptions.ExportProcessorType == ExportProcessorType.Simple)
            {
                return loggerOptions.AddProcessor(new SimpleLogRecordExportProcessor(otlpExporter));
            }
            else
            {
                return loggerOptions.AddProcessor(new BatchLogRecordExportProcessor(
                    otlpExporter,
                    exporterOptions.BatchExportProcessorOptions.MaxQueueSize,
                    exporterOptions.BatchExportProcessorOptions.ScheduledDelayMilliseconds,
                    exporterOptions.BatchExportProcessorOptions.ExporterTimeoutMilliseconds,
                    exporterOptions.BatchExportProcessorOptions.MaxExportBatchSize));
            }
        }

        /// <summary>
        /// Adds OTLP exporter to the LoggerProviderBuilder.
        /// </summary>
        /// <param name="builder"><see cref="LoggerProviderBuilder"/>.</param>
        /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
        public static LoggerProviderBuilder AddOtlpExporter(this LoggerProviderBuilder builder)
            => AddOtlpExporter(builder, name: null, configure: null);

        /// <summary>
        /// Adds OTLP exporter to the LoggerProviderBuilder.
        /// </summary>
        /// <param name="builder"><see cref="LoggerProviderBuilder"/>.</param>
        /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
        /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
        public static LoggerProviderBuilder AddOtlpExporter(
            this LoggerProviderBuilder builder,
            Action<OtlpExporterOptions> configure)
            => AddOtlpExporter(builder, name: null, configure);

        /// <summary>
        /// Adds OTLP exporter to the LoggerProviderBuilder.
        /// </summary>
        /// <param name="builder"><see cref="LoggerProviderBuilder"/>.</param>
        /// <param name="name">Name which is used when retrieving options.</param>
        /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
        /// <returns>The instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
        public static LoggerProviderBuilder AddOtlpExporter(
            this LoggerProviderBuilder builder,
            string name,
            Action<OtlpExporterOptions> configure)
        {
            Guard.ThrowIfNull(builder);

            name ??= Options.DefaultName;

            builder.ConfigureServices(services =>
            {
                if (configure != null)
                {
                    services.Configure(name, configure);
                }

                services.RegisterOptionsFactory(configuration => new SdkLimitOptions(configuration));
                services.RegisterOptionsFactory(configuration => new OtlpExporterOptions(configuration));
            });

            builder.ConfigureBuilder((sp, builder) =>
            {
                var exporterOptions = sp.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get(name);

                // Note: Not using name here for SdkLimitOptions. There should
                // only be one provider for a given service collection so
                // SdkLimitOptions is treated as a single default instance.
                var sdkLimitOptions = sp.GetRequiredService<IOptionsMonitor<SdkLimitOptions>>().CurrentValue;

                AddOtlpExporter(builder, exporterOptions, sdkLimitOptions, sp);
            });

            return builder;
        }

        private static void AddOtlpExporter(
            LoggerProviderBuilder builder,
            OtlpExporterOptions exporterOptions,
            SdkLimitOptions sdkLimitOptions,
            IServiceProvider serviceProvider)
        {
            exporterOptions.TryEnableIHttpClientFactoryIntegration(serviceProvider, "OtlpLogExporter");

            var otlpExporter = new OtlpLogExporter(exporterOptions, sdkLimitOptions);

            if (exporterOptions.ExportProcessorType == ExportProcessorType.Simple)
            {
                builder.AddProcessor(new SimpleLogRecordExportProcessor(otlpExporter));
            }
            else
            {
                // TODO: exporterOptions.BatchExportProcessorOptions is
                // BatchExportActivityProcessorOptions which is using tracing
                // environment variables. There should probably be a dedicated
                // setting for logs using BatchExportLogRecordProcessorOptions
                builder.AddProcessor(new BatchLogRecordExportProcessor(
                    otlpExporter,
                    exporterOptions.BatchExportProcessorOptions.MaxQueueSize,
                    exporterOptions.BatchExportProcessorOptions.ScheduledDelayMilliseconds,
                    exporterOptions.BatchExportProcessorOptions.ExporterTimeoutMilliseconds,
                    exporterOptions.BatchExportProcessorOptions.MaxExportBatchSize));
            }
        }
    }
}
