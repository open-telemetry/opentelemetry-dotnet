// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus;

internal sealed class PrometheusCollectionManager
{
    private const int MaxCachedMetrics = 1024;

    private readonly PrometheusExporter exporter;
    private readonly int scrapeResponseCacheDurationMilliseconds;
    private readonly PrometheusExporter.ExportFunc onCollectRef;
    private readonly Dictionary<Metric, PrometheusMetric> metricsCache;
    private readonly HashSet<string> scopes;
    private readonly SemaphoreSlim globalLockState;
    private int metricsCacheCount;
    private byte[] plainTextBuffer = new byte[85000]; // encourage the object to live in LOH (large object heap)
    private byte[] openMetricsBuffer = new byte[85000]; // encourage the object to live in LOH (large object heap)
    private int targetInfoBufferLength = -1; // zero or positive when target_info has been written for the first time
    private ArraySegment<byte> previousPlainTextDataView;
    private ArraySegment<byte> previousOpenMetricsDataView;
    private DateTime? previousPlainTextDataViewGeneratedAtUtc;
    private DateTime? previousOpenMetricsDataViewGeneratedAtUtc;
    private int readerCount;
    private bool collectionRunning;
    private TaskCompletionSource<CollectionResponse>? collectionTcs;

    public PrometheusCollectionManager(PrometheusExporter exporter)
    {
        this.exporter = exporter;
        this.scrapeResponseCacheDurationMilliseconds = this.exporter.ScrapeResponseCacheDurationMilliseconds;
        this.onCollectRef = this.OnCollect;
        this.metricsCache = [];
        this.scopes = [];
        this.globalLockState = new(1, 1);
    }

    internal Func<DateTime> UtcNow { get; set; } = static () => DateTime.UtcNow;

#if NET
    public ValueTask<CollectionResponse> EnterCollect(bool openMetricsRequested)
#else
    public Task<CollectionResponse> EnterCollect(bool openMetricsRequested)
#endif
    {
        this.EnterGlobalLock();

        DateTime? previousDataViewGeneratedAtUtc;

        try
        {
            // If we are within {ScrapeResponseCacheDurationMilliseconds} of the
            // last successful collect, return the previous view.
            previousDataViewGeneratedAtUtc = openMetricsRequested
                ? this.previousOpenMetricsDataViewGeneratedAtUtc
                : this.previousPlainTextDataViewGeneratedAtUtc;

            if (previousDataViewGeneratedAtUtc.HasValue
                && this.scrapeResponseCacheDurationMilliseconds > 0
                && previousDataViewGeneratedAtUtc.Value.AddMilliseconds(this.scrapeResponseCacheDurationMilliseconds) >= this.UtcNow())
            {
#if NET
                return new ValueTask<CollectionResponse>(new CollectionResponse(this.previousOpenMetricsDataView, this.previousPlainTextDataView, previousDataViewGeneratedAtUtc.Value, fromCache: true));
#else
                return Task.FromResult(new CollectionResponse(this.previousOpenMetricsDataView, this.previousPlainTextDataView, previousDataViewGeneratedAtUtc.Value, fromCache: true));
#endif
            }

            // If a collection is already running, return a task to wait on the result.
            if (this.collectionRunning)
            {
                this.collectionTcs ??= new TaskCompletionSource<CollectionResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

#if NET
                return new ValueTask<CollectionResponse>(this.collectionTcs.Task);
#else
                return this.collectionTcs.Task;
#endif
            }

            this.WaitForReadersToComplete();

            // Start a collection on the current thread.
            this.collectionRunning = true;

            if (openMetricsRequested)
            {
                this.previousOpenMetricsDataViewGeneratedAtUtc = null;
            }
            else
            {
                this.previousPlainTextDataViewGeneratedAtUtc = null;
            }
        }
        finally
        {
            Interlocked.Increment(ref this.readerCount);
            this.ExitGlobalLock();
        }

        CollectionResponse response;
        var result = this.ExecuteCollect(openMetricsRequested);
        if (result)
        {
            var generatedAt = this.UtcNow();

            if (openMetricsRequested)
            {
                this.previousOpenMetricsDataViewGeneratedAtUtc = generatedAt;
            }
            else
            {
                this.previousPlainTextDataViewGeneratedAtUtc = generatedAt;
            }

            previousDataViewGeneratedAtUtc = openMetricsRequested
                ? this.previousOpenMetricsDataViewGeneratedAtUtc
                : this.previousPlainTextDataViewGeneratedAtUtc;

            response = new CollectionResponse(this.previousOpenMetricsDataView, this.previousPlainTextDataView, previousDataViewGeneratedAtUtc!.Value, fromCache: false);
        }
        else
        {
            response = default;
        }

        this.EnterGlobalLock();

        try
        {
            this.collectionRunning = false;

            if (this.collectionTcs != null)
            {
                this.collectionTcs.SetResult(response);
                this.collectionTcs = null;
            }
        }
        finally
        {
            this.ExitGlobalLock();
        }

#if NET
        return new ValueTask<CollectionResponse>(response);
#else
        return Task.FromResult(response);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitCollect() =>
        Interlocked.Decrement(ref this.readerCount);

    private static bool IncreaseBufferSize(ref byte[] buffer)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnterGlobalLock() =>
        this.globalLockState.Wait();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExitGlobalLock() =>
        this.globalLockState.Release();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WaitForReadersToComplete()
    {
        SpinWait.SpinUntil(NoReaders);
        bool NoReaders() => Interlocked.CompareExchange(ref this.readerCount, 0, this.readerCount) == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ExecuteCollect(bool openMetricsRequested)
    {
        Debug.Assert(this.exporter.Collect != null, "this.exporter.Collect was null");

        this.exporter.OnExport = this.onCollectRef;
        this.exporter.OpenMetricsRequested = openMetricsRequested;
        var result = this.exporter.Collect!(Timeout.Infinite);
        this.exporter.OnExport = null;
        return result;
    }

    private ExportResult OnCollect(in Batch<Metric> metrics)
    {
        var cursor = 0;
        ref byte[] buffer = ref (this.exporter.OpenMetricsRequested ? ref this.openMetricsBuffer : ref this.plainTextBuffer);

        try
        {
            if (this.exporter.OpenMetricsRequested)
            {
                cursor = this.WriteTargetInfo(ref buffer);

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
                                cursor = PrometheusSerializer.WriteScopeInfo(buffer, cursor, metric.MeterName);

                                break;
                            }
                            catch (IndexOutOfRangeException)
                            {
                                if (!IncreaseBufferSize(ref buffer))
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
                            buffer,
                            cursor,
                            metric,
                            this.GetPrometheusMetric(metric),
                            this.exporter.OpenMetricsRequested,
                            this.exporter.DisableTimestamp);

                        break;
                    }
                    catch (IndexOutOfRangeException)
                    {
                        if (!IncreaseBufferSize(ref buffer))
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
                    cursor = PrometheusSerializer.WriteEof(buffer, cursor);
                    break;
                }
                catch (IndexOutOfRangeException)
                {
                    if (!IncreaseBufferSize(ref buffer))
                    {
                        throw;
                    }
                }
            }

            if (this.exporter.OpenMetricsRequested)
            {
                this.previousOpenMetricsDataView = new ArraySegment<byte>(buffer, 0, cursor);
            }
            else
            {
                this.previousPlainTextDataView = new ArraySegment<byte>(buffer, 0, cursor);
            }

            return ExportResult.Success;
        }
        catch (Exception)
        {
            if (this.exporter.OpenMetricsRequested)
            {
                this.previousOpenMetricsDataView = new ArraySegment<byte>([], 0, 0);
            }
            else
            {
                this.previousPlainTextDataView = new ArraySegment<byte>([], 0, 0);
            }

            return ExportResult.Failure;
        }
    }

    private int WriteTargetInfo(ref byte[] buffer)
    {
        if (this.targetInfoBufferLength < 0)
        {
            while (true)
            {
                try
                {
                    this.targetInfoBufferLength = PrometheusSerializer.WriteTargetInfo(buffer, 0, this.exporter.Resource);

                    break;
                }
                catch (IndexOutOfRangeException)
                {
                    if (!IncreaseBufferSize(ref buffer))
                    {
                        throw;
                    }
                }
            }
        }

        return this.targetInfoBufferLength;
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
        public CollectionResponse(ArraySegment<byte> openMetricsView, ArraySegment<byte> plainTextView, DateTime generatedAtUtc, bool fromCache)
        {
            this.OpenMetricsView = openMetricsView;
            this.PlainTextView = plainTextView;
            this.GeneratedAtUtc = generatedAtUtc;
            this.FromCache = fromCache;
        }

        public ArraySegment<byte> OpenMetricsView { get; }

        public ArraySegment<byte> PlainTextView { get; }

        public DateTime GeneratedAtUtc { get; }

        public bool FromCache { get; }
    }
}
