// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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

        return loggerOptions.AddProcessor(BuildOtlpLogExporter(exporterOptions, processorOptions));
    }

    internal static BaseProcessor<LogRecord> BuildOtlpLogExporter(
        OtlpExporterOptions exporterOptions,
        LogRecordExportProcessorOptions processorOptions,
        Func<BaseExporter<LogRecord>, BaseExporter<LogRecord>> configureExporterInstance = null)
    {
        BaseExporter<LogRecord> otlpExporter = new OtlpLogExporter(exporterOptions);

        if (configureExporterInstance != null)
        {
            otlpExporter = configureExporterInstance(otlpExporter);
        }

        if (processorOptions.ExportProcessorType == ExportProcessorType.Simple)
        {
            return new SimpleLogRecordExportProcessor(otlpExporter);
        }
        else
        {
            var batchOptions = processorOptions.BatchExportProcessorOptions;

            return new BatchLogRecordExportProcessor(
                otlpExporter,
                batchOptions.MaxQueueSize,
                batchOptions.ScheduledDelayMilliseconds,
                batchOptions.ExporterTimeoutMilliseconds,
                batchOptions.MaxExportBatchSize);
        }
    }

    private static OpenTelemetryLoggerOptions AddOtlpExporterInternal(
    OpenTelemetryLoggerOptions loggerOptions,
    Action<OtlpExporterOptions> configure)
    {
        var exporterOptions = new OtlpExporterOptions();

        configure?.Invoke(exporterOptions);

        return loggerOptions.AddProcessor(BuildOtlpLogExporter(exporterOptions, new()));
    }
}
