// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Internal;

/// <summary>
/// Thread-based implementation of periodic exporting metric reader worker.
/// </summary>
internal sealed class PeriodicExportingMetricReaderThreadWorker : PeriodicExportingMetricReaderWorker
{
    private readonly Thread exporterThread;
    private readonly AutoResetEvent exportTrigger = new(false);
    private readonly ManualResetEvent shutdownTrigger = new(false);
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicExportingMetricReaderThreadWorker"/> class.
    /// </summary>
    /// <param name="metricReader">The metric reader instance.</param>
    /// <param name="exportIntervalMilliseconds">The interval in milliseconds between two consecutive exports.</param>
    /// <param name="exportTimeoutMilliseconds">How long the export can run before it is cancelled.</param>
    public PeriodicExportingMetricReaderThreadWorker(
        BaseExportingMetricReader metricReader,
        int exportIntervalMilliseconds,
        int exportTimeoutMilliseconds)
        : base(metricReader, exportIntervalMilliseconds, exportTimeoutMilliseconds)
    {
        this.exporterThread = new Thread(this.ExporterProc)
        {
            IsBackground = true,
#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
            Name = $"OpenTelemetry-{nameof(PeriodicExportingMetricReader)}-{metricReader.GetType().Name}",
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
    public override bool Shutdown(int timeoutMilliseconds)
    {
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
        if (!this.disposed)
        {
            if (disposing)
            {
                this.exportTrigger.Dispose();
                this.shutdownTrigger.Dispose();
            }

            this.disposed = true;
        }

        base.Dispose(disposing);
    }

    private void ExporterProc()
    {
        int index;
        int timeout;
        var triggers = new WaitHandle[] { this.exportTrigger, this.shutdownTrigger };
        var sw = Stopwatch.StartNew();

        while (true)
        {
            timeout = (int)(this.exportIntervalMilliseconds - (sw.ElapsedMilliseconds % this.exportIntervalMilliseconds));

            try
            {
                index = WaitHandle.WaitAny(triggers, timeout);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            switch (index)
            {
                case 0: // export
                    OpenTelemetrySdkEventSource.Log.MetricReaderEvent("PeriodicExportingMetricReader calling MetricReader.Collect because Export was triggered.");
                    this.metricReader.Collect(this.exportTimeoutMilliseconds);
                    break;
                case 1: // shutdown
                    OpenTelemetrySdkEventSource.Log.MetricReaderEvent("PeriodicExportingMetricReader calling MetricReader.Collect because Shutdown was triggered.");
                    this.metricReader.Collect(this.exportTimeoutMilliseconds);
                    return;
                case WaitHandle.WaitTimeout: // timer
                    OpenTelemetrySdkEventSource.Log.MetricReaderEvent("PeriodicExportingMetricReader calling MetricReader.Collect because the export interval has elapsed.");
                    this.metricReader.Collect(this.exportTimeoutMilliseconds);
                    break;
            }
        }
    }
}
