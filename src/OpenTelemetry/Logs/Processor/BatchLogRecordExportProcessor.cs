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
    private static long instanceCounter = -1;

    private readonly KeyValuePair<string, object?>[] successTags;
    private readonly KeyValuePair<string, object?>[] queueFullTags;
    private readonly KeyValuePair<string, object?>[] alreadyShutdownTags;

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
        int maxQueueSize = DefaultMaxQueueSize,
        int scheduledDelayMilliseconds = DefaultScheduledDelayMilliseconds,
        int exporterTimeoutMilliseconds = DefaultExporterTimeoutMilliseconds,
        int maxExportBatchSize = DefaultMaxExportBatchSize)
        : base(
            exporter,
            maxQueueSize,
            scheduledDelayMilliseconds,
            exporterTimeoutMilliseconds,
            maxExportBatchSize)
    {
        var index = Interlocked.Increment(ref instanceCounter);
        var componentName = "batching_log_processor/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var baseTags = new KeyValuePair<string, object?>[]
        {
            new("otel.component.type", "batching_log_processor"),
            new("otel.component.name", componentName),
        };
        this.successTags = baseTags;
        this.queueFullTags = [.. baseTags, new("error.type", "queue_full")];
        this.alreadyShutdownTags = [.. baseTags, new("error.type", "already_shutdown")];
        this.ExportStarted = this.RecordSuccessfulProcessing;
    }

    /// <inheritdoc/>
    public override void OnEnd(LogRecord data)
    {
        if (!this.TryEnterOnEnd())
        {
            SdkSelfObservability.LogProcessedCounter.Add(1, this.alreadyShutdownTags);
            return;
        }

        try
        {
            // Note: Intentionally not using Guard.ThrowIfNull to save prod cycles
#pragma warning disable CA1062 // Validate arguments of public methods
            switch (data.Source)
#pragma warning restore CA1062 // Validate arguments of public methods
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

                case LogRecord.LogRecordSource.FromThreadStaticPool:
                default:
                    Debug.Assert(data.Source == LogRecord.LogRecordSource.FromThreadStaticPool, "LogRecord source was something unexpected");

                    // Note: If we are using ThreadStatic pool we make a copy of the record.
                    this.TryExport(data.Copy());
                    break;
            }
        }
        finally
        {
            this.ExitOnEnd();
        }
    }

    // TODO: https://github.com/open-telemetry/opentelemetry-dotnet/issues/7586
    // Consider an ObservableCounter instead of per-item Counter.Add().
    internal override void OnItemDropped()
        => SdkSelfObservability.LogProcessedCounter.Add(1, this.queueFullTags);

    private void RecordSuccessfulProcessing(long count)
        => SdkSelfObservability.LogProcessedCounter.Add(count, this.successTags);
}
