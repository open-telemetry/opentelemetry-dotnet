// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

internal static class LogRecordExportProcessorFactory
{
    public static BaseExportProcessor<LogRecord> CreateBatchExportProcessor(
        LogRecordExportProcessorOptions options,
        BaseExporter<LogRecord> exporter)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNull(exporter);

        return new BatchLogRecordExportProcessor(
            exporter,
            options.BatchExportProcessorOptions.MaxQueueSize,
            options.BatchExportProcessorOptions.ScheduledDelayMilliseconds,
            options.BatchExportProcessorOptions.ExporterTimeoutMilliseconds,
            options.BatchExportProcessorOptions.MaxExportBatchSize)
        {
            PipelineWeight = options.PipelineWeight,
        };
    }

    public static BaseExportProcessor<LogRecord> CreateSimpleExportProcessor(
        LogRecordExportProcessorOptions options,
        BaseExporter<LogRecord> exporter)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNull(exporter);

        var concurrencyMode = exporter
            .GetType()
            .GetCustomAttribute<ConcurrencyModesAttribute>(inherit: true)?.Supported
            ?? ConcurrencyModes.Reentrant;

        if (!concurrencyMode.HasFlag(ConcurrencyModes.Reentrant))
        {
            throw new NotSupportedException("Non-reentrant simple export processors are not currently supported.");
        }

        BaseExportProcessor<LogRecord> processor = concurrencyMode.HasFlag(ConcurrencyModes.Multithreaded)
            ? new SimpleMultithreadedExportProcessor<LogRecord>(exporter)
            : new SimpleLogRecordExportProcessor(exporter);

        processor.PipelineWeight = options.PipelineWeight;

        return processor;
    }
}
