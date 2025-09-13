// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// MetricReader implementation which collects metrics based on
/// a user-configurable time interval and passes the metrics to
/// the configured MetricExporter.
/// </summary>
public class PeriodicExportingMetricReader : BaseExportingMetricReader
{
    internal const int DefaultExportIntervalMilliseconds = 60000;
    internal const int DefaultExportTimeoutMilliseconds = 30000;

    internal readonly int ExportIntervalMilliseconds;
    internal readonly int ExportTimeoutMilliseconds;
    private readonly PeriodicExportingMetricReaderWorker worker;
    private readonly bool useThreads;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicExportingMetricReader"/> class.
    /// </summary>
    /// <param name="exporter">Exporter instance to export Metrics to.</param>
    /// <param name="exportIntervalMilliseconds">The interval in milliseconds between two consecutive exports. The default value is 60000.</param>
    /// <param name="exportTimeoutMilliseconds">How long the export can run before it is cancelled. The default value is 30000.</param>
    public PeriodicExportingMetricReader(
        BaseExporter<Metric> exporter,
        int exportIntervalMilliseconds = DefaultExportIntervalMilliseconds,
        int exportTimeoutMilliseconds = DefaultExportTimeoutMilliseconds)
        : this(exporter, new PeriodicExportingMetricReaderOptions
        {
            ExportIntervalMilliseconds = exportIntervalMilliseconds,
            ExportTimeoutMilliseconds = exportTimeoutMilliseconds,
            UseThreads = true,
        })
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicExportingMetricReader"/> class.
    /// </summary>
    /// <param name="exporter">Exporter instance to export Metrics to.</param>
    /// <param name="options">Configuration options for the periodic exporting metric reader.</param>
    public PeriodicExportingMetricReader(
        BaseExporter<Metric> exporter,
        PeriodicExportingMetricReaderOptions options)
        : base(exporter)
    {
        Guard.ThrowIfNull(options);

        var exportIntervalMilliseconds = options?.ExportIntervalMilliseconds ?? DefaultExportIntervalMilliseconds;
        var exportTimeoutMilliseconds = options?.ExportTimeoutMilliseconds ?? DefaultExportTimeoutMilliseconds;

        Guard.ThrowIfInvalidTimeout(exportIntervalMilliseconds);
        Guard.ThrowIfZero(exportIntervalMilliseconds);
        Guard.ThrowIfInvalidTimeout(exportTimeoutMilliseconds);

        if ((this.SupportedExportModes & ExportModes.Push) != ExportModes.Push)
        {
            throw new InvalidOperationException($"The '{nameof(exporter)}' does not support '{nameof(ExportModes)}.{nameof(ExportModes.Push)}'");
        }

        this.ExportIntervalMilliseconds = exportIntervalMilliseconds;
        this.ExportTimeoutMilliseconds = exportTimeoutMilliseconds;
        this.useThreads = options?.UseThreads ?? false;

        this.worker = this.CreateWorker();
        this.worker.Start();
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        var result = this.worker.Shutdown(timeoutMilliseconds);

        if (timeoutMilliseconds == Timeout.Infinite)
        {
            return this.exporter.Shutdown() && result;
        }

        if (timeoutMilliseconds == 0)
        {
            return this.exporter.Shutdown(0) && result;
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

#pragma warning disable CA1859 // Change return type of method 'CreateWorker' from 'PeriodicExportingMetricReaderWorker' to 'PeriodicExportingMetricReaderThreadWorker' for improved performance

    private PeriodicExportingMetricReaderWorker CreateWorker()
#pragma warning restore CA1859
    {
#if NET
        // Use task-based worker for browser platform where threading may be limited
        if (ThreadingHelper.IsThreadingDisabled() || !this.useThreads)
        {
            return new PeriodicExportingMetricReaderTaskWorker(
                this,
                this.ExportIntervalMilliseconds,
                this.ExportTimeoutMilliseconds);
        }
#endif

        // Use thread-based worker for all other platforms
        return new PeriodicExportingMetricReaderThreadWorker(
            this,
            this.ExportIntervalMilliseconds,
            this.ExportTimeoutMilliseconds);
    }
}
