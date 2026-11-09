// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Metrics;

namespace OpenTelemetry.Internal;

/// <summary>
/// Abstract base class for periodic exporting metric reader workers that handle the threading and synchronization logic.
/// </summary>
internal abstract class PeriodicExportingMetricReaderWorker : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicExportingMetricReaderWorker"/> class.
    /// </summary>
    /// <param name="metricReader">The metric reader instance.</param>
    /// <param name="exportIntervalMilliseconds">The interval in milliseconds between two consecutive exports.</param>
    /// <param name="exportTimeoutMilliseconds">How long the export can run before it is cancelled.</param>
    protected PeriodicExportingMetricReaderWorker(
        BaseExportingMetricReader metricReader,
        int exportIntervalMilliseconds,
        int exportTimeoutMilliseconds)
    {
        this.MetricReader = metricReader;
        this.ExportIntervalMilliseconds = exportIntervalMilliseconds;
        this.ExportTimeoutMilliseconds = exportTimeoutMilliseconds;

        // When the metric reader limits the size of the batches it exports, the
        // export timeout applies to each of the exports of a collection, which the
        // metric reader enforces, instead of to the whole collection.
        this.CollectTimeoutMilliseconds = metricReader is PeriodicExportingMetricReader { MaxExportBatchSize: not null }
            ? Timeout.Infinite
            : exportTimeoutMilliseconds;
    }

    ~PeriodicExportingMetricReaderWorker()
    {
        // Finalizer to ensure resources are cleaned up if Dispose is not called
        this.Dispose(false);
    }

    /// <summary>
    /// Gets the metric reader instance.
    /// </summary>
    protected BaseExportingMetricReader MetricReader { get; }

    /// <summary>
    /// Gets he interval in milliseconds between two consecutive exports.
    /// </summary>
    protected int ExportIntervalMilliseconds { get; }

    /// <summary>
    /// Gets how long the export can run before it is cancelled.
    /// </summary>
    protected int ExportTimeoutMilliseconds { get; }

    /// <summary>
    /// Gets the timeout in milliseconds of the collections started by the worker.
    /// </summary>
    protected int CollectTimeoutMilliseconds { get; }

    /// <summary>
    /// Starts the worker.
    /// </summary>
    public abstract void Start();

    /// <summary>
    /// Triggers an export operation.
    /// </summary>
    /// <returns><see langword="true"/> if the shutdown completed within the timeout; otherwise, <see langword="false"/>.</returns>
    public abstract bool TriggerExport();

    /// <summary>
    /// Initiates shutdown and waits for completion.
    /// </summary>
    /// <param name="timeoutMilliseconds">The timeout in milliseconds.</param>
    /// <returns>True if the shutdown completed within the timeout; otherwise, false.</returns>
    public abstract bool Shutdown(int timeoutMilliseconds);

    /// <summary>
    /// Disposes of the worker and its resources.
    /// </summary>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by this class and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
    }
}
