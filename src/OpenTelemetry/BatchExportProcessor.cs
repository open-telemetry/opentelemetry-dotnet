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
    private readonly CancellationTokenSource exporterTaskCancellation;
    private readonly Task exporterTask;
    private Task exportTask;
    private long shutdownDrainTarget = long.MaxValue;
    private long droppedCount;
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
        this.exportTask = Task.CompletedTask;
        this.exporterTaskCancellation = new CancellationTokenSource();
        this.exporterTask = Task.Run(this.ExporterProc);
    }

    /// <summary>
    /// Gets the number of telemetry objects dropped by the processor.
    /// </summary>
    internal long DroppedCount => Volatile.Read(ref this.droppedCount);

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
                _ = this.ExportAsync();
            }

            return true; // enqueue succeeded
        }

        // either the queue is full or exceeded the spin limit, drop the item on the floor
        Interlocked.Increment(ref this.droppedCount);

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
        return this.FlushAsync(TimeSpan.FromMilliseconds(timeoutMilliseconds)).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        return this.ShutdownAsync(TimeSpan.FromMilliseconds(timeoutMilliseconds)).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                this.exporterTaskCancellation.Cancel();
                this.exporterTaskCancellation.Dispose();
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }

    private async Task<bool> FlushAsync(TimeSpan timeout)
    {
        var tail = this.circularBuffer.RemovedCount;
        var head = this.circularBuffer.AddedCount;

        if (head == tail)
        {
            return true; // nothing to flush
        }

        _ = this.ExportAsync();

        if (timeout == TimeSpan.Zero)
        {
            return false;
        }

        CancellationTokenSource timeoutCancellation;
        try
        {
            timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(this.exporterTaskCancellation.Token);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        var timeoutTask = Task.Delay(timeout, timeoutCancellation.Token);

        while (true)
        {
            Task completedTask;
            try
            {
                completedTask = await Task.WhenAny(timeoutTask, this.ExportAsync()).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                return false;
            }

            if (this.circularBuffer.RemovedCount >= head)
            {
                return true;
            }

            if (completedTask == timeoutTask)
            {
                return false;
            }

            if (Volatile.Read(ref this.shutdownDrainTarget) != long.MaxValue)
            {
                return false;
            }
        }
    }

    private async Task<bool> ShutdownAsync(TimeSpan timeout)
    {
        Volatile.Write(ref this.shutdownDrainTarget, this.circularBuffer.AddedCount);

        try
        {
            this.exporterTaskCancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        OpenTelemetrySdkEventSource.Log.DroppedExportProcessorItems(this.GetType().Name, this.exporter.GetType().Name, this.DroppedCount);

        if (timeout == Timeout.InfiniteTimeSpan)
        {
            await this.exporterTask.ConfigureAwait(false);
            return this.exporter.Shutdown();
        }

        if (timeout == TimeSpan.Zero)
        {
            return this.exporter.Shutdown(0);
        }

        var sw = Stopwatch.StartNew();
        await Task.WhenAny(this.exporterTask, Task.Delay(timeout)).ConfigureAwait(false);
        var remainingTimeout = timeout.TotalMilliseconds - sw.ElapsedMilliseconds;
        return this.exporter.Shutdown((int)Math.Max(remainingTimeout, 0));
    }

    private async Task ExporterProc()
    {
        while (true)
        {
            // only wait when the queue doesn't have enough items, otherwise keep busy and send data continuously
            if (this.circularBuffer.Count < this.MaxExportBatchSize)
            {
                try
                {
                    await Task.Delay(this.ScheduledDelayMilliseconds, this.exporterTaskCancellation.Token).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    // The delay was canceled for the exporter to shut down.
                }
                catch (ObjectDisposedException)
                {
                    // the exporter is somehow disposed before the worker thread could finish its job
                    return;
                }
            }

            if (this.circularBuffer.Count > 0)
            {
                await this.ExportAsync().ConfigureAwait(false);
            }

            if (this.circularBuffer.RemovedCount >= Volatile.Read(ref this.shutdownDrainTarget))
            {
                return;
            }
        }
    }

    private Task ExportAsync()
    {
        var optimisticExportTask = this.exportTask;
        if (!optimisticExportTask.IsCompleted)
        {
            // An export is currently being processed.
            return optimisticExportTask;
        }

        TaskCompletionSource<object?> newCurrentExportTaskCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var localExportTask = Interlocked.CompareExchange(
            ref this.exportTask,
            newCurrentExportTaskCompletion.Task,
            optimisticExportTask);
        if (!localExportTask.IsCompleted)
        {
            // An export is currently being processed.
            return localExportTask;
        }

        // Use Task.Run to yield the execution as soon as possible.
        return Task.Run(CoreAsync);

        async Task CoreAsync()
        {
            try
            {
                using (var batch = new Batch<T>(this.circularBuffer, this.MaxExportBatchSize))
                {
                    await this.exporter.ExportAsync(batch).ConfigureAwait(false);
                }

                newCurrentExportTaskCompletion.SetResult(null);
            }
            catch (Exception e)
            {
                newCurrentExportTaskCompletion.SetException(e);
            }
        }
    }
}
