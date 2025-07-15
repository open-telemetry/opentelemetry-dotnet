// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

/// <summary>
/// Abstract base class for batch export workers that handle the threading and synchronization logic for batch export processors.
/// </summary>
/// <typeparam name="T">The type of telemetry object to be exported.</typeparam>
internal abstract class BatchExportWorker<T> : IDisposable
    where T : class
{
    protected readonly CircularBuffer<T> circularBuffer;
    protected readonly BaseExporter<T> exporter;
    protected readonly int maxExportBatchSize;
    protected readonly int scheduledDelayMilliseconds;
    protected readonly int exporterTimeoutMilliseconds;

    private long shutdownDrainTarget = long.MaxValue;
    private long droppedCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchExportWorker{T}"/> class.
    /// </summary>
    /// <param name="circularBuffer">The circular buffer for storing telemetry objects.</param>
    /// <param name="exporter">The exporter instance.</param>
    /// <param name="maxExportBatchSize">The maximum batch size for exports.</param>
    /// <param name="scheduledDelayMilliseconds">The delay between exports in milliseconds.</param>
    /// <param name="exporterTimeoutMilliseconds">The timeout for export operations in milliseconds.</param>
    protected BatchExportWorker(
        CircularBuffer<T> circularBuffer,
        BaseExporter<T> exporter,
        int maxExportBatchSize,
        int scheduledDelayMilliseconds,
        int exporterTimeoutMilliseconds)
    {
        this.circularBuffer = circularBuffer;
        this.exporter = exporter;
        this.maxExportBatchSize = maxExportBatchSize;
        this.scheduledDelayMilliseconds = scheduledDelayMilliseconds;
        this.exporterTimeoutMilliseconds = exporterTimeoutMilliseconds;
    }

    /// <summary>
    /// Gets the number of telemetry objects dropped by the processor.
    /// </summary>
    internal long DroppedCount => Volatile.Read(ref this.droppedCount);

    /// <summary>
    /// Gets the shutdown drain target.
    /// </summary>
    protected long ShutdownDrainTarget => Volatile.Read(ref this.shutdownDrainTarget);

    /// <summary>
    /// Starts the worker.
    /// </summary>
    public abstract void Start();

    /// <summary>
    /// Triggers an export operation.
    /// </summary>
    /// <returns>True if the export was triggered successfully; otherwise, false.</returns>
    public abstract bool TriggerExport();

    /// <summary>
    /// Waits for export to complete.
    /// </summary>
    /// <param name="timeoutMilliseconds">The timeout in milliseconds.</param>
    /// <returns>True if the export completed within the timeout; otherwise, false.</returns>
    public abstract bool WaitForExport(int timeoutMilliseconds);

    /// <summary>
    /// Initiates shutdown and waits for completion.
    /// </summary>
    /// <param name="timeoutMilliseconds">The timeout in milliseconds.</param>
    /// <returns>True if shutdown completed within the timeout; otherwise, false.</returns>
    public abstract bool Shutdown(int timeoutMilliseconds);

    /// <summary>
    /// Increments the dropped count.
    /// </summary>
    public void IncrementDroppedCount()
    {
        Interlocked.Increment(ref this.droppedCount);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Sets the shutdown drain target.
    /// </summary>
    /// <param name="target">The target count.</param>
    protected void SetShutdownDrainTarget(long target)
    {
        Volatile.Write(ref this.shutdownDrainTarget, target);
    }

    /// <summary>
    /// Performs the export operation.
    /// </summary>
    protected void PerformExport()
    {
        if (this.circularBuffer.Count > 0)
        {
            using (var batch = new Batch<T>(this.circularBuffer, this.maxExportBatchSize))
            {
                this.exporter.Export(batch);
            }
        }
    }

    /// <summary>
    /// Checks if shutdown should occur.
    /// </summary>
    /// <returns>True if shutdown should occur; otherwise, false.</returns>
    protected bool ShouldShutdown()
    {
        return this.circularBuffer.RemovedCount >= this.ShutdownDrainTarget;
    }

    /// <summary>
    /// Releases the unmanaged resources used by this class and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
