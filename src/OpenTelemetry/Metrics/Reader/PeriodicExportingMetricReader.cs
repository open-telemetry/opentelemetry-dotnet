// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    private Metric[]? exportBatchMetrics;

    // The Stopwatch timestamp after which no more exports are started, set
    // when the reader is shut down. long.MaxValue when there is no deadline.
    private long exportDeadlineTimestamp = long.MaxValue;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicExportingMetricReader"/> class.
    /// </summary>
    /// <param name="exporter">Exporter instance to export Metrics to.</param>
    /// <param name="exportIntervalMilliseconds">The interval in milliseconds between two consecutive exports. The default value is 60000.</param>
    /// <param name="exportTimeoutMilliseconds">How long the export can run before it is cancelled. The default value is 30000.</param>
    [SuppressMessage("roslyn", "RS0027", Justification = "The parameters of the overload which limits the size of the exported batches cannot be optional, as calls using the defaults of both overloads would be ambiguous.")]
    public PeriodicExportingMetricReader(
        BaseExporter<Metric> exporter,
        int exportIntervalMilliseconds = DefaultExportIntervalMilliseconds,
        int exportTimeoutMilliseconds = DefaultExportTimeoutMilliseconds)
        : this(exporter, null, exportIntervalMilliseconds, exportTimeoutMilliseconds)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicExportingMetricReader"/> class
    /// which limits the number of metric points in each batch it exports.
    /// </summary>
    /// <remarks>
    /// The metric points of each collection are split into batches of at most
    /// <paramref name="maxExportBatchSize"/> metric points, which may split up
    /// the metric points of a single metric into different batches. The batches
    /// are exported serially and <paramref name="exportTimeoutMilliseconds"/>
    /// applies to each export.
    /// </remarks>
    /// <param name="exporter">Exporter instance to export Metrics to.</param>
    /// <param name="exportIntervalMilliseconds">The interval in milliseconds between two consecutive exports.</param>
    /// <param name="exportTimeoutMilliseconds">How long each export can run before it is cancelled.</param>
    /// <param name="maxExportBatchSize">The maximum number of metric points in a batch provided to a single export.</param>
    public PeriodicExportingMetricReader(
        BaseExporter<Metric> exporter,
        int exportIntervalMilliseconds,
        int exportTimeoutMilliseconds,
        int maxExportBatchSize)
        : this(exporter, (int?)maxExportBatchSize, exportIntervalMilliseconds, exportTimeoutMilliseconds)
    {
    }

    private PeriodicExportingMetricReader(
        BaseExporter<Metric> exporter,
        int? maxExportBatchSize,
        int exportIntervalMilliseconds,
        int exportTimeoutMilliseconds)
        : base(exporter)
    {
        Guard.ThrowIfInvalidTimeout(exportIntervalMilliseconds);
        Guard.ThrowIfZero(exportIntervalMilliseconds);
        Guard.ThrowIfInvalidTimeout(exportTimeoutMilliseconds);

        if (maxExportBatchSize is { } size)
        {
            Guard.ThrowIfOutOfRange(size, nameof(maxExportBatchSize), min: 1);
        }

        if ((this.SupportedExportModes & ExportModes.Push) != ExportModes.Push)
        {
            throw new InvalidOperationException($"The '{nameof(exporter)}' does not support '{nameof(ExportModes)}.{nameof(ExportModes.Push)}'");
        }

        this.ExportIntervalMilliseconds = exportIntervalMilliseconds;
        this.ExportTimeoutMilliseconds = exportTimeoutMilliseconds;
        this.MaxExportBatchSize = maxExportBatchSize;

        this.worker = this.CreateWorker();
        this.worker.Start();
    }

    /// <summary>
    /// Gets the maximum number of metric points in a batch provided to a
    /// single export, or <see langword="null"/> if the number of metric points
    /// in a batch is not limited.
    /// </summary>
    public int? MaxExportBatchSize { get; }

    /// <inheritdoc/>
    internal override bool ProcessMetrics(in Batch<Metric> metrics, int timeoutMilliseconds)
    {
        if (this.MaxExportBatchSize is { } maxExportBatchSize &&
            CountMetricPoints(metrics) > maxExportBatchSize)
        {
            return this.ProcessMetricsInBatches(metrics, maxExportBatchSize, timeoutMilliseconds);
        }

        return this.ExportMetrics(metrics, timeoutMilliseconds);
    }

    /// <inheritdoc />
    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        long? timestamp = timeoutMilliseconds == Timeout.Infinite ? null : Stopwatch.GetTimestamp();

        if (timestamp is { } shutdownStartedAt)
        {
            // A collection still in progress only exports until the shutdown timeout elapses
            Volatile.Write(ref this.exportDeadlineTimestamp, shutdownStartedAt + (timeoutMilliseconds * Stopwatch.Frequency / 1000));
        }

        var result = this.worker.Shutdown(timeoutMilliseconds);

        // The worker has stopped, or the shutdown timeout has elapsed,
        // so no more exports are started once the exporter is shut down.
        Volatile.Write(ref this.exportDeadlineTimestamp, Stopwatch.GetTimestamp());

        if (timestamp is { } startedAt)
        {
            timeoutMilliseconds = Stopwatch.Remaining(timeoutMilliseconds, startedAt);
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

    private static long CountMetricPoints(in Batch<Metric> metrics)
    {
        long count = 0;

        foreach (var metric in metrics)
        {
            count += metric.AggregatorStore.SnapshotMetricPointCount;
        }

        return count;
    }

    private bool ExportMetrics(in Batch<Metric> metrics, int timeoutMilliseconds)
    {
        // The export timeout also applies to exports which are not started by the
        // worker, such as those of MeterProvider.ForceFlush(), when it is shorter.
        if (this.ExportTimeoutMilliseconds > 0 &&
            (timeoutMilliseconds == Timeout.Infinite || this.ExportTimeoutMilliseconds < timeoutMilliseconds))
        {
            timeoutMilliseconds = this.ExportTimeoutMilliseconds;
        }

        // A collection still in progress when the reader is shut down only
        // exports until the shutdown timeout elapses, and never once the
        // exporter has been shut down.
        var deadline = Volatile.Read(ref this.exportDeadlineTimestamp);

        if (deadline != long.MaxValue)
        {
            var remaining = deadline - Stopwatch.GetTimestamp();
            var remainingMilliseconds = remaining <= 0 ? 0 : (int)Math.Min(int.MaxValue, remaining * 1000 / Stopwatch.Frequency);

            if (remainingMilliseconds <= 0)
            {
                OpenTelemetrySdkEventSource.Log.MetricReaderEvent("PeriodicExportingMetricReader skipped exporting because the reader is shut down.");
                return false;
            }

            if (timeoutMilliseconds == Timeout.Infinite || remainingMilliseconds < timeoutMilliseconds)
            {
                timeoutMilliseconds = remainingMilliseconds;
            }
        }

        return base.ProcessMetrics(metrics, timeoutMilliseconds);
    }

    private bool ProcessMetricsInBatches(in Batch<Metric> metrics, int maxExportBatchSize, int timeoutMilliseconds)
    {
        long? startedAt = timeoutMilliseconds == Timeout.Infinite ? null : Stopwatch.GetTimestamp();

        // Each metric in a batch contributes at least one metric point to it,
        // so a batch never holds more metrics than either of these.
        var capacity = (int)Math.Min(maxExportBatchSize, metrics.Count);

        if (this.exportBatchMetrics == null || this.exportBatchMetrics.Length < capacity)
        {
            this.exportBatchMetrics = new Metric[capacity];
        }

        var batchMetrics = this.exportBatchMetrics;
        var batchMetricCount = 0;
        var batchMetricPointCount = 0;
        var result = true;

        try
        {
            foreach (var metric in metrics)
            {
                var aggregatorStore = metric.AggregatorStore;
                var metricPointCount = aggregatorStore.SnapshotMetricPointCount;

                // The metric points of a metric are split between batches when they
                // do not fit in the space left in the current batch. Only the last
                // metric of a batch can continue into the next one, and its window
                // is not moved until the batch it is in has been exported.
                for (var offset = 0; offset < metricPointCount;)
                {
                    var count = Math.Min(metricPointCount - offset, maxExportBatchSize - batchMetricPointCount);

                    aggregatorStore.SetExportWindow(offset, count);
                    batchMetrics[batchMetricCount++] = metric;

                    batchMetricPointCount += count;
                    offset += count;

                    if (batchMetricPointCount == maxExportBatchSize)
                    {
                        result = this.ExportBatch(batchMetrics, batchMetricCount, startedAt, timeoutMilliseconds) && result;
                        batchMetricCount = 0;
                        batchMetricPointCount = 0;
                    }
                }
            }

            if (batchMetricCount > 0)
            {
                result = this.ExportBatch(batchMetrics, batchMetricCount, startedAt, timeoutMilliseconds) && result;
            }
        }
        finally
        {
            foreach (var metric in metrics)
            {
                metric.AggregatorStore.ResetExportWindow();
            }

            Array.Clear(batchMetrics, 0, batchMetrics.Length);
        }

        return result;
    }

    private bool ExportBatch(Metric[] metrics, int count, long? startedAt, int timeoutMilliseconds)
    {
        if (startedAt is { } timestamp)
        {
            timeoutMilliseconds = Stopwatch.Remaining(timeoutMilliseconds, timestamp);

            if (timeoutMilliseconds <= 0)
            {
                OpenTelemetrySdkEventSource.Log.MetricReaderEvent("PeriodicExportingMetricReader skipped exporting a batch because the timeout period has elapsed.");
                return false;
            }
        }

        return this.ExportMetrics(new Batch<Metric>(metrics, count), timeoutMilliseconds);
    }

#pragma warning disable CA1859 // Change return type of method 'CreateWorker' from 'PeriodicExportingMetricReaderWorker' to 'PeriodicExportingMetricReaderThreadWorker' for improved performance

    private PeriodicExportingMetricReaderWorker CreateWorker()
#pragma warning restore CA1859
    {
#if NET
        // Use task-based worker for browser platform where threading may be limited
        if (!ThreadingHelper.IsThreadingEnabled())
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
