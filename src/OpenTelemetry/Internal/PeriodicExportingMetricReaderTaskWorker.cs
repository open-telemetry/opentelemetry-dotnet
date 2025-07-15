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
    private volatile bool isShutdownRequested;
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
    {
        this.workerTask = Task.Factory.StartNew(
            this.ExporterProcAsync,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

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
        this.isShutdownRequested = true;

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

        if (timeoutMilliseconds == 0)
        {
            return true;
        }

        return this.workerTask.Wait(timeoutMilliseconds);
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
        var sw = Stopwatch.StartNew();

        try
        {
            while (!cancellationToken.IsCancellationRequested && !this.isShutdownRequested)
            {
                var timeout = (int)(this.exportIntervalMilliseconds - (sw.ElapsedMilliseconds % this.exportIntervalMilliseconds));

                try
                {
                    await Task.WhenAny(
                        this.exportTrigger.WaitAsync(cancellationToken),
                        Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (this.isShutdownRequested)
                    {
                        break;
                    }

                    // Continue to check if shutdown was requested
                }

                if (cancellationToken.IsCancellationRequested || this.isShutdownRequested)
                {
                    OpenTelemetrySdkEventSource.Log.MetricReaderEvent("PeriodicExportingMetricReader calling MetricReader.Collect because Shutdown was triggered.");
                    this.metricReader.Collect(this.exportTimeoutMilliseconds);
                    break;
                }

                // Check if the trigger was signaled by trying to acquire it with a timeout of 0
                var wasTriggered = await this.exportTrigger.WaitAsync(0, cancellationToken).ConfigureAwait(false);

                if (wasTriggered)
                {
                    OpenTelemetrySdkEventSource.Log.MetricReaderEvent("PeriodicExportingMetricReader calling MetricReader.Collect because Export was triggered.");
                }
                else
                {
                    OpenTelemetrySdkEventSource.Log.MetricReaderEvent("PeriodicExportingMetricReader calling MetricReader.Collect because the export interval has elapsed.");
                }

                this.metricReader.Collect(this.exportTimeoutMilliseconds);
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
