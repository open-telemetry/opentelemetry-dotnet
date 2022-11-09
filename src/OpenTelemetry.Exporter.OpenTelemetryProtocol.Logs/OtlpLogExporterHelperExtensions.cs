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
        public static OpenTelemetryLoggerOptions AddOtlpExporter(this OpenTelemetryLoggerOptions loggerOptions)
            => AddOtlpExporter(loggerOptions, configure: null);

        /// <summary>
        /// Adds OTLP Exporter as a configuration to the OpenTelemetry ILoggingBuilder.
        /// </summary>
        /// <remarks>
        /// Note: AddOtlpExporter automatically sets <see
        /// cref="OpenTelemetryLoggerOptions.ParseStateValues"/> to <see
        /// langword="true"/>.
        /// </remarks>
        /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
        /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
        /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
        public static OpenTelemetryLoggerOptions AddOtlpExporter(
            this OpenTelemetryLoggerOptions loggerOptions,
            Action<OtlpExporterOptions> configure)
            => AddOtlpExporter(loggerOptions, new(), configure);

        private static OpenTelemetryLoggerOptions AddOtlpExporter(
            OpenTelemetryLoggerOptions loggerOptions,
            OtlpExporterOptions exporterOptions,
            Action<OtlpExporterOptions> configure)
        {
            configure?.Invoke(exporterOptions);

            var otlpExporter = new OtlpLogExporter(exporterOptions);
            loggerOptions.ParseStateValues = true;
            if (exporterOptions.ExportProcessorType == ExportProcessorType.Simple)
            {
                loggerOptions.AddProcessor(new SimpleLogRecordExportProcessor(otlpExporter));
            }
            else
            {
                loggerOptions.AddProcessor(new BatchLogRecordExportProcessor(
                    otlpExporter,
                    exporterOptions.BatchExportProcessorOptions.MaxQueueSize,
                    exporterOptions.BatchExportProcessorOptions.ScheduledDelayMilliseconds,
                    exporterOptions.BatchExportProcessorOptions.ExporterTimeoutMilliseconds,
                    exporterOptions.BatchExportProcessorOptions.MaxExportBatchSize));
            }

            return loggerOptions;
        }
    }
}
