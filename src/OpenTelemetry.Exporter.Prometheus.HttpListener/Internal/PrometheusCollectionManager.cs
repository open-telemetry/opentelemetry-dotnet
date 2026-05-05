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
    private readonly PrometheusExporter.ExportFunc onCollectRef;
    private readonly Dictionary<Metric, PrometheusMetric> metricsCache;
    private readonly HashSet<string> scopes;
    private int metricsCacheCount;
    private byte[] plainTextBuffer = new byte[85000]; // encourage the object to live in LOH (large object heap)
    private byte[] openMetricsBuffer = new byte[85000]; // encourage the object to live in LOH (large object heap)
    private int plainTextTargetInfoBufferLength = -1;
    private int openMetricsTargetInfoBufferLength = -1;
    private ArraySegment<byte> previousPlainTextDataView;
    private ArraySegment<byte> previousOpenMetricsDataView;
    private int globalLockState;
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
            this.collectionTcs?.SetResult(response);
            this.collectionTcs = null;
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
    public void ExitCollect()
        => Interlocked.Decrement(ref this.readerCount);

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
        => Interlocked.Exchange(ref this.globalLockState, 0);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ExecuteCollect(bool openMetricsRequested)
    {
        this.exporter.OnExport = this.onCollectRef;
        this.exporter.OpenMetricsRequested = openMetricsRequested;
        var result = this.exporter.Collect!(Timeout.Infinite);
        this.exporter.OnExport = null;
        return result;
    }

    private ExportResult OnCollect(in Batch<Metric> metrics)
    {
        var cursor = 0;
        ref var buffer = ref (this.exporter.OpenMetricsRequested ? ref this.openMetricsBuffer : ref this.plainTextBuffer);

        try
        {
            cursor = this.WriteTargetInfo(ref buffer);

            if (this.exporter.OpenMetricsRequested)
            {
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
                                cursor = PrometheusSerializer.WriteScopeInfo(buffer, cursor, metric.MeterName, openMetricsRequested: true);

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

            var metricStates = this.GetMetricStates(metrics, this.exporter.OpenMetricsRequested);

            foreach (var metricState in metricStates)
            {
                while (true)
                {
                    try
                    {
                        cursor = PrometheusSerializer.WriteMetric(
                            buffer,
                            cursor,
                            metricState.Metric,
                            metricState.PrometheusMetric,
                            this.exporter.OpenMetricsRequested,
                            metricState.WriteType,
                            metricState.WriteUnit,
                            metricState.WriteHelp,
                            metricState.Unit,
                            metricState.Help);

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
        catch (Exception ex)
        {
            if (this.exporter.OpenMetricsRequested)
            {
                this.previousOpenMetricsDataView = new ArraySegment<byte>([], 0, 0);
            }
            else
            {
                this.previousPlainTextDataView = new ArraySegment<byte>([], 0, 0);
            }

            PrometheusExporterEventSource.Log.FailedExport(ex);

            return ExportResult.Failure;
        }
    }

    private int WriteTargetInfo(ref byte[] buffer)
    {
        ref var targetInfoBufferLength = ref this.exporter.OpenMetricsRequested
            ? ref this.openMetricsTargetInfoBufferLength
            : ref this.plainTextTargetInfoBufferLength;

        if (targetInfoBufferLength < 0)
        {
            while (true)
            {
                try
                {
                    targetInfoBufferLength = PrometheusSerializer.WriteTargetInfo(buffer, 0, this.exporter.Resource, this.exporter.OpenMetricsRequested);
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

        return targetInfoBufferLength;
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

    private List<MetricState> GetMetricStates(in Batch<Metric> metrics, bool openMetricsRequested)
    {
        var precomputedMetricStates = new List<PrecomputedMetricState>();
        var metadataStates = new Dictionary<string, MetadataState>(StringComparer.Ordinal);
        var droppedMetricNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var metric in metrics)
        {
            if (!PrometheusSerializer.CanWriteMetric(metric))
            {
                continue;
            }

            var prometheusMetric = this.GetPrometheusMetric(metric);
            var metadataName = openMetricsRequested ? prometheusMetric.OpenMetricsMetadataName : prometheusMetric.Name;
            precomputedMetricStates.Add(new PrecomputedMetricState(metric, prometheusMetric, metadataName));

            if (!metadataStates.TryGetValue(metadataName, out var metadataState))
            {
                metadataStates[metadataName] = new MetadataState(
                    prometheusMetric.Type,
                    string.IsNullOrEmpty(metric.Description) ? null : metric.Description,
                    string.IsNullOrEmpty(prometheusMetric.Unit) ? null : prometheusMetric.Unit);
                continue;
            }

            if (metadataState.Type != prometheusMetric.Type)
            {
                droppedMetricNames.Add(metadataName);
                PrometheusExporterEventSource.Log.ConflictingType(metadataName, metadataState.Type, prometheusMetric.Type);
            }

            if (!string.IsNullOrEmpty(prometheusMetric.Unit) &&
                metadataState.Unit == null)
            {
                metadataState = new MetadataState(metadataState.Type, metadataState.Help, prometheusMetric.Unit);
                metadataStates[metadataName] = metadataState;
            }
            else if (!string.IsNullOrEmpty(prometheusMetric.Unit) &&
                     metadataState.Unit != null &&
                     metadataState.Unit != prometheusMetric.Unit)
            {
                PrometheusExporterEventSource.Log.ConflictingUnit(metadataName, metadataState.Unit, prometheusMetric.Unit!);
            }

            if (!string.IsNullOrEmpty(metric.Description) &&
                metadataState.Help == null)
            {
                metadataState = new MetadataState(metadataState.Type, metric.Description, metadataState.Unit);
                metadataStates[metadataName] = metadataState;
            }
            else if (!string.IsNullOrEmpty(metric.Description) &&
                     metadataState.Help != null &&
                     metadataState.Help != metric.Description)
            {
                PrometheusExporterEventSource.Log.ConflictingHelp(metadataName, metadataState.Help, metric.Description);
            }
        }

        var metricStates = new List<MetricState>(precomputedMetricStates.Count);
        var emittedMetricNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var metricState in precomputedMetricStates)
        {
            if (droppedMetricNames.Contains(metricState.MetadataName))
            {
                continue;
            }

            var writeMetadata = emittedMetricNames.Add(metricState.MetadataName);
            var metadataState = metadataStates[metricState.MetadataName];

            metricStates.Add(
                new MetricState(
                    metricState.Metric,
                    metricState.PrometheusMetric,
                    writeMetadata,
                    writeMetadata && metadataState.Unit != null,
                    writeMetadata && metadataState.Help != null,
                    metadataState.Unit,
                    metadataState.Help));
        }

        return metricStates;
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

        public readonly ArraySegment<byte> OpenMetricsView { get; }

        public readonly ArraySegment<byte> PlainTextView { get; }

        public readonly DateTime GeneratedAtUtc { get; }

        public readonly bool FromCache { get; }
    }

    private readonly struct MetricState
    {
        public MetricState(
            Metric metric,
            PrometheusMetric prometheusMetric,
            bool writeType,
            bool writeUnit,
            bool writeHelp,
            string? unit,
            string? help)
        {
            this.Metric = metric;
            this.PrometheusMetric = prometheusMetric;
            this.WriteType = writeType;
            this.WriteUnit = writeUnit;
            this.WriteHelp = writeHelp;
            this.Unit = unit;
            this.Help = help;
        }

        public readonly Metric Metric { get; }

        public readonly PrometheusMetric PrometheusMetric { get; }

        public readonly bool WriteType { get; }

        public readonly bool WriteUnit { get; }

        public readonly bool WriteHelp { get; }

        public readonly string? Unit { get; }

        public readonly string? Help { get; }
    }

    private readonly struct PrecomputedMetricState
    {
        public PrecomputedMetricState(Metric metric, PrometheusMetric prometheusMetric, string metadataName)
        {
            this.Metric = metric;
            this.PrometheusMetric = prometheusMetric;
            this.MetadataName = metadataName;
        }

        public readonly Metric Metric { get; }

        public readonly PrometheusMetric PrometheusMetric { get; }

        public readonly string MetadataName { get; }
    }

    private readonly struct MetadataState
    {
        public MetadataState(PrometheusType type, string? help, string? unit)
        {
            this.Type = type;
            this.Help = help;
            this.Unit = unit;
        }

        public readonly PrometheusType Type { get; }

        public readonly string? Help { get; }

        public readonly string? Unit { get; }
    }
}
