// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
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
        int maxQueueSize,
        int scheduledDelayMilliseconds,
        int exporterTimeoutMilliseconds,
        int maxExportBatchSize)
        : this(exporter, true, maxQueueSize, scheduledDelayMilliseconds, exporterTimeoutMilliseconds, maxExportBatchSize)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchExportProcessor{T}"/> class.
    /// </summary>
    /// <param name="exporter">Exporter instance.</param>
    /// <param name="useThreads">Enables the use of <see cref="Thread" /> when true, <see cref="Task"/> when false.</param>
    /// <param name="maxQueueSize">The maximum queue size. After the size is reached data are dropped. The default value is 2048.</param>
    /// <param name="scheduledDelayMilliseconds">The delay interval in milliseconds between two consecutive exports. The default value is 5000.</param>
    /// <param name="exporterTimeoutMilliseconds">How long the export can run before it is cancelled. The default value is 30000.</param>
    /// <param name="maxExportBatchSize">The maximum batch size of every export. It must be smaller or equal to maxQueueSize. The default value is 512.</param>
    protected BatchExportProcessor(
        BaseExporter<T> exporter,
        bool useThreads = true,
        int maxQueueSize = DefaultMaxQueueSize,
        int scheduledDelayMilliseconds = DefaultScheduledDelayMilliseconds,
        int exporterTimeoutMilliseconds = DefaultExporterTimeoutMilliseconds,
        int maxExportBatchSize = DefaultMaxExportBatchSize)
        : base(exporter)
    {
        Guard.ThrowIfOutOfRange(maxQueueSize, min: 1);
        Guard.ThrowIfOutOfRange(maxExportBatchSize, min: 1, max: maxQueueSize, maxName: nameof(maxQueueSize));
        Guard.ThrowIfOutOfRange(scheduledDelayMilliseconds, min: 1);
        Guard.ThrowIfOutOfRange(exporterTimeoutMilliseconds, min: 0);

        this.circularBuffer = new CircularBuffer<T>(maxQueueSize);
        this.ScheduledDelayMilliseconds = scheduledDelayMilliseconds;
        this.ExporterTimeoutMilliseconds = exporterTimeoutMilliseconds;
        this.MaxExportBatchSize = maxExportBatchSize;
        this.useThreads = useThreads;

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

        var sw = Stopwatch.StartNew();
        var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;
        return this.exporter.Shutdown((int)Math.Max(timeout, 0)) && result;
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
