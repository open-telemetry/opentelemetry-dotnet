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
using OpenTelemetry.Internal;

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
        /// <remarks><inheritdoc cref="AddOtlpExporter(OpenTelemetryLoggerOptions, string, Action{OtlpExporterOptions})" path="/remarks"/></remarks>
        /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
        /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
        public static OpenTelemetryLoggerOptions AddOtlpExporter(this OpenTelemetryLoggerOptions loggerOptions)
            => AddOtlpExporter(loggerOptions, name: null, configure: null);

        /// <summary>
        /// Adds OTLP Exporter as a configuration to the OpenTelemetry ILoggingBuilder.
        /// </summary>
        /// <remarks><inheritdoc cref="AddOtlpExporter(OpenTelemetryLoggerOptions, string, Action{OtlpExporterOptions})" path="/remarks"/></remarks>
        /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
        /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
        /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
        public static OpenTelemetryLoggerOptions AddOtlpExporter(
            this OpenTelemetryLoggerOptions loggerOptions,
            Action<OtlpExporterOptions> configure)
            => AddOtlpExporter(loggerOptions, name: null, configure);

        /// <summary>
        /// Adds OTLP Exporter as a configuration to the OpenTelemetry ILoggingBuilder.
        /// </summary>
        /// <remarks>
        /// Note: AddOtlpExporter automatically sets <see
        /// cref="OpenTelemetryLoggerOptions.ParseStateValues"/> to <see
        /// langword="true"/>.
        /// </remarks>
        /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
        /// <param name="name">Name which is used when retrieving options.</param>
        /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
        /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
        public static OpenTelemetryLoggerOptions AddOtlpExporter(
            this OpenTelemetryLoggerOptions loggerOptions,
            string name,
            Action<OtlpExporterOptions> configure)
        {
            Guard.ThrowIfNull(loggerOptions);

            loggerOptions.ParseStateValues = true;

            name ??= Options.DefaultName;

            if (configure != null)
            {
                loggerOptions.ConfigureServices(services => services.Configure(name, configure));
            }

            return loggerOptions.ConfigureProvider((sp, provider) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get(name);

                AddOtlpExporter(provider, options, sp);
            });
        }

        private static void AddOtlpExporter(
            OpenTelemetryLoggerProvider provider,
            OtlpExporterOptions exporterOptions,
            IServiceProvider serviceProvider)
        {
            exporterOptions.TryEnableIHttpClientFactoryIntegration(serviceProvider, "OtlpLogExporter");

            var otlpExporter = new OtlpLogExporter(exporterOptions);

            if (exporterOptions.ExportProcessorType == ExportProcessorType.Simple)
            {
                provider.AddProcessor(new SimpleLogRecordExportProcessor(otlpExporter));
            }
            else
            {
                // TODO: exporterOptions.BatchExportProcessorOptions is
                // BatchExportActivityProcessorOptions which is using tracing
                // environment variables. There should probably be a dedicated
                // setting for logs using BatchExportLogRecordProcessorOptions
                provider.AddProcessor(new BatchLogRecordExportProcessor(
                    otlpExporter,
                    exporterOptions.BatchExportProcessorOptions.MaxQueueSize,
                    exporterOptions.BatchExportProcessorOptions.ScheduledDelayMilliseconds,
                    exporterOptions.BatchExportProcessorOptions.ExporterTimeoutMilliseconds,
                    exporterOptions.BatchExportProcessorOptions.MaxExportBatchSize));
            }
        }
    }
}
