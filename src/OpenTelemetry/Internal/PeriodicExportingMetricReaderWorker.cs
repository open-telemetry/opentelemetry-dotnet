// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Metrics;

namespace OpenTelemetry.Internal;

/// <summary>
/// Abstract base class for periodic exporting metric reader workers that handle the threading and synchronization logic.
/// </summary>
internal abstract class PeriodicExportingMetricReaderWorker : IDisposable
{
    protected readonly BaseExportingMetricReader metricReader;
    protected readonly int exportIntervalMilliseconds;
    protected readonly int exportTimeoutMilliseconds;

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
        this.metricReader = metricReader;
        this.exportIntervalMilliseconds = exportIntervalMilliseconds;
        this.exportTimeoutMilliseconds = exportTimeoutMilliseconds;
    }

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
