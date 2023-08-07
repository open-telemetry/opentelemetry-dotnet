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

using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;

namespace OpenTelemetry.Logs;

/// <summary>
/// Extension methods to simplify registering of the OpenTelemetry Protocol (OTLP) exporter.
/// </summary>
public static class OtlpLogExporterHelperExtensions
{
    /// <summary>
    /// Adds an OTLP Exporter to the OpenTelemetry <see cref="ILoggerProvider"/>.
    /// </summary>
    /// <remarks><inheritdoc cref="AddOtlpExporter(OpenTelemetryLoggerOptions, Action{OtlpExporterOptions})" path="/remarks"/></remarks>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    public static OpenTelemetryLoggerOptions AddOtlpExporter(this OpenTelemetryLoggerOptions loggerOptions)
        => AddOtlpExporterInternal(loggerOptions, configure: null);

    /// <summary>
    /// Adds an OTLP Exporter to the OpenTelemetry <see cref="ILoggerProvider"/>.
    /// </summary>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    public static OpenTelemetryLoggerOptions AddOtlpExporter(
        this OpenTelemetryLoggerOptions loggerOptions,
        Action<OtlpExporterOptions> configure)
        => AddOtlpExporterInternal(loggerOptions, configure);

    /// <summary>
    /// Adds an OTLP Exporter to the OpenTelemetry <see cref="ILoggerProvider"/>.
    /// </summary>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <param name="configureExporterAndProcessor">Callback action for configuring <see cref="OtlpExporterOptions"/> and <see cref="LogRecordExportProcessorOptions"/>.</param>
    /// <returns>The instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    public static OpenTelemetryLoggerOptions AddOtlpExporter(
        this OpenTelemetryLoggerOptions loggerOptions,
        Action<OtlpExporterOptions, LogRecordExportProcessorOptions> configureExporterAndProcessor)
    {
        var exporterOptions = new OtlpExporterOptions();
        var processorOptions = new LogRecordExportProcessorOptions();

        configureExporterAndProcessor?.Invoke(exporterOptions, processorOptions);

        return AddOtlpLogExporterInternal(
             loggerOptions,
             exporterOptions: exporterOptions,
             processorOptions: processorOptions);
    }

    private static OpenTelemetryLoggerOptions AddOtlpExporterInternal(
        OpenTelemetryLoggerOptions loggerOptions,
        Action<OtlpExporterOptions> configure)
    {
        var exporterOptions = new OtlpExporterOptions();

        configure?.Invoke(exporterOptions);

        return AddOtlpLogExporterInternal(
            loggerOptions,
            exporterOptions: exporterOptions,
            processorOptions: new());
    }

    private static OpenTelemetryLoggerOptions AddOtlpLogExporterInternal(
        OpenTelemetryLoggerOptions loggerOptions,
        OtlpExporterOptions exporterOptions,
        LogRecordExportProcessorOptions processorOptions)
    {
        var otlpExporter = new OtlpLogExporter(exporterOptions);

        if (processorOptions.ExportProcessorType == ExportProcessorType.Simple)
        {
            loggerOptions.AddProcessor(new SimpleLogRecordExportProcessor(otlpExporter));
        }
        else
        {
            var batchOptions = processorOptions.BatchExportProcessorOptions;

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
