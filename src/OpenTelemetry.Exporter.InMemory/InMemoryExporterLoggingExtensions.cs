// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

#pragma warning disable CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
public static class InMemoryExporterLoggingExtensions
#pragma warning restore CA1001 // Types that own disposable fields should be disposable - handled by GlobalCleanup
{
    /// <summary>
    /// Adds InMemory exporter to the OpenTelemetryLoggerOptions.
    /// </summary>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <param name="exportedItems">Collection which will be populated with the exported <see cref="LogRecord"/>.</param>
    /// <returns>The supplied instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    // TODO: [Obsolete("Call LoggerProviderBuilder.AddInMemoryExporter instead this method will be removed in a future version.")]
    public static OpenTelemetryLoggerOptions AddInMemoryExporter(
        this OpenTelemetryLoggerOptions loggerOptions,
        ICollection<LogRecord> exportedItems)
    {
        Guard.ThrowIfNull(loggerOptions);
        Guard.ThrowIfNull(exportedItems);

#pragma warning disable CA2000 // Dispose objects before losing scope
        var logExporter = BuildExporter(exportedItems);

        return loggerOptions.AddProcessor(
            new SimpleLogRecordExportProcessor(logExporter));
#pragma warning restore CA2000 // Dispose objects before losing scope
    }

    /// <summary>
    /// Adds InMemory exporter to the LoggerProviderBuilder.
    /// </summary>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="exportedItems">Collection which will be populated with the exported <see cref="LogRecord"/>.</param>
    /// <returns>The supplied instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public static LoggerProviderBuilder AddInMemoryExporter(
        this LoggerProviderBuilder loggerProviderBuilder,
        ICollection<LogRecord> exportedItems)
    {
        Guard.ThrowIfNull(loggerProviderBuilder);
        Guard.ThrowIfNull(exportedItems);

#pragma warning disable CA2000 // Dispose objects before losing scope
        var logExporter = BuildExporter(exportedItems);

        return loggerProviderBuilder.AddProcessor(
            new SimpleLogRecordExportProcessor(logExporter));
#pragma warning restore CA2000 // Dispose objects before losing scope
    }

    private static InMemoryExporter<LogRecord> BuildExporter(ICollection<LogRecord> exportedItems)
    {
        return new InMemoryExporter<LogRecord>(
            exportFunc: (in Batch<LogRecord> batch) => ExportLogRecord(in batch, exportedItems));
    }

    private static ExportResult ExportLogRecord(in Batch<LogRecord> batch, ICollection<LogRecord> exportedItems)
    {
        foreach (var log in batch)
        {
            exportedItems.Add(log.Copy());
        }

        return ExportResult.Success;
    }
}
