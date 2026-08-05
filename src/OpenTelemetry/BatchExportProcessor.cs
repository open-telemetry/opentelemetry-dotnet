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

    // Number of OnEnd calls currently in-flight (past the shutdown check).
    // OnShutdown waits for this to reach zero so those items finish enqueueing
    // before teardown, keeping the processed vs. already_shutdown counting race-free.
    private int activeOnEndCount;
    private int isShutdown;
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

        this.worker = this.CreateWorker();
        this.worker.Start();
    }

    internal Action<long>? ExportStarted { get; set; }

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

    /// <summary>
    /// Gets a value indicating whether <see cref="OnShutdown(int)"/> has been invoked.
    /// </summary>
    private bool IsShutdown => Volatile.Read(ref this.isShutdown) != 0;

    /// <summary>
    /// Marks the beginning of an <see cref="BaseProcessor{T}.OnEnd(T)"/> call which may enqueue data.
    /// </summary>
    /// <remarks>
    /// When this returns <see langword="true"/> the caller MUST invoke <see
    /// cref="ExitOnEnd"/> once it is done enqueueing. When it returns <see
    /// langword="false"/> the processor has already been shut down and the
    /// caller MUST NOT enqueue.
    /// </remarks>
    /// <returns><see langword="true"/> if the caller may proceed to enqueue data.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool TryEnterOnEnd()
    {
        if (this.IsShutdown)
        {
            return false;
        }

        Interlocked.Increment(ref this.activeOnEndCount);

        if (this.IsShutdown)
        {
            Interlocked.Decrement(ref this.activeOnEndCount);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Marks the end of an <see cref="BaseProcessor{T}.OnEnd(T)"/> call started by <see cref="TryEnterOnEnd"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ExitOnEnd()
        => Interlocked.Decrement(ref this.activeOnEndCount);

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
        this.OnItemDropped();

        return false;
    }

    /// <summary>
    /// Invoked when an item could not be enqueued and was dropped.
    /// </summary>
    internal virtual void OnItemDropped()
    {
    }

    /// <inheritdoc/>
    protected override void OnExport(T data)
        => this.TryExport(data);

    /// <inheritdoc/>
    protected override bool OnForceFlush(int timeoutMilliseconds)
        => this.worker.WaitForExport(timeoutMilliseconds);

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        // Note: BaseProcessor.Shutdown guarantees OnShutdown is invoked at most once, so
        // the previous value is discarded. Interlocked is used instead of Volatile.Write
        // for the full fence it provides, which TryEnterOnEnd relies on.
        _ = Interlocked.Exchange(ref this.isShutdown, 1);

        timeoutMilliseconds = this.WaitForActiveOnEndCalls(timeoutMilliseconds);

        long? timestamp = timeoutMilliseconds == Timeout.Infinite ? null : Stopwatch.GetTimestamp();

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

        if (timestamp is { } startedAt)
        {
            timeoutMilliseconds = Stopwatch.Remaining(timeoutMilliseconds, startedAt);
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
        if (ThreadingHelper.IsThreadingDisabled())
        {
            return new BatchExportTaskWorker<T>(
                this.circularBuffer,
                this.exporter,
                this.MaxExportBatchSize,
                this.ScheduledDelayMilliseconds,
                this.ExporterTimeoutMilliseconds,
                this.OnExportStarted);
        }
#endif

        // Use thread-based worker for all other platforms
        return new BatchExportThreadWorker<T>(
            this.circularBuffer,
            this.exporter,
            this.MaxExportBatchSize,
            this.ScheduledDelayMilliseconds,
            this.ExporterTimeoutMilliseconds,
            this.OnExportStarted);
    }

    private void OnExportStarted(long count)
        => this.ExportStarted?.Invoke(count);

    /// <summary>
    /// Waits for in-flight <see cref="BaseProcessor{T}.OnEnd(T)"/> calls to finish enqueueing so
    /// teardown is consistent, without exceeding the caller's shutdown budget.
    /// </summary>
    /// <param name="timeoutMilliseconds">The shutdown timeout supplied by the caller.</param>
    /// <returns>The remaining timeout available for the rest of the shutdown sequence.</returns>
    private int WaitForActiveOnEndCalls(int timeoutMilliseconds)
    {
        if (Volatile.Read(ref this.activeOnEndCount) == 0)
        {
            return timeoutMilliseconds;
        }

        SpinWait spinner = default;

        if (timeoutMilliseconds == Timeout.Infinite)
        {
            while (Volatile.Read(ref this.activeOnEndCount) != 0)
            {
                spinner.SpinOnce();
            }

            return Timeout.Infinite;
        }

        var startedAt = Stopwatch.GetTimestamp();
        int remainingMilliseconds;

        while ((remainingMilliseconds = Stopwatch.Remaining(timeoutMilliseconds, startedAt)) > 0
            && Volatile.Read(ref this.activeOnEndCount) != 0)
        {
            spinner.SpinOnce();
        }

        return remainingMilliseconds;
    }
}
