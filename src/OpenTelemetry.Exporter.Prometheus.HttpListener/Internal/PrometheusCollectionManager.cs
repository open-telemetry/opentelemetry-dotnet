// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus;

internal sealed class PrometheusCollectionManager
{
    private const int MaxCachedMetrics = 1024;

    private readonly PrometheusExporter exporter;
    private readonly int scrapeResponseCacheDurationMilliseconds;
    private readonly Func<Batch<Metric>, ExportResult> onCollectRef;
    private readonly Dictionary<Metric, PrometheusMetric> metricsCache;
    private readonly HashSet<string> scopes;
    private int metricsCacheCount;
    private byte[] plainTextBuffer = new byte[85000]; // encourage the object to live in LOH (large object heap)
    private byte[] openMetricsBuffer = new byte[85000]; // encourage the object to live in LOH (large object heap)
    private int targetInfoBufferLength = -1; // zero or positive when target_info has been written for the first time
    private ArraySegment<byte> previousPlainTextDataView;
    private ArraySegment<byte> previousOpenMetricsDataView;
    private volatile int globalLockState;
    private volatile int readerCount;
    private volatile TaskCompletionSource<CollectionResponse> collectionTcs;

    public PrometheusCollectionManager(PrometheusExporter exporter)
    {
        this.exporter = exporter;
        this.scrapeResponseCacheDurationMilliseconds = this.exporter.ScrapeResponseCacheDurationMilliseconds;
        this.onCollectRef = this.OnCollect;
        this.metricsCache = new Dictionary<Metric, PrometheusMetric>();
        this.scopes = new HashSet<string>();

        this.collectionTcs = new TaskCompletionSource<CollectionResponse>();
        this.collectionTcs.SetResult(default);
    }

#if NET6_0_OR_GREATER
    public (ValueTask<CollectionResponse> Response, bool FromCache) EnterCollect()
#else
    public (Task<CollectionResponse> Response, bool FromCache) EnterCollect()
#endif
    {
        this.EnterGlobalLock();
        var tcs = this.collectionTcs;

        if (tcs.Task.IsCompleted)
        {
            if (this.scrapeResponseCacheDurationMilliseconds > 0
                && tcs.Task.Result.GeneratedAtUtc.AddMilliseconds(this.scrapeResponseCacheDurationMilliseconds) >= DateTime.UtcNow)
            {
                Interlocked.Increment(ref this.readerCount);
                this.ExitGlobalLock();
#if NET6_0_OR_GREATER
                return (new ValueTask<CollectionResponse>(tcs.Task), true);
#else
                return (tcs.Task, true);
#endif
            }
        }
        else
        {
            Interlocked.Increment(ref this.readerCount);
            this.ExitGlobalLock();
#if NET6_0_OR_GREATER
            return (new ValueTask<CollectionResponse>(tcs.Task), false);
#else
            return (tcs.Task, false);
#endif
        }

        this.WaitForReadersToComplete();
        Interlocked.Increment(ref this.readerCount);
        var newTcs = new TaskCompletionSource<CollectionResponse>();
        SpinWait spinWait = default;

        while (true)
        {
            if (Interlocked.CompareExchange(ref this.collectionTcs, newTcs, tcs) == tcs)
            {
                Task.Factory.StartNew(this.ExecuteCollect, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                this.ExitGlobalLock();
#if NET6_0_OR_GREATER
                return (new ValueTask<CollectionResponse>(newTcs.Task), false);
#else
                return (newTcs.Task, false);
#endif
            }

            spinWait.SpinOnce();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitCollect()
    {
        Interlocked.Decrement(ref this.readerCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnterGlobalLock()
    {
        SpinWait lockWait = default;
        while (true)
        {
            if (Interlocked.CompareExchange(ref this.globalLockState, 1, this.globalLockState) != 0)
            {
                lockWait.SpinOnce();
                continue;
            }

            break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExitGlobalLock()
    {
        Interlocked.Exchange(ref this.globalLockState, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WaitForReadersToComplete()
    {
        SpinWait readWait = default;
        while (true)
        {
            if (Interlocked.CompareExchange(ref this.readerCount, 0, 0) != 0)
            {
                readWait.SpinOnce();
                continue;
            }

            break;
        }
    }

    private void ExecuteCollect()
    {
        this.exporter.OnExport = this.onCollectRef;
        var result = this.exporter.Collect(Timeout.Infinite);
        this.exporter.OnExport = null;

        CollectionResponse response;

        if (result)
        {
            response = new CollectionResponse(this.previousOpenMetricsDataView, this.previousPlainTextDataView, DateTime.UtcNow);
        }
        else
        {
            response = default;
        }

        // Set the result and notify all waiting readers.
        // We are not calling `tcs.TrySetResult(response)` directly here
        // because we don't want any continuation to be run inlined on the current thread.
        var tcs = this.collectionTcs;
        Task.Factory.StartNew(s => ((TaskCompletionSource<CollectionResponse>)s!).TrySetResult(response), tcs, CancellationToken.None, TaskCreationOptions.PreferFairness, TaskScheduler.Default);
        tcs.Task.Wait();
    }

    private ExportResult OnCollect(Batch<Metric> metrics)
    {
        try
        {
            var openMetricsCursor = this.WriteTargetInfo();
            var plainTextCursor = 0;

            this.scopes.Clear();

            foreach (var metric in metrics)
            {
                if (!PrometheusSerializer.CanWriteMetric(metric))
                {
                    continue;
                }

                if (this.scopes.Add(metric.MeterName))
                {
                    while (true)
                    {
                        try
                        {
                            openMetricsCursor = PrometheusSerializer.WriteScopeInfo(this.openMetricsBuffer, openMetricsCursor, metric.MeterName);

                            break;
                        }
                        catch (IndexOutOfRangeException)
                        {
                            if (!this.IncreaseBufferSize(ref this.openMetricsBuffer))
                            {
                                // there are two cases we might run into the following condition:
                                // 1. we have many metrics to be exported - in this case we probably want
                                //    to put some upper limit and allow the user to configure it.
                                // 2. we got an IndexOutOfRangeException which was triggered by some other
                                //    code instead of the buffer[cursor++] - in this case we should give up
                                //    at certain point rather than allocating like crazy.
                                throw;
                            }
                        }
                    }
                }
            }

            // TODO: caching the response based on the request type on demand,
            // instead of always caching two responses regardless the request type

            foreach (var metric in metrics)
            {
                if (!PrometheusSerializer.CanWriteMetric(metric))
                {
                    continue;
                }

                var prometheusMetric = this.GetPrometheusMetric(metric);

                while (true)
                {
                    try
                    {
                        openMetricsCursor = PrometheusSerializer.WriteMetric(
                            this.openMetricsBuffer,
                            openMetricsCursor,
                            metric,
                            prometheusMetric,
                            true);

                        break;
                    }
                    catch (IndexOutOfRangeException)
                    {
                        if (!this.IncreaseBufferSize(ref this.openMetricsBuffer))
                        {
                            throw;
                        }
                    }
                }

                while (true)
                {
                    try
                    {
                        plainTextCursor = PrometheusSerializer.WriteMetric(
                            this.plainTextBuffer,
                            plainTextCursor,
                            metric,
                            prometheusMetric,
                            false);

                        break;
                    }
                    catch (IndexOutOfRangeException)
                    {
                        if (!this.IncreaseBufferSize(ref this.plainTextBuffer))
                        {
                            throw;
                        }
                    }
                }
            }

            while (true)
            {
                try
                {
                    openMetricsCursor = PrometheusSerializer.WriteEof(this.openMetricsBuffer, openMetricsCursor);
                    break;
                }
                catch (IndexOutOfRangeException)
                {
                    if (!this.IncreaseBufferSize(ref this.openMetricsBuffer))
                    {
                        throw;
                    }
                }
            }

            while (true)
            {
                try
                {
                    plainTextCursor = PrometheusSerializer.WriteEof(this.plainTextBuffer, plainTextCursor);
                    break;
                }
                catch (IndexOutOfRangeException)
                {
                    if (!this.IncreaseBufferSize(ref this.plainTextBuffer))
                    {
                        throw;
                    }
                }
            }

            this.previousOpenMetricsDataView = new ArraySegment<byte>(this.openMetricsBuffer, 0, openMetricsCursor);
            this.previousPlainTextDataView = new ArraySegment<byte>(this.plainTextBuffer, 0, plainTextCursor);
            return ExportResult.Success;
        }
        catch (Exception)
        {
            this.previousOpenMetricsDataView = this.previousPlainTextDataView = new ArraySegment<byte>(Array.Empty<byte>(), 0, 0);
            return ExportResult.Failure;
        }
    }

    private int WriteTargetInfo()
    {
        if (this.targetInfoBufferLength < 0)
        {
            while (true)
            {
                try
                {
                    this.targetInfoBufferLength = PrometheusSerializer.WriteTargetInfo(this.openMetricsBuffer, 0, this.exporter.Resource);

                    break;
                }
                catch (IndexOutOfRangeException)
                {
                    if (!this.IncreaseBufferSize(ref this.openMetricsBuffer))
                    {
                        throw;
                    }
                }
            }
        }

        return this.targetInfoBufferLength;
    }

    private bool IncreaseBufferSize(ref byte[] buffer)
    {
        var newBufferSize = buffer.Length * 2;

        if (newBufferSize > 100 * 1024 * 1024)
        {
            return false;
        }

        var newBuffer = new byte[newBufferSize];
        buffer.CopyTo(newBuffer, 0);
        buffer = newBuffer;

        return true;
    }

    private PrometheusMetric GetPrometheusMetric(Metric metric)
    {
        // Optimize writing metrics with bounded cache that has pre-calculated Prometheus names.
        if (!this.metricsCache.TryGetValue(metric, out var prometheusMetric))
        {
            prometheusMetric = PrometheusMetric.Create(metric, this.exporter.DisableTotalNameSuffixForCounters);

            // Add to the cache if there is space.
            if (this.metricsCacheCount < MaxCachedMetrics)
            {
                this.metricsCache[metric] = prometheusMetric;
                this.metricsCacheCount++;
            }
        }

        return prometheusMetric;
    }

    public readonly struct CollectionResponse
    {
        public CollectionResponse(ArraySegment<byte> openMetricsView, ArraySegment<byte> plainTextView, DateTime generatedAtUtc)
        {
            this.OpenMetricsView = openMetricsView;
            this.PlainTextView = plainTextView;
            this.GeneratedAtUtc = generatedAtUtc;
        }

        public ArraySegment<byte> OpenMetricsView { get; }

        public ArraySegment<byte> PlainTextView { get; }

        public DateTime GeneratedAtUtc { get; }
    }
}
