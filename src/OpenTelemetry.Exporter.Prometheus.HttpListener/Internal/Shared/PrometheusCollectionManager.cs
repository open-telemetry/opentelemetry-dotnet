// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus;

internal sealed class PrometheusCollectionManager
{
    private const int MaxCachedMetrics = 1024;

    private readonly ConcurrentDictionary<PrometheusProtocol, PrometheusProtocolState> protocolStates = new();

    private readonly PrometheusExporter exporter;
    private readonly TimeSpan scrapeResponseCacheDuration;
    private readonly long baseTimestamp = Stopwatch.GetTimestamp();
    private readonly PrometheusExporter.ExportFunc onCollectRef;
    private readonly Dictionary<Metric, PrometheusMetric> metricsCache;
    private int metricsCacheCount;
    private int globalLockState;
    private int readerCount;
    private bool collectionRunning;
    private PrometheusProtocol collectionProtocol;
    private TaskCompletionSource<CollectionResponse>? collectionTcs;

    public PrometheusCollectionManager(PrometheusExporter exporter)
    {
        this.exporter = exporter;
        this.scrapeResponseCacheDuration = TimeSpan.FromMilliseconds(this.exporter.ScrapeResponseCacheDurationMilliseconds);
        this.onCollectRef = this.OnCollect;
        this.metricsCache = [];
        this.GetElapsedTime = () => Stopwatch.GetElapsedTime(this.baseTimestamp);
    }

    internal Func<DateTime> UtcNow { get; set; } = static () => DateTime.UtcNow;

    internal Func<TimeSpan> GetElapsedTime { get; set; }

#if NET
    public ValueTask<CollectionResponse> EnterCollect(PrometheusProtocol protocol)
#else
    public Task<CollectionResponse> EnterCollect(PrometheusProtocol protocol)
#endif
    {
        CollectionResponse? cachedResponse = null;
        Task<CollectionResponse>? pendingCollectionTask = null;
        var incrementReaderCount = false;

        this.EnterGlobalLock();

        try
        {
            // If we are within {ScrapeResponseCacheDurationMilliseconds} of the
            // last successful collect, return the previous view.
            if (this.protocolStates.TryGetValue(protocol, out var state) &&
                state.GeneratedAt is { } generatedAt &&
                this.scrapeResponseCacheDuration > TimeSpan.Zero &&
                this.GetElapsedTime() - state.GeneratedAtElapsed < this.scrapeResponseCacheDuration)
            {
                cachedResponse = new CollectionResponse(state.View, generatedAt, fromCache: true);
                incrementReaderCount = true;
            }
            else if (this.collectionRunning)
            {
                this.collectionTcs ??= new TaskCompletionSource<CollectionResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
                pendingCollectionTask = this.collectionTcs.Task;
                incrementReaderCount = this.collectionProtocol.Equals(protocol);
            }
            else
            {
                this.WaitForReadersToComplete();

                // Start a collection on the current thread.
                this.collectionRunning = true;
                this.collectionProtocol = protocol;
                incrementReaderCount = true;
            }
        }
        finally
        {
            if (incrementReaderCount)
            {
                Interlocked.Increment(ref this.readerCount);
            }

            this.ExitGlobalLock();
        }

        if (cachedResponse is { } collectionResponse)
        {
#if NET
            return new ValueTask<CollectionResponse>(collectionResponse);
#else
            return Task.FromResult(collectionResponse);
#endif
        }

        if (pendingCollectionTask is not null)
        {
            if (incrementReaderCount)
            {
#if NET
                return new ValueTask<CollectionResponse>(pendingCollectionTask);
#else
                return pendingCollectionTask;
#endif
            }

            return this.WaitForRunningCollectionAndRetryAsync(protocol, pendingCollectionTask);
        }

        CollectionResponse response;
        var result = this.ExecuteCollect(protocol);

        if (result)
        {
            var generatedAt = this.UtcNow();
            var generatedAtElapsed = this.GetElapsedTime();

            ArraySegment<byte> view;

            if (this.protocolStates.TryGetValue(protocol, out var state))
            {
                state.UpdateTimestamps(generatedAt, generatedAtElapsed);
                view = state.View;
            }
            else
            {
                view = PrometheusProtocolState.EmptyView;
            }

            response = new CollectionResponse(view, generatedAt, fromCache: false);
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

#if NET
    private async ValueTask<CollectionResponse> WaitForRunningCollectionAndRetryAsync(PrometheusProtocol protocol, Task<CollectionResponse> pendingCollectionTask)
#else
    private async Task<CollectionResponse> WaitForRunningCollectionAndRetryAsync(PrometheusProtocol protocol, Task<CollectionResponse> pendingCollectionTask)
#endif
    {
        await pendingCollectionTask.ConfigureAwait(false);
        return await this.EnterCollect(protocol).ConfigureAwait(false);
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
    private bool ExecuteCollect(PrometheusProtocol protocol)
    {
        this.exporter.OnExport = this.onCollectRef;

        try
        {
            this.exporter.Protocol = protocol;
            return this.exporter.Collect!(Timeout.Infinite);
        }
        finally
        {
            this.exporter.OnExport = null;
        }
    }

    private ExportResult OnCollect(in Batch<Metric> metrics)
    {
        var protocol = this.exporter.Protocol;

        if (!this.protocolStates.TryGetValue(protocol, out var state))
        {
            this.protocolStates[protocol] = state = new();
        }

        try
        {
            var cursor = this.WriteTargetInfo(protocol, state);

            var metricStates = this.GetMetricStates(metrics, protocol.IsOpenMetrics);

            foreach (var metricState in metricStates)
            {
                while (true)
                {
                    try
                    {
                        cursor = PrometheusSerializer.WriteMetric(
                            state.Buffer,
                            cursor,
                            metricState.Metric,
                            metricState.PrometheusMetric,
                            protocol.IsOpenMetrics,
                            metricState.WriteType,
                            metricState.WriteUnit,
                            metricState.WriteHelp,
                            metricState.Unit,
                            metricState.Help);

                        break;
                    }
                    catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
                    {
                        if (!state.TryExpandBuffer())
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
                    cursor = PrometheusSerializer.WriteEof(state.Buffer, cursor);
                    break;
                }
                catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
                {
                    if (!state.TryExpandBuffer())
                    {
                        throw;
                    }
                }
            }

            state.UpdateView(cursor);

            return ExportResult.Success;
        }
        catch (Exception ex)
        {
            state.ResetView();

            PrometheusExporterEventSource.Log.FailedExport(ex);

            return ExportResult.Failure;
        }
    }

    private int WriteTargetInfo(PrometheusProtocol protocol, PrometheusProtocolState state)
    {
        while (true)
        {
            try
            {
                return PrometheusSerializer.WriteTargetInfo(state.Buffer, 0, this.exporter.Resource, protocol.IsOpenMetrics);
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
            {
                if (!state.TryExpandBuffer())
                {
                    throw;
                }
            }
        }
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
        HashSet<string>? droppedMetricNames = null;

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
                droppedMetricNames ??= new(StringComparer.Ordinal);
                droppedMetricNames.Add(metadataName);
                PrometheusExporterEventSource.Log.ConflictingType(metadataName, metadataState.Type, prometheusMetric.Type);
            }

            if (!string.IsNullOrEmpty(prometheusMetric.Unit) &&
                metadataState.Unit == null)
            {
                metadataState = new MetadataState(metadataState.Type, metadataState.Help, prometheusMetric.Unit);
                metadataStates[metadataName] = metadataState;
            }
            else if (prometheusMetric.Unit is { Length: > 0 } &&
                     metadataState.Unit != null &&
                     metadataState.Unit != prometheusMetric.Unit)
            {
                PrometheusExporterEventSource.Log.ConflictingUnit(metadataName, metadataState.Unit, prometheusMetric.Unit);
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

        precomputedMetricStates.Sort(static (left, right) =>
        {
            var result = string.CompareOrdinal(left.MetadataName, right.MetadataName);
            if (result != 0)
            {
                return result;
            }

            result = string.CompareOrdinal(left.Metric.MeterName, right.Metric.MeterName);
            return result != 0 ? result : string.CompareOrdinal(left.Metric.Name, right.Metric.Name);
        });

        var metricStates = new List<MetricState>(precomputedMetricStates.Count);
        var emittedMetricNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var metricState in precomputedMetricStates)
        {
            if (droppedMetricNames?.Contains(metricState.MetadataName) is true)
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
        public CollectionResponse(ArraySegment<byte> view, DateTime generatedAtUtc, bool fromCache)
        {
            this.View = view;
            this.GeneratedAtUtc = generatedAtUtc;
            this.FromCache = fromCache;
        }

        public readonly ArraySegment<byte> View { get; }

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

    private sealed class PrometheusProtocolState
    {
        private const int InitialBufferSize = 85_000; // Encourage the object to live in LOH (large object heap)
        private const int MaxBufferSize = 100 * 1024 * 1024; // 100 MB

        public static ArraySegment<byte> EmptyView { get; } =
#if NET
            ArraySegment<byte>.Empty;
#else
            new([], 0, 0);
#endif

        public byte[] Buffer { get; private set; } = new byte[InitialBufferSize];

        public ArraySegment<byte> View { get; private set; } = EmptyView;

        public DateTime? GeneratedAt { get; private set; }

        public TimeSpan? GeneratedAtElapsed { get; private set; }

        public bool TryExpandBuffer()
        {
            var newBufferSize = this.Buffer.Length * 2;

            if (newBufferSize > MaxBufferSize)
            {
                return false;
            }

            var expanded = new byte[newBufferSize];
            this.Buffer.CopyTo(expanded, 0);
            this.Buffer = expanded;

            return true;
        }

        public void ResetView() => this.View = EmptyView;

        public void UpdateView(int cursor)
            => this.View = new ArraySegment<byte>(this.Buffer, 0, cursor);

        public void UpdateTimestamps(DateTime timestamp, TimeSpan elapsed)
        {
            this.GeneratedAt = timestamp;
            this.GeneratedAtElapsed = elapsed;
        }
    }
}
