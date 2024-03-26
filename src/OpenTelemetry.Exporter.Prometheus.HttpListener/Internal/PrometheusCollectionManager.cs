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
    private byte[] buffer = new byte[85000]; // encourage the object to live in LOH (large object heap)
    private int targetInfoBufferLength = -1; // zero or positive when target_info has been written for the first time
    private int globalLockState;
    private ArraySegment<byte> previousDataView;
    private DateTime? previousDataViewGeneratedAtUtc;
    private int readerCount;
    private bool collectionRunning;
    private TaskCompletionSource<CollectionResponse> collectionTcs;

    public PrometheusCollectionManager(PrometheusExporter exporter)
    {
        this.exporter = exporter;
        this.scrapeResponseCacheDurationMilliseconds = this.exporter.ScrapeResponseCacheDurationMilliseconds;
        this.onCollectRef = this.OnCollect;
        this.metricsCache = new Dictionary<Metric, PrometheusMetric>();
        this.scopes = new HashSet<string>();
    }

#if NET6_0_OR_GREATER
    public ValueTask<CollectionResponse> EnterCollect(bool openMetricsRequested)
#else
    public Task<CollectionResponse> EnterCollect(bool openMetricsRequested)
#endif
    {
        this.EnterGlobalLock();

        // If we are within {ScrapeResponseCacheDurationMilliseconds} of the
        // last successful collect, return the previous view.
        if (this.previousDataViewGeneratedAtUtc.HasValue
            && this.scrapeResponseCacheDurationMilliseconds > 0
            && this.previousDataViewGeneratedAtUtc.Value.AddMilliseconds(this.scrapeResponseCacheDurationMilliseconds) >= DateTime.UtcNow)
        {
            Interlocked.Increment(ref this.readerCount);
            this.ExitGlobalLock();
#if NET6_0_OR_GREATER
            return new ValueTask<CollectionResponse>(new CollectionResponse(this.previousDataView, this.previousDataViewGeneratedAtUtc.Value, fromCache: true));
#else
            return Task.FromResult(new CollectionResponse(this.previousDataView, this.previousDataViewGeneratedAtUtc.Value, fromCache: true));
#endif
        }

        // If a collection is already running, return a task to wait on the result.
        if (this.collectionRunning)
        {
            if (this.collectionTcs == null)
            {
                this.collectionTcs = new TaskCompletionSource<CollectionResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            Interlocked.Increment(ref this.readerCount);
            this.ExitGlobalLock();
#if NET6_0_OR_GREATER
            return new ValueTask<CollectionResponse>(this.collectionTcs.Task);
#else
            return this.collectionTcs.Task;
#endif
        }

        this.WaitForReadersToComplete();

        // Start a collection on the current thread.
        this.collectionRunning = true;
        this.previousDataViewGeneratedAtUtc = null;
        Interlocked.Increment(ref this.readerCount);
        this.ExitGlobalLock();

        CollectionResponse response;
        var result = this.ExecuteCollect(openMetricsRequested);
        if (result)
        {
            this.previousDataViewGeneratedAtUtc = DateTime.UtcNow;
            response = new CollectionResponse(this.previousDataView, this.previousDataViewGeneratedAtUtc.Value, fromCache: false);
        }
        else
        {
            response = default;
        }

        this.EnterGlobalLock();

        this.collectionRunning = false;

        if (this.collectionTcs != null)
        {
            this.collectionTcs.SetResult(response);
            this.collectionTcs = null;
        }

        this.ExitGlobalLock();

#if NET6_0_OR_GREATER
        return new ValueTask<CollectionResponse>(response);
#else
        return Task.FromResult(response);
#endif
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
            if (Interlocked.CompareExchange(ref this.readerCount, 0, this.readerCount) != 0)
            {
                readWait.SpinOnce();
                continue;
            }

            break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ExecuteCollect(bool openMetricsRequested)
    {
        this.exporter.OnExport = this.onCollectRef;
        this.exporter.OpenMetricsRequested = openMetricsRequested;
        var result = this.exporter.Collect(Timeout.Infinite);
        this.exporter.OnExport = null;
        return result;
    }

    private ExportResult OnCollect(Batch<Metric> metrics)
    {
        var cursor = 0;

        try
        {
            if (this.exporter.OpenMetricsRequested)
            {
                cursor = this.WriteTargetInfo();

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
                                cursor = PrometheusSerializer.WriteScopeInfo(this.buffer, cursor, metric.MeterName);

                                break;
                            }
                            catch (IndexOutOfRangeException)
                            {
                                if (!this.IncreaseBufferSize())
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
            }

            foreach (var metric in metrics)
            {
                if (!PrometheusSerializer.CanWriteMetric(metric))
                {
                    continue;
                }

                while (true)
                {
                    try
                    {
                        cursor = PrometheusSerializer.WriteMetric(
                            this.buffer,
                            cursor,
                            metric,
                            this.GetPrometheusMetric(metric),
                            this.exporter.OpenMetricsRequested);

                        break;
                    }
                    catch (IndexOutOfRangeException)
                    {
                        if (!this.IncreaseBufferSize())
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
                    cursor = PrometheusSerializer.WriteEof(this.buffer, cursor);
                    break;
                }
                catch (IndexOutOfRangeException)
                {
                    if (!this.IncreaseBufferSize())
                    {
                        throw;
                    }
                }
            }

            this.previousDataView = new ArraySegment<byte>(this.buffer, 0, cursor);
            return ExportResult.Success;
        }
        catch (Exception)
        {
            this.previousDataView = new ArraySegment<byte>(Array.Empty<byte>(), 0, 0);
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
                    this.targetInfoBufferLength = PrometheusSerializer.WriteTargetInfo(this.buffer, 0, this.exporter.Resource);

                    break;
                }
                catch (IndexOutOfRangeException)
                {
                    if (!this.IncreaseBufferSize())
                    {
                        throw;
                    }
                }
            }
        }

        return this.targetInfoBufferLength;
    }

    private bool IncreaseBufferSize()
    {
        var newBufferSize = this.buffer.Length * 2;

        if (newBufferSize > 100 * 1024 * 1024)
        {
            return false;
        }

        var newBuffer = new byte[newBufferSize];
        this.buffer.CopyTo(newBuffer, 0);
        this.buffer = newBuffer;

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
        public CollectionResponse(ArraySegment<byte> view, DateTime generatedAtUtc, bool fromCache)
        {
            this.View = view;
            this.GeneratedAtUtc = generatedAtUtc;
            this.FromCache = fromCache;
        }

        public ArraySegment<byte> View { get; }

        public DateTime GeneratedAtUtc { get; }

        public bool FromCache { get; }
    }
}
