// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry;

/// <summary>
/// Implements processor that batches telemetry objects before calling exporter.
/// </summary>
/// <typeparam name="T">The type of telemetry object to be exported.</typeparam>
public abstract class BatchExportProcessor<T> : BaseExportProcessor<T>
    where T : class
{
    internal const int DefaultMaxQueueSize = 2048;
    internal const int DefaultScheduledDelayMilliseconds = 5000;
    internal const int DefaultExporterTimeoutMilliseconds = 30000;
    internal const int DefaultMaxExportBatchSize = 512;

    internal readonly int MaxExportBatchSize;
    internal readonly int ScheduledDelayMilliseconds;
    internal readonly int ExporterTimeoutMilliseconds;

    private readonly CircularBuffer<T> circularBuffer;
    private readonly BatchExportWorker<T> worker;
    private readonly bool useThreads;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchExportProcessor{T}"/> class.
    /// </summary>
    /// <param name="exporter">Exporter instance.</param>
    /// <param name="maxQueueSize">The maximum queue size. After the size is reached data are dropped. The default value is 2048.</param>
    /// <param name="scheduledDelayMilliseconds">The delay interval in milliseconds between two consecutive exports. The default value is 5000.</param>
    /// <param name="exporterTimeoutMilliseconds">How long the export can run before it is cancelled. The default value is 30000.</param>
    /// <param name="maxExportBatchSize">The maximum batch size of every export. It must be smaller or equal to maxQueueSize. The default value is 512.</param>
    protected BatchExportProcessor(
        BaseExporter<T> exporter,
        int maxQueueSize = DefaultMaxQueueSize,
        int scheduledDelayMilliseconds = DefaultScheduledDelayMilliseconds,
        int exporterTimeoutMilliseconds = DefaultExporterTimeoutMilliseconds,
        int maxExportBatchSize = DefaultMaxExportBatchSize)
        : this(exporter, new BatchExportProcessorOptions<T>
        {
            MaxQueueSize = maxQueueSize,
            ScheduledDelayMilliseconds = scheduledDelayMilliseconds,
            ExporterTimeoutMilliseconds = exporterTimeoutMilliseconds,
            MaxExportBatchSize = maxExportBatchSize,
            UseThreads = true,
        })
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchExportProcessor{T}"/> class.
    /// </summary>
    /// <param name="exporter">Exporter instance.</param>
    /// <param name="options">Configuration options for the batch export processor.</param>
    protected BatchExportProcessor(
        BaseExporter<T> exporter,
        BatchExportProcessorOptions<T> options)
        : base(exporter)
    {
        Guard.ThrowIfNull(options);

        var maxQueueSize = options?.MaxQueueSize ?? 0;
        Guard.ThrowIfOutOfRange(maxQueueSize, min: 1);

        this.circularBuffer = new CircularBuffer<T>(maxQueueSize);
        this.ScheduledDelayMilliseconds = options?.ScheduledDelayMilliseconds ?? 0;
        this.ExporterTimeoutMilliseconds = options?.ExporterTimeoutMilliseconds ?? -1;
        this.MaxExportBatchSize = options?.MaxExportBatchSize ?? 0;
        this.useThreads = options?.UseThreads ?? true;

        Guard.ThrowIfOutOfRange(this.MaxExportBatchSize, min: 1, max: maxQueueSize, maxName: nameof(options.MaxQueueSize));
        Guard.ThrowIfOutOfRange(this.ScheduledDelayMilliseconds, min: 1);
        Guard.ThrowIfOutOfRange(this.ExporterTimeoutMilliseconds, min: 0);

        this.worker = this.CreateWorker();
        this.worker.Start();
    }

    /// <summary>
    /// Gets the number of telemetry objects dropped by the processor.
    /// </summary>
    internal long DroppedCount => this.worker.DroppedCount;

    /// <summary>
    /// Gets the number of telemetry objects received by the processor.
    /// </summary>
    internal long ReceivedCount => this.circularBuffer.AddedCount + this.DroppedCount;

    /// <summary>
    /// Gets the number of telemetry objects processed by the underlying exporter.
    /// </summary>
    internal long ProcessedCount => this.circularBuffer.RemovedCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryExport(T data)
    {
        if (this.circularBuffer.TryAdd(data, maxSpinCount: 50000))
        {
            if (this.circularBuffer.Count >= this.MaxExportBatchSize)
            {
                this.worker.TriggerExport();
            }

            return true; // enqueue succeeded
        }

        // either the queue is full or exceeded the spin limit, drop the item on the floor
        this.worker.IncrementDroppedCount();

        return false;
    }

    /// <inheritdoc/>
    protected override void OnExport(T data)
    {
        this.TryExport(data);
    }

    /// <inheritdoc/>
    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        return this.worker.WaitForExport(timeoutMilliseconds);
    }

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        var result = this.worker.Shutdown(timeoutMilliseconds);

        OpenTelemetrySdkEventSource.Log.DroppedExportProcessorItems(this.GetType().Name, this.exporter.GetType().Name, this.DroppedCount);

        if (timeoutMilliseconds == Timeout.Infinite)
        {
            return this.exporter.Shutdown() && result;
        }

        if (timeoutMilliseconds == 0)
        {
            return this.exporter.Shutdown(0) && result;
        }

        return this.exporter.Shutdown(timeoutMilliseconds) && result;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                this.worker?.Dispose();
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }

    private BatchExportWorker<T> CreateWorker()
    {
#if NET
        // Use task-based worker for browser platform where threading may be limited
        if (OperatingSystem.IsBrowser() || !this.useThreads)
        {
            return new BatchExportTaskWorker<T>(
                this.circularBuffer,
                this.exporter,
                this.MaxExportBatchSize,
                this.ScheduledDelayMilliseconds,
                this.ExporterTimeoutMilliseconds);
        }
#endif

        // Use thread-based worker for all other platforms
        return new BatchExportThreadWorker<T>(
            this.circularBuffer,
            this.exporter,
            this.MaxExportBatchSize,
            this.ScheduledDelayMilliseconds,
            this.ExporterTimeoutMilliseconds);
    }
}
