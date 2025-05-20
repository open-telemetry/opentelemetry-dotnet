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
    private readonly Thread exporterThread;
    private readonly AutoResetEvent exportTrigger = new(false);
    private readonly ManualResetEvent dataExportedNotification = new(false);
    private readonly ManualResetEvent shutdownTrigger = new(false);
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
        this.exporterThread = new Thread(this.ExporterProc)
        {
            IsBackground = true,
#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
            Name = $"OpenTelemetry-{nameof(BatchExportProcessor<T>)}-{exporter.GetType().Name}",
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1
        };
        this.exporterThread.Start();
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
                try
                {
                    this.exportTrigger.Set();
                }
                catch (ObjectDisposedException)
                {
                }
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
        var tail = this.circularBuffer.RemovedCount;
        var head = this.circularBuffer.AddedCount;

        if (head == tail)
        {
            return true; // nothing to flush
        }

        try
        {
            this.exportTrigger.Set();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        if (timeoutMilliseconds == 0)
        {
            return false;
        }

        var triggers = new WaitHandle[] { this.dataExportedNotification, this.shutdownTrigger };

        var sw = timeoutMilliseconds == Timeout.Infinite
            ? null
            : Stopwatch.StartNew();

        // There is a chance that the export thread finished processing all the data from the queue,
        // and signaled before we enter wait here, use polling to prevent being blocked indefinitely.
        const int pollingMilliseconds = 1000;

        while (true)
        {
            if (sw == null)
            {
                try
                {
                    WaitHandle.WaitAny(triggers, pollingMilliseconds);
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }
            else
            {
                var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                if (timeout <= 0)
                {
                    return this.circularBuffer.RemovedCount >= head;
                }

                try
                {
                    WaitHandle.WaitAny(triggers, Math.Min((int)timeout, pollingMilliseconds));
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }

            if (this.circularBuffer.RemovedCount >= head)
            {
                return true;
            }

            if (Volatile.Read(ref this.shutdownDrainTarget) != long.MaxValue)
            {
                return false;
            }
        }
    }

    /// <inheritdoc/>
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        Volatile.Write(ref this.shutdownDrainTarget, this.circularBuffer.AddedCount);

        try
        {
            this.shutdownTrigger.Set();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        OpenTelemetrySdkEventSource.Log.DroppedExportProcessorItems(this.GetType().Name, this.exporter.GetType().Name, this.DroppedCount);

        if (timeoutMilliseconds == Timeout.Infinite)
        {
            this.exporterThread.Join();
            return this.exporter.Shutdown();
        }

        if (timeoutMilliseconds == 0)
        {
            return this.exporter.Shutdown(0);
        }

        var sw = Stopwatch.StartNew();
        this.exporterThread.Join(timeoutMilliseconds);
        var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;
        return this.exporter.Shutdown((int)Math.Max(timeout, 0));
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                this.exportTrigger.Dispose();
                this.dataExportedNotification.Dispose();
                this.shutdownTrigger.Dispose();
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }

    private void ExporterProc()
    {
        var triggers = new WaitHandle[] { this.exportTrigger, this.shutdownTrigger };

        while (true)
        {
            // only wait when the queue doesn't have enough items, otherwise keep busy and send data continuously
            if (this.circularBuffer.Count < this.MaxExportBatchSize)
            {
                try
                {
                    WaitHandle.WaitAny(triggers, this.ScheduledDelayMilliseconds);
                }
                catch (ObjectDisposedException)
                {
                    // the exporter is somehow disposed before the worker thread could finish its job
                    return;
                }
            }

            if (this.circularBuffer.Count > 0)
            {
                using (var batch = new Batch<T>(this.circularBuffer, this.MaxExportBatchSize))
                {
                    this.exporter.Export(batch);
                }

                try
                {
                    this.dataExportedNotification.Set();
                    this.dataExportedNotification.Reset();
                }
                catch (ObjectDisposedException)
                {
                    // the exporter is somehow disposed before the worker thread could finish its job
                    return;
                }
            }

            if (this.circularBuffer.RemovedCount >= Volatile.Read(ref this.shutdownDrainTarget))
            {
                return;
            }
        }
    }
}
