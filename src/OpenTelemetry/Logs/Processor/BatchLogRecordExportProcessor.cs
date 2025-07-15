// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Logs;

namespace OpenTelemetry;

/// <summary>
/// Implements a batch log record export processor.
/// </summary>
public class BatchLogRecordExportProcessor : BatchExportProcessor<LogRecord>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BatchLogRecordExportProcessor"/> class.
    /// </summary>
    /// <param name="exporter">Log record exporter.</param>
    /// <param name="maxQueueSize">The maximum queue size. After the size is reached data are dropped. The default value is 2048.</param>
    /// <param name="scheduledDelayMilliseconds">The delay interval in milliseconds between two consecutive exports. The default value is 5000.</param>
    /// <param name="exporterTimeoutMilliseconds">How long the export can run before it is cancelled. The default value is 30000.</param>
    /// <param name="maxExportBatchSize">The maximum batch size of every export. It must be smaller or equal to maxQueueSize. The default value is 512.</param>
    public BatchLogRecordExportProcessor(
        BaseExporter<LogRecord> exporter,
        int maxQueueSize,
        int scheduledDelayMilliseconds,
        int exporterTimeoutMilliseconds,
        int maxExportBatchSize)
        : this(
            exporter,
            maxQueueSize,
            scheduledDelayMilliseconds,
            exporterTimeoutMilliseconds,
            maxExportBatchSize,
            true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchLogRecordExportProcessor"/> class.
    /// </summary>
    /// <param name="exporter">Log record exporter.</param>
    /// <param name="useThreads">Enables the use of <see cref="Thread" /> when true, <see cref="Task"/> when false.</param>
    /// <param name="maxQueueSize">The maximum queue size. After the size is reached data are dropped. The default value is 2048.</param>
    /// <param name="scheduledDelayMilliseconds">The delay interval in milliseconds between two consecutive exports. The default value is 5000.</param>
    /// <param name="exporterTimeoutMilliseconds">How long the export can run before it is cancelled. The default value is 30000.</param>
    /// <param name="maxExportBatchSize">The maximum batch size of every export. It must be smaller or equal to maxQueueSize. The default value is 512.</param>
    public BatchLogRecordExportProcessor(
        BaseExporter<LogRecord> exporter,
        int maxQueueSize = DefaultMaxQueueSize,
        int scheduledDelayMilliseconds = DefaultScheduledDelayMilliseconds,
        int exporterTimeoutMilliseconds = DefaultExporterTimeoutMilliseconds,
        int maxExportBatchSize = DefaultMaxExportBatchSize,
        bool useThreads = true)
        : base(
            exporter,
            maxQueueSize,
            scheduledDelayMilliseconds,
            exporterTimeoutMilliseconds,
            maxExportBatchSize,
            useThreads)
    {
    }

    /// <inheritdoc/>
    public override void OnEnd(LogRecord data)
    {
        // Note: Intentionally doing a Debug.Assert here and not a
        // Guard.ThrowIfNull to save prod cycles. Null should really never
        // happen here.
        Debug.Assert(data != null, "LogRecord was null.");

        switch (data!.Source)
        {
            case LogRecord.LogRecordSource.FromSharedPool:
                data.Buffer();
                data.AddReference();
                if (!this.TryExport(data))
                {
                    LogRecordSharedPool.Current.Return(data);
                }

                break;
            case LogRecord.LogRecordSource.CreatedManually:
                data.Buffer();
                this.TryExport(data);
                break;
            default:
                Debug.Assert(data.Source == LogRecord.LogRecordSource.FromThreadStaticPool, "LogRecord source was something unexpected");

                // Note: If we are using ThreadStatic pool we make a copy of the record.
                this.TryExport(data.Copy());
                break;
        }
    }
}
