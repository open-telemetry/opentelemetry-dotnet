// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Internal;

/// <summary>
/// Task-based implementation of batch export worker for environments where threading may be limited.
/// </summary>
/// <typeparam name="T">The type of telemetry object to be exported.</typeparam>
internal sealed class BatchExportTaskWorker<T> : BatchExportWorker<T>
    where T : class
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly SemaphoreSlim exportTrigger = new(0, 1);
    private readonly TaskCompletionSource<bool> shutdownCompletionSource = new();
    private volatile TaskCompletionSource<bool> dataExportedNotification = new();
    private Task? workerTask;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="BatchExportTaskWorker{T}"/> class.
    /// </summary>
    /// <param name="circularBuffer">The circular buffer for storing telemetry objects.</param>
    /// <param name="exporter">The exporter instance.</param>
    /// <param name="maxExportBatchSize">The maximum batch size for exports.</param>
    /// <param name="scheduledDelayMilliseconds">The delay between exports in milliseconds.</param>
    /// <param name="exporterTimeoutMilliseconds">The timeout for export operations in milliseconds.</param>
    public BatchExportTaskWorker(
        CircularBuffer<T> circularBuffer,
        BaseExporter<T> exporter,
        int maxExportBatchSize,
        int scheduledDelayMilliseconds,
        int exporterTimeoutMilliseconds)
        : base(circularBuffer, exporter, maxExportBatchSize, scheduledDelayMilliseconds, exporterTimeoutMilliseconds)
    {
    }

    /// <inheritdoc/>
    public override void Start()
        => this.workerTask = Task.Run(this.ExporterProcAsync);

    /// <inheritdoc/>
    public override bool TriggerExport()
    {
        if (this.cancellationTokenSource.IsCancellationRequested)
        {
            return false;
        }

        try
        {
            this.exportTrigger.Release();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch (SemaphoreFullException)
        {
            // Semaphore is already signaled, export is pending
            return true;
        }
    }

    /// <inheritdoc/>
    public override bool WaitForExport(int timeoutMilliseconds)
    {
        var tail = this.CircularBuffer.RemovedCount;
        var head = this.CircularBuffer.AddedCount;

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

        // On Wasm (single-threaded), calling .GetAwaiter().GetResult() would deadlock
        // because there is no second thread to complete the async work. Instead, just
        // trigger the export and let the async worker loop drain the buffer. Wasm apps
        // don't have a real exit path so there is no risk of data loss.
        if (ThreadingHelper.IsThreadingDisabled())
        {
            return true;
        }

        // Otherwise we can just wait for the export to complete synchronously
        return this.WaitForExportAsync(timeoutMilliseconds, head).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public override bool Shutdown(int timeoutMilliseconds)
    {
        this.SetShutdownDrainTarget(this.CircularBuffer.AddedCount);

        try
        {
            this.cancellationTokenSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }

        if (this.workerTask == null)
        {
            return true;
        }

        if (timeoutMilliseconds == Timeout.Infinite)
        {
            this.workerTask.Wait();
            return true;
        }

        return timeoutMilliseconds == 0 || this.workerTask.Wait(timeoutMilliseconds);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                this.cancellationTokenSource.Dispose();
                this.exportTrigger.Dispose();
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }

    private async Task<bool> WaitForExportAsync(int timeoutMilliseconds, long targetHead)
    {
        long? timestamp = timeoutMilliseconds == Timeout.Infinite ? null : Stopwatch.GetTimestamp();

        // There is a chance that the export task finished processing all the data from the queue,
        // and signaled before we enter wait here, use polling to prevent being blocked indefinitely.
        const int pollingMilliseconds = 1000;

        while (true)
        {
            var timeout = pollingMilliseconds;

            if (timestamp is { } startedAt)
            {
                timeoutMilliseconds = Stopwatch.Remaining(timeoutMilliseconds, startedAt);

                if (timeoutMilliseconds <= 0)
                {
                    return this.CircularBuffer.RemovedCount >= targetHead;
                }
            }

            try
            {
                using var cts = new CancellationTokenSource(timeout);
                using var combinedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cts.Token,
                    this.cancellationTokenSource.Token);

                await Task.WhenAny(
                    this.dataExportedNotification.Task,
                    this.shutdownCompletionSource.Task,
                    Task.Delay(timeout, combinedTokenSource.Token)).ConfigureAwait(false);
#if NET
                await combinedTokenSource.CancelAsync().ConfigureAwait(false);
#else
                combinedTokenSource.Cancel();
#endif
            }
            catch (ObjectDisposedException)
            {
                return false; // The worker has been disposed
            }
            catch (OperationCanceledException)
            {
                // Expected when timeout or shutdown occurs
            }

            if (this.CircularBuffer.RemovedCount >= targetHead)
            {
                return true;
            }

            if (this.ShutdownDrainTarget != long.MaxValue)
            {
                return false;
            }
        }
    }

    private async Task ExporterProcAsync()
    {
        var cancellationToken = this.cancellationTokenSource.Token;

        try
        {
            while (true)
            {
                // only wait when the queue doesn't have enough items, otherwise keep busy and send data continuously
                if (this.CircularBuffer.Count < this.MaxExportBatchSize)
                {
                    try
                    {
                        using var waitAndDelayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        await Task.WhenAny(
                            this.exportTrigger.WaitAsync(waitAndDelayCts.Token),
                            Task.Delay(this.ScheduledDelayMilliseconds, waitAndDelayCts.Token)).ConfigureAwait(false);
#if NET
                        await waitAndDelayCts.CancelAsync().ConfigureAwait(false);
#else
                        waitAndDelayCts.Cancel();
#endif
                    }
                    catch (OperationCanceledException)
                    {
                        // Continue to check if there's data to export before exiting
                    }
                    catch (ObjectDisposedException)
                    {
                        // the exporter is somehow disposed before the worker thread could finish its job
                        return;
                    }
                }

                this.PerformExport();

                // Signal that data has been exported
                var previousTcs = this.dataExportedNotification;
                var newTcs = new TaskCompletionSource<bool>();
                if (Interlocked.CompareExchange(ref this.dataExportedNotification, newTcs, previousTcs) == previousTcs)
                {
                    previousTcs.TrySetResult(true);
                }

                if (this.ShouldShutdown() || cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            // Log the exception if needed
            OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.ExporterProcAsync), ex);
        }
        finally
        {
            this.shutdownCompletionSource.TrySetResult(true);
        }
    }
}
