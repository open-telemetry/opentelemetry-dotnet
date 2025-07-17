// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
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
        int exportIntervalMilliseconds,
        int exportTimeoutMilliseconds)
        : this(exporter, exportIntervalMilliseconds, exportTimeoutMilliseconds, true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicExportingMetricReader"/> class.
    /// </summary>
    /// <param name="exporter">Exporter instance to export Metrics to.</param>
    /// <param name="exportIntervalMilliseconds">The interval in milliseconds between two consecutive exports. The default value is 60000.</param>
    /// <param name="exportTimeoutMilliseconds">How long the export can run before it is cancelled. The default value is 30000.</param>
    /// <param name="useThreads">Enables the use of <see cref="Thread" /> when true, <see cref="Task"/> when false.</param>
    public PeriodicExportingMetricReader(
        BaseExporter<Metric> exporter,
        int exportIntervalMilliseconds = DefaultExportIntervalMilliseconds,
        int exportTimeoutMilliseconds = DefaultExportTimeoutMilliseconds,
        bool useThreads = true)
        : base(exporter)
    {
        Guard.ThrowIfInvalidTimeout(exportIntervalMilliseconds);
        Guard.ThrowIfZero(exportIntervalMilliseconds);
        Guard.ThrowIfInvalidTimeout(exportTimeoutMilliseconds);

        if ((this.SupportedExportModes & ExportModes.Push) != ExportModes.Push)
        {
            throw new InvalidOperationException($"The '{nameof(exporter)}' does not support '{nameof(ExportModes)}.{nameof(ExportModes.Push)}'");
        }

        this.ExportIntervalMilliseconds = exportIntervalMilliseconds;
        this.ExportTimeoutMilliseconds = exportTimeoutMilliseconds;
        this.useThreads = useThreads;

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

        var sw = Stopwatch.StartNew();
        var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;
        return this.exporter.Shutdown((int)Math.Max(timeout, 0)) && result;
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

    // The pragma is required by the fact that this method is compiled on both .NET Framework and .NET Core, and the CA1859 warning is only relevant for .NET Framework.
    // The warning suggests changing the return type to PeriodicExportingMetricReaderThreadWorker for improved performance, but we want to keep the method signature consistent across platforms.
#pragma warning disable CA1859 // Change return type of method 'CreateWorker' from 'PeriodicExportingMetricReaderWorker' to 'PeriodicExportingMetricReaderThreadWorker' for improved performance

    private PeriodicExportingMetricReaderWorker CreateWorker()
#pragma warning restore CA1859
    {
#if NET
        // Use task-based worker for browser platform where threading may be limited
        if (OperatingSystem.IsBrowser() || !this.useThreads)
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
