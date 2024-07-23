// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

internal static class LogRecordExportProcessorFactory
{
    public static BaseExportProcessor<LogRecord> CreateBatchExportProcessor(
        BaseExporter<LogRecord> exporter,
        BatchExportProcessorOptions<LogRecord> options,
        int pipelineWeight)
    {
        Guard.ThrowIfNull(exporter);
        Guard.ThrowIfNull(options);

        return new BatchLogRecordExportProcessor(
            exporter,
            options.MaxQueueSize,
            options.ScheduledDelayMilliseconds,
            options.ExporterTimeoutMilliseconds,
            options.MaxExportBatchSize)
        {
            PipelineWeight = pipelineWeight,
        };
    }

    public static BaseExportProcessor<LogRecord> CreateSimpleExportProcessor(
        BaseExporter<LogRecord> exporter,
        int pipelineWeight)
    {
        Guard.ThrowIfNull(exporter);

        BaseExportProcessor<LogRecord> processor = ConcurrencyModesAttribute
            .GetConcurrencyModeForExporter(exporter)
            .HasFlag(ConcurrencyModes.Multithreaded)
                ? new SimpleMultithreadedExportProcessor<LogRecord>(exporter)
                : new SimpleLogRecordExportProcessor(exporter);

        processor.PipelineWeight = pipelineWeight;

        return processor;
    }
}
