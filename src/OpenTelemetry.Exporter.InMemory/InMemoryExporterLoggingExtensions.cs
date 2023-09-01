// <copyright file="InMemoryExporterLoggingExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

public static class InMemoryExporterLoggingExtensions
{
    /// <summary>
    /// Adds InMemory exporter to the OpenTelemetryLoggerOptions.
    /// </summary>
    /// <param name="loggerOptions"><see cref="OpenTelemetryLoggerOptions"/> options to use.</param>
    /// <param name="exportedItems">Collection which will be populated with the exported <see cref="LogRecord"/>.</param>
    /// <returns>The supplied instance of <see cref="OpenTelemetryLoggerOptions"/> to chain the calls.</returns>
    /// todo: [Obsolete("Call LoggerProviderBuilder.AddInMemoryExporter instead this method will be removed in a future version.")]
    public static OpenTelemetryLoggerOptions AddInMemoryExporter(
        this OpenTelemetryLoggerOptions loggerOptions,
        ICollection<LogRecord> exportedItems)
    {
        Guard.ThrowIfNull(loggerOptions);
        Guard.ThrowIfNull(exportedItems);

        var logExporter = BuildExporter(exportedItems);

        return loggerOptions.AddProcessor(
            new SimpleLogRecordExportProcessor(logExporter));
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Adds InMemory exporter to the LoggerProviderBuilder.
    /// </summary>
    /// <remarks><b>WARNING</b>: This is an experimental API which might change or be removed in the future. Use at your own risk.</remarks>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="exportedItems">Collection which will be populated with the exported <see cref="LogRecord"/>.</param>
    /// <returns>The supplied instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    public
#else
    /// <summary>
    /// Adds InMemory exporter to the LoggerProviderBuilder.
    /// </summary>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="exportedItems">Collection which will be populated with the exported <see cref="LogRecord"/>.</param>
    /// <returns>The supplied instance of <see cref="LoggerProviderBuilder"/> to chain the calls.</returns>
    internal
#endif
        static LoggerProviderBuilder AddInMemoryExporter(
        this LoggerProviderBuilder loggerProviderBuilder,
        ICollection<LogRecord> exportedItems)
    {
        Guard.ThrowIfNull(loggerProviderBuilder);
        Guard.ThrowIfNull(exportedItems);

        var logExporter = BuildExporter(exportedItems);

        return loggerProviderBuilder.AddProcessor(
            new SimpleLogRecordExportProcessor(logExporter));
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
