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

using OpenTelemetry.Exporter;

namespace OpenTelemetry.Logs
{
    /// <summary>
    /// Extension methods to simplify registering of the OpenTelemetry Protocol (OTLP) exporter.
    /// </summary>
    public static class OtlpLogExporterHelperExtensions
    {
        /// <summary>
        /// Adds OTLP Exporter as a configuration to the OpenTelemetry ILoggingBuilder.
        /// </summary>
        /// <remarks><inheritdoc cref="AddOtlpExporter(OpenTelemetryLoggerOptions, Action{OtlpExporterOptions})" path="/remarks"/></remarks>
        /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
        /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
        [Obsolete("We will never ship this method as it is. We will ship the one that returns LoggerProviderBuilder from main-logs branch instead of OpenTelemetryLoggerOptions.")]
        public static OpenTelemetryLoggerOptions AddOtlpExporter(this OpenTelemetryLoggerOptions loggerOptions)
            => AddOtlpExporter(loggerOptions, configure: null, configureProcessorOptions: null);

        /// <summary>
        /// Adds OTLP Exporter as a configuration to the OpenTelemetry ILoggingBuilder.
        /// </summary>
        /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
        /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
        /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
        [Obsolete("We will never ship this method as it is. We will ship one that returns LoggerProviderBuilder from main-logs branch instead of OpenTelemetryLoggerOptions.")]
        public static OpenTelemetryLoggerOptions AddOtlpExporter(
            this OpenTelemetryLoggerOptions loggerOptions,
            Action<OtlpExporterOptions> configure)
            => AddOtlpExporterInternal(loggerOptions, configure, configureProcessorOptions: null);

        /// <summary>
        /// Adds OTLP Exporter as a configuration to the OpenTelemetry ILoggingBuilder.
        /// </summary>
        /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
        /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
        /// <param name="configureProcessorOptions">Callback action for configuring <see cref="LogRecordExportProcessorOptions"/>.</param>
        /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
        [Obsolete("We will never ship this method as it is. We will ship one that returns LoggerProviderBuilder from main-logs branch instead of OpenTelemetryLoggerOptions.")]
        public static OpenTelemetryLoggerOptions AddOtlpExporter(
            this OpenTelemetryLoggerOptions loggerOptions,
            Action<OtlpExporterOptions> configure,
            Action<LogRecordExportProcessorOptions> configureProcessorOptions)
            => AddOtlpExporterInternal(loggerOptions, configure, configureProcessorOptions);

        private static OpenTelemetryLoggerOptions AddOtlpExporterInternal(
            OpenTelemetryLoggerOptions loggerOptions,
            Action<OtlpExporterOptions> configure,
            Action<LogRecordExportProcessorOptions> configureProcessorOptions)
        {
            // TODO: This has not yet been adapted to correctly utilize IConfiguration/IOptions
            // It simply illustrates that the processor options will not come from
            // the obsolete properties on OtlpExporterOptions.

            var exporterOptions = new OtlpExporterOptions();

            var processorOptions = new LogRecordExportProcessorOptions();

            configure?.Invoke(exporterOptions);
            configureProcessorOptions?.Invoke(processorOptions);

            var otlpExporter = new OtlpLogExporter(exporterOptions);

            if (processorOptions.ExportProcessorType == ExportProcessorType.Simple)
            {
                loggerOptions.AddProcessor(new SimpleLogRecordExportProcessor(otlpExporter));
            }
            else
            {
                var batchOptions = processorOptions.BatchExportProcessorOptions ?? new BatchExportLogRecordProcessorOptions();

                loggerOptions.AddProcessor(new BatchLogRecordExportProcessor(
                    otlpExporter,
                    batchOptions.MaxQueueSize,
                    batchOptions.ScheduledDelayMilliseconds,
                    batchOptions.ExporterTimeoutMilliseconds,
                    batchOptions.MaxExportBatchSize));
            }

            return loggerOptions;
        }
    }
}
