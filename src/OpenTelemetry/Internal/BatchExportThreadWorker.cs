// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Internal;

/// <summary>
/// Thread-based implementation of batch export worker.
/// </summary>
/// <typeparam name="T">The type of telemetry object to be exported.</typeparam>
internal sealed class BatchExportThreadWorker<T> : BatchExportWorker<T>
    where T : class
{
    private readonly Thread exporterThread;
    private readonly AutoResetEvent exportTrigger = new(false);
    private readonly ManualResetEvent dataExportedNotification = new(false);
    private readonly ManualResetEvent shutdownTrigger = new(false);
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchExportThreadWorker{T}"/> class.
    /// </summary>
    /// <param name="circularBuffer">The circular buffer for storing telemetry objects.</param>
    /// <param name="exporter">The exporter instance.</param>
    /// <param name="maxExportBatchSize">The maximum batch size for exports.</param>
    /// <param name="scheduledDelayMilliseconds">The delay between exports in milliseconds.</param>
    /// <param name="exporterTimeoutMilliseconds">The timeout for export operations in milliseconds.</param>
    public BatchExportThreadWorker(
        CircularBuffer<T> circularBuffer,
        BaseExporter<T> exporter,
        int maxExportBatchSize,
        int scheduledDelayMilliseconds,
        int exporterTimeoutMilliseconds)
        : base(circularBuffer, exporter, maxExportBatchSize, scheduledDelayMilliseconds, exporterTimeoutMilliseconds)
    {
        this.exporterThread = new Thread(this.ExporterProc)
        {
            IsBackground = true,
#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
            Name = $"OpenTelemetry-{nameof(BatchExportProcessor<T>)}-{exporter.GetType().Name}",
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1
        };
    }

    /// <inheritdoc/>
    public override void Start()
    {
        this.exporterThread.Start();
    }

    /// <inheritdoc/>
    public override bool TriggerExport()
    {
        try
        {
            this.exportTrigger.Set();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public override bool WaitForExport(int timeoutMilliseconds)
    {
        var tail = this.circularBuffer.RemovedCount;
        var head = this.circularBuffer.AddedCount;

        if (head == tail)
        {
            return true; // nothing to flush
        }

        if (!this.TriggerExport())
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

            if (this.ShutdownDrainTarget != long.MaxValue)
            {
                return false;
            }
        }
    }

    /// <inheritdoc/>
    public override bool Shutdown(int timeoutMilliseconds)
    {
        this.SetShutdownDrainTarget(this.circularBuffer.AddedCount);

        try
        {
            this.shutdownTrigger.Set();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        if (timeoutMilliseconds == Timeout.Infinite)
        {
            this.exporterThread.Join();
            return true;
        }

        if (timeoutMilliseconds == 0)
        {
            return true;
        }

        return this.exporterThread.Join(timeoutMilliseconds);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!this.disposed)
        {
            this.disposed = true;

            this.exportTrigger.Dispose();
            this.dataExportedNotification.Dispose();
            this.shutdownTrigger.Dispose();
        }
    }

    private void ExporterProc()
    {
        var triggers = new WaitHandle[] { this.exportTrigger, this.shutdownTrigger };

        while (true)
        {
            // only wait when the queue doesn't have enough items, otherwise keep busy and send data continuously
            if (this.circularBuffer.Count < this.maxExportBatchSize)
            {
                try
                {
                    WaitHandle.WaitAny(triggers, this.scheduledDelayMilliseconds);
                }
                catch (ObjectDisposedException)
                {
                    // the exporter is somehow disposed before the worker thread could finish its job
                    return;
                }
            }

            this.PerformExport();

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

            if (this.ShouldShutdown())
            {
                return;
            }
        }
    }
}
