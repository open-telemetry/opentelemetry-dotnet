// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus;

internal sealed class PrometheusCollectionManager
{
    private readonly PrometheusCollector openMetricsCollector;
    private readonly PrometheusCollector plainTextCollector;
    private readonly PrometheusExporter exporter;

    private int collectionCtsLockState;

    public PrometheusCollectionManager(PrometheusExporter exporter)
    {
        this.exporter = exporter;
        this.openMetricsCollector = new PrometheusCollector(exporter, true);
        this.plainTextCollector = new PrometheusCollector(exporter, false);
        this.exporter.OnExport += this.openMetricsCollector.OnCollect;
        this.exporter.OnExport += this.plainTextCollector.OnCollect;
    }

    ~PrometheusCollectionManager()
    {
        this.exporter.OnExport -= this.openMetricsCollector.OnCollect;
        this.exporter.OnExport -= this.plainTextCollector.OnCollect;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#if NET6_0_OR_GREATER
    public ValueTask<CollectionResponse> EnterCollect(bool openMetricsRequested)
#else
    public Task<CollectionResponse> EnterCollect(bool openMetricsRequested)
#endif
    {
        return openMetricsRequested ? this.openMetricsCollector.EnterCollect() : this.plainTextCollector.EnterCollect();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitCollect(bool openMetricsRequested)
    {
        if (openMetricsRequested)
        {
            this.openMetricsCollector.ExitCollect();
        }
        else
        {
            this.plainTextCollector.ExitCollect();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnterCollectionLock()
    {
        SpinWait lockWait = default;
        while (true)
        {
            if (Interlocked.CompareExchange(ref this.collectionCtsLockState, 1, this.collectionCtsLockState) != 0)
            {
                lockWait.SpinOnce();
                continue;
            }

            break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExitCollectionLock()
    {
        Interlocked.Exchange(ref this.collectionCtsLockState, 0);
    }

    public readonly struct CollectionResponse
    {
        public CollectionResponse(ArraySegment<byte> view, DateTime generatedAtUtc, bool fromCache, bool isOpenMetricsFormat)
        {
            this.View = view;
            this.GeneratedAtUtc = generatedAtUtc;
            this.FromCache = fromCache;
            this.IsOpenMetricsFormat = isOpenMetricsFormat;
        }

        public ArraySegment<byte> View { get; }

        public DateTime GeneratedAtUtc { get; }

        public bool FromCache { get; }

        public bool IsOpenMetricsFormat { get; }
    }

    internal sealed class PrometheusCollector
    {
        private const int MaxCachedMetrics = 1024;

        private readonly PrometheusExporter exporter;
        private readonly bool isOpenMetricsFormat;
        private readonly int scrapeResponseCacheDurationMilliseconds;
        private readonly Dictionary<Metric, PrometheusMetric> metricsCache;
        private readonly HashSet<string> scopes;
        private int metricsCacheCount;
        private byte[] buffer = new byte[85000]; // encourage the object to live in LOH (large object heap)
        private int targetInfoBufferLength = -1; // zero or positive when target_info has been written for the first time
        private int globalLockState;
        private ArraySegment<byte> previousDataView;
        private DateTime? previousDataViewGeneratedAtUtc;
        private bool collectionRunning;
        private int readerCount;
        private TaskCompletionSource<CollectionResponse> collectionTcs;

        public PrometheusCollector(PrometheusExporter exporter, bool isOpenMetricsFormat)
        {
            this.exporter = exporter;
            this.isOpenMetricsFormat = isOpenMetricsFormat;
            this.scrapeResponseCacheDurationMilliseconds = this.exporter.ScrapeResponseCacheDurationMilliseconds;
            this.metricsCache = new Dictionary<Metric, PrometheusMetric>();
            this.scopes = new HashSet<string>();
        }

#if NET6_0_OR_GREATER
        public async ValueTask<CollectionResponse> EnterCollect()
#else
        public async Task<CollectionResponse> EnterCollect()
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
                return new CollectionResponse(this.previousDataView, this.previousDataViewGeneratedAtUtc.Value, true, this.isOpenMetricsFormat);
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
                return await this.collectionTcs.Task;
            }

            this.WaitForReadersToComplete();

            // Start a collection on the current thread.
            this.collectionRunning = true;
            this.previousDataViewGeneratedAtUtc = null;
            Interlocked.Increment(ref this.readerCount);
            this.ExitGlobalLock();

            CollectionResponse response;
            var result = await this.ExecuteCollectAsync();
            if (result)
            {
                this.previousDataViewGeneratedAtUtc = DateTime.UtcNow;
                response = new CollectionResponse(this.previousDataView, this.previousDataViewGeneratedAtUtc.Value, false, this.isOpenMetricsFormat);
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

            return response;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ExitCollect()
        {
            Interlocked.Decrement(ref this.readerCount);
        }

        internal ExportResult OnCollect(Batch<Metric> metrics)
        {
            var cursor = 0;

            try
            {
                if (this.isOpenMetricsFormat)
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
                else
                {
                    this.targetInfoBufferLength = -1;
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
                                this.isOpenMetricsFormat);

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

#if NET6_0_OR_GREATER
        private ValueTask<bool> ExecuteCollectAsync()
#else
        private Task<bool> ExecuteCollectAsync()
#endif
        {
            this.exporter.CollectionManager.EnterCollectionLock();

            if (this.exporter.CollectionCts is not TaskCompletionSource<bool> collectionCts)
            {
                this.exporter.CollectionCts = new TaskCompletionSource<bool>();
                collectionCts = this.exporter.CollectionCts;
                this.exporter.CollectionManager.ExitCollectionLock();
                var result = this.exporter.Collect(Timeout.Infinite);
                this.exporter.CollectionManager.EnterCollectionLock();
                this.exporter.CollectionCts = null;
                collectionCts.SetResult(result);
            }

            this.exporter.CollectionManager.ExitCollectionLock();

#if NET6_0_OR_GREATER
            return new ValueTask<bool>(collectionCts.Task);
#else
            return collectionCts.Task;
#endif
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
    }
}
