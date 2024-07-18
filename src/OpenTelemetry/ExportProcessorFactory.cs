// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

public static class ExportProcessorFactory<T>
    where T : class
{
    public static BaseExportProcessor<T> CreateBatchExportProcessor(
        BatchExportProcessorOptions<T> options,
        BaseExporter<T> exporter)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNull(exporter);

        if (typeof(T) == typeof(Activity))
        {
            return (BaseExportProcessor<T>)(object)new BatchActivityExportProcessor(
                (BaseExporter<Activity>)(object)exporter,
                options.MaxQueueSize,
                options.ScheduledDelayMilliseconds,
                options.ExporterTimeoutMilliseconds,
                options.MaxExportBatchSize);
        }
        else if (typeof(T) == typeof(LogRecord))
        {
            return (BaseExportProcessor<T>)(object)new BatchLogRecordExportProcessor(
                (BaseExporter<LogRecord>)(object)exporter,
                options.MaxQueueSize,
                options.ScheduledDelayMilliseconds,
                options.ExporterTimeoutMilliseconds,
                options.MaxExportBatchSize);
        }
        else
        {
            throw new NotSupportedException($"Building batch export processors for type '{typeof(T)}' is not supported");
        }
    }

    public static BaseExportProcessor<T> CreateSimpleExportProcessor(
        BaseExporter<T> exporter,
        ConcurrencyModes concurrencyMode = ConcurrencyModes.Reentrant)
    {
        Guard.ThrowIfNull(exporter);

        if (!concurrencyMode.HasFlag(ConcurrencyModes.Reentrant))
        {
            throw new NotSupportedException("Non-reentrant simple export processors are not currently supported.");
        }

        if (typeof(T) == typeof(Activity))
        {
            if (concurrencyMode.HasFlag(ConcurrencyModes.Multithreaded))
            {
                return (BaseExportProcessor<T>)(object)new SimpleMultithreadedActivityExportProcessor(
                    (BaseExporter<Activity>)(object)exporter);
            }
            else
            {
                return (BaseExportProcessor<T>)(object)new SimpleActivityExportProcessor(
                    (BaseExporter<Activity>)(object)exporter);
            }
        }
        else if (typeof(T) == typeof(LogRecord))
        {
            if (concurrencyMode.HasFlag(ConcurrencyModes.Multithreaded))
            {
                return (BaseExportProcessor<T>)(object)new SimpleMultithreadedExportProcessor<LogRecord>(
                    (BaseExporter<LogRecord>)(object)exporter);
            }
            else
            {
                return (BaseExportProcessor<T>)(object)new SimpleLogRecordExportProcessor(
                    (BaseExporter<LogRecord>)(object)exporter);
            }
        }
        else
        {
            throw new NotSupportedException($"Building simple export processors for type '{typeof(T)}' is not supported");
        }
    }
}
