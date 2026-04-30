// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Internal;

/// <summary>
/// Task-based implementation of periodic exporting metric reader worker for environments where threading may be limited.
/// </summary>
internal sealed class PeriodicExportingMetricReaderTaskWorker : PeriodicExportingMetricReaderWorker
{
    private readonly CancellationTokenSource cancellationTokenSource = new();
    private readonly SemaphoreSlim exportTrigger = new(0, 1);
    private readonly TaskCompletionSource<bool> shutdownCompletionSource = new();
    private Task? workerTask;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicExportingMetricReaderTaskWorker"/> class.
    /// </summary>
    /// <param name="metricReader">The metric reader instance.</param>
    /// <param name="exportIntervalMilliseconds">The interval in milliseconds between two consecutive exports.</param>
    /// <param name="exportTimeoutMilliseconds">How long the export can run before it is cancelled.</param>
    public PeriodicExportingMetricReaderTaskWorker(
        BaseExportingMetricReader metricReader,
        int exportIntervalMilliseconds,
        int exportTimeoutMilliseconds)
        : base(metricReader, exportIntervalMilliseconds, exportTimeoutMilliseconds)
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
    public override bool Shutdown(int timeoutMilliseconds)
    {
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

    private async Task ExporterProcAsync()
    {
        var cancellationToken = this.cancellationTokenSource.Token;
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).Ticks / TimeSpan.TicksPerMillisecond;
                var timeout = this.ExportIntervalMilliseconds - (int)(elapsedMilliseconds % this.ExportIntervalMilliseconds);

                Task? exportTriggerTask = null;
                Task? triggeredTask = null;

                try
                {
                    using var waitAndDelayCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    exportTriggerTask = this.exportTrigger.WaitAsync(waitAndDelayCts.Token);
                    triggeredTask = await Task.WhenAny(
                        exportTriggerTask,
                        Task.Delay(timeout, waitAndDelayCts.Token)).ConfigureAwait(false);
#if NET8_0_OR_GREATER
                    await waitAndDelayCts.CancelAsync().ConfigureAwait(false);
#else
                    waitAndDelayCts.Cancel();
#endif
                }
                catch (OperationCanceledException)
                {
                    // Continue to check if shutdown was requested
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    OpenTelemetrySdkEventSource.Log.MetricReaderEvent("PeriodicExportingMetricReader calling MetricReader.Collect because Shutdown was triggered.");
                    this.MetricReader.Collect(this.ExportTimeoutMilliseconds);
                    break;
                }

                // Check if the trigger was signaled by trying to acquire it with a timeout of 0
                var exportWasTriggered = triggeredTask != null && triggeredTask == exportTriggerTask;

                if (exportWasTriggered)
                {
                    OpenTelemetrySdkEventSource.Log.MetricReaderEvent("PeriodicExportingMetricReader calling MetricReader.Collect because Export was triggered.");
                }
                else
                {
                    OpenTelemetrySdkEventSource.Log.MetricReaderEvent("PeriodicExportingMetricReader calling MetricReader.Collect because the export interval has elapsed.");
                }

                this.MetricReader.Collect(this.ExportTimeoutMilliseconds);
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
