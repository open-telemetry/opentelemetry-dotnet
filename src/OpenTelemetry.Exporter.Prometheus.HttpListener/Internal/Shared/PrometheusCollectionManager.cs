// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Exporter.Prometheus.Serialization;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.Prometheus;

internal sealed class PrometheusCollectionManager
{
    private const int MaxCachedMetrics = 1024;

    // Upper bound on how many times entering a collection will retry while
    // resolving races with concurrent scrapes (a collection completing, active
    // readers draining, or a shared collection that did not serve this protocol).
    // In practice only a couple of iterations ever run; the cap exists purely so
    // a pathological interleaving of concurrent scrapes cannot starve one into
    // looping forever.
    private const int MaxCollectAttempts = 1024;

    private readonly ConcurrentDictionary<PrometheusProtocol, PrometheusProtocolState> protocolStates = new();

    private readonly PrometheusExporter exporter;
    private readonly bool scopeInfoEnabled;
    private readonly bool targetInfoEnabled;
    private readonly Func<string, bool>? resourceConstantLabelsFilter;
    private readonly TimeSpan scrapeResponseCacheDuration;
    private readonly long baseTimestamp = Stopwatch.GetTimestamp();
    private readonly PrometheusExporter.ExportFunc onCollectRef;
    private readonly Dictionary<Metric, PrometheusMetric> metricsCache;
    private readonly int maxBufferSize;

    private int metricsCacheCount;
    private IReadOnlyList<KeyValuePair<string, object>>? resourceConstantLabels;
    private bool resourceConstantLabelsComputed;
    private int globalLockState;
    private CollectionContext? collectionContext;
    private CollectionContext? onCollectContext;
    private CollectionExecutionResult collectionExecutionResult;

    public PrometheusCollectionManager(PrometheusExporter exporter)
    {
        this.exporter = exporter;
        this.scopeInfoEnabled = this.exporter.ScopeInfoEnabled;
        this.targetInfoEnabled = this.exporter.TargetInfoEnabled;
        this.resourceConstantLabelsFilter = this.exporter.ResourceConstantLabels;
        this.scrapeResponseCacheDuration = TimeSpan.FromMilliseconds(this.exporter.ScrapeResponseCacheDurationMilliseconds);
        this.onCollectRef = this.OnCollect;
        this.metricsCache = [];
        this.GetElapsedTime = () => Stopwatch.GetElapsedTime(this.baseTimestamp);
        this.maxBufferSize = this.exporter.MaxScrapeResponseSizeBytes;
    }

    internal Func<DateTime> UtcNow { get; set; } = static () => DateTime.UtcNow;

    internal Func<TimeSpan> GetElapsedTime { get; set; }

#if NET
    public ValueTask<CollectionResponse> EnterCollect(in PrometheusProtocol protocol)
#else
    public Task<CollectionResponse> EnterCollect(in PrometheusProtocol protocol)
#endif
    {
        var step = this.TryEnterCollect(protocol);

        if (step.PendingCollectionTask is null)
        {
#if NET
            return new ValueTask<CollectionResponse>(step.Response);
#else
            return Task.FromResult(step.Response);
#endif
        }

        return this.WaitForCollectionResponseAsync(protocol, step.PendingCollectionTask, step.JoinedActiveCollection);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitCollect(in PrometheusProtocol protocol)
        => this.GetProtocolState(protocol).DecrementReaderCount();

    /// <summary>
    /// Performs a single synchronous attempt to enter a collection. Returns a
    /// completed response when one is immediately available (served from the
    /// cache, or produced by a collection executed on this thread), otherwise
    /// returns the in-flight collection task the caller must await.
    /// </summary>
    /// <param name="protocol">The protocol for which the collection is being attempted.</param>
    /// <returns>
    /// The <see cref="CollectStep"/> result of the attempt.
    /// </returns>
    private CollectStep TryEnterCollect(in PrometheusProtocol protocol)
    {
        CollectionResponse? cachedResponse = null;
        Task<CollectionResult>? pendingCollectionTask = null;
        CollectionContext? activeCollectionContext = null;
        var joinedActiveCollection = false;
        var resolved = false;

        for (var attempt = 0; attempt < MaxCollectAttempts; attempt++)
        {
            pendingCollectionTask = null;
            joinedActiveCollection = false;
            var retry = false;

            this.EnterGlobalLock();

            try
            {
                // If we are within {ScrapeResponseCacheDurationMilliseconds} of the
                // last successful collect, return the previous view.
                if (this.TryGetCachedResponse(protocol, out var response))
                {
                    cachedResponse = response;
                    this.IncrementReaderCount(protocol);
                    resolved = true;
                    break;
                }

                if (this.collectionContext is { } currentCollectionContext)
                {
                    pendingCollectionTask = currentCollectionContext.Task;
                    joinedActiveCollection = currentCollectionContext.TryRegisterProtocol(protocol, this.HasActiveReaders(protocol));

                    if (joinedActiveCollection)
                    {
                        this.IncrementReaderCount(protocol);
                        resolved = true;
                        break;
                    }

                    if (currentCollectionContext.Task.IsCompleted)
                    {
                        if (ReferenceEquals(this.collectionContext, currentCollectionContext))
                        {
                            this.collectionContext = null;
                        }

                        pendingCollectionTask = null;
                        retry = true;
                    }
                    else
                    {
                        resolved = true;
                        break;
                    }
                }
            }
            finally
            {
                this.ExitGlobalLock();
            }

            if (retry)
            {
                continue;
            }

            if (this.WaitForReadersToComplete(protocol))
            {
                continue;
            }

            this.EnterGlobalLock();

            try
            {
                if (this.TryGetCachedResponse(protocol, out var response))
                {
                    cachedResponse = response;
                    this.IncrementReaderCount(protocol);
                    resolved = true;
                    break;
                }

                if (this.collectionContext is { } currentCollectionContext)
                {
                    pendingCollectionTask = currentCollectionContext.Task;
                    joinedActiveCollection = currentCollectionContext.TryRegisterProtocol(protocol, this.HasActiveReaders(protocol));

                    if (joinedActiveCollection)
                    {
                        this.IncrementReaderCount(protocol);
                        resolved = true;
                        break;
                    }

                    if (currentCollectionContext.Task.IsCompleted)
                    {
                        if (ReferenceEquals(this.collectionContext, currentCollectionContext))
                        {
                            this.collectionContext = null;
                        }

                        pendingCollectionTask = null;
                        continue;
                    }

                    resolved = true;
                    break;
                }

                activeCollectionContext = new CollectionContext(protocol);
                this.collectionContext = activeCollectionContext;

                this.IncrementReaderCount(protocol);
                resolved = true;
                break;
            }
            finally
            {
                this.ExitGlobalLock();
            }
        }

        if (!resolved)
        {
            // The attempt budget was exhausted while contending with concurrent
            // scrapes. Degrade to a failed (empty) response rather than spinning
            // forever - this mirrors how an unsuccessful collection is reported.
            this.IncrementReaderCount(protocol);
            PrometheusExporterEventSource.Log.CollectFailed();

            return CollectStep.Completed(default);
        }

        if (cachedResponse is { } collectionResponse)
        {
            return CollectStep.Completed(collectionResponse);
        }

        if (pendingCollectionTask is not null)
        {
            return CollectStep.Pending(pendingCollectionTask, joinedActiveCollection);
        }

        var result = this.ExecuteCollect(activeCollectionContext!);

        activeCollectionContext!.SetResult(result);

        this.EnterGlobalLock();

        try
        {
            if (ReferenceEquals(this.collectionContext, activeCollectionContext))
            {
                this.collectionContext = null;
            }
        }
        finally
        {
            this.ExitGlobalLock();
        }

        return result.TryGetResponse(protocol, out var collectedResponse)
            ? CollectStep.Completed(collectedResponse)
            : CollectStep.Completed(default);
    }

#if NET
    private async ValueTask<CollectionResponse> WaitForCollectionResponseAsync(PrometheusProtocol protocol, Task<CollectionResult> pendingCollectionTask, bool joinedActiveCollection)
#else
    private async Task<CollectionResponse> WaitForCollectionResponseAsync(PrometheusProtocol protocol, Task<CollectionResult> pendingCollectionTask, bool joinedActiveCollection)
#endif
    {
        for (var attempt = 0; attempt < MaxCollectAttempts; attempt++)
        {
            var collectionResult = await pendingCollectionTask.ConfigureAwait(false);

            if (joinedActiveCollection &&
                collectionResult.TryGetResponse(protocol, out var response))
            {
                return response;
            }

            if (joinedActiveCollection)
            {
                this.ExitCollect(protocol);
            }

            // The shared collection did not produce a response for this protocol,
            // so make another attempt. Loop here (bounded by MaxCollectAttempts)
            // rather than recursing back into EnterCollect: when the awaited
            // collection completes synchronously the continuation runs inline,
            // so a recursive retry would grow the stack on every iteration
            // and eventually overflow under contention.
            var step = this.TryEnterCollect(protocol);

            if (step.PendingCollectionTask is null)
            {
                return step.Response;
            }

            pendingCollectionTask = step.PendingCollectionTask;
            joinedActiveCollection = step.JoinedActiveCollection;
        }

        // The retry budget was exhausted while contending with concurrent scrapes;
        // give up on this pending collection and degrade to a failed (empty)
        // response instead of awaiting/retrying indefinitely.
        //
        // Leave exactly one reader slot outstanding for the caller's ExitCollect to
        // release: when the last step joined the active collection, TryEnterCollect
        // already took that slot, so take one only otherwise - taking a second here
        // would leak, taking none would drive the count negative and hang every
        // later scrape.
        if (!joinedActiveCollection)
        {
            this.IncrementReaderCount(protocol);
            PrometheusExporterEventSource.Log.CollectFailed();
        }

        return default;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnterGlobalLock()
    {
        SpinWait lockWait = default;
        while (true)
        {
            if (Interlocked.CompareExchange(ref this.globalLockState, 1, 0) != 0)
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
    private void IncrementReaderCount(in PrometheusProtocol protocol)
        => this.GetProtocolState(protocol).IncrementReaderCount();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasActiveReaders(in PrometheusProtocol protocol)
        => this.protocolStates.TryGetValue(protocol, out var state) && state.HasActiveReaders();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool WaitForReadersToComplete(in PrometheusProtocol protocol)
    {
        var state = this.GetProtocolState(protocol);
        var didSpin = false;
        SpinWait readWait = default;
        while (true)
        {
            if (!state.HasActiveReaders())
            {
                break;
            }

            this.EnterGlobalLock();

            try
            {
                if (this.collectionContext is not null)
                {
                    return true;
                }
            }
            finally
            {
                this.ExitGlobalLock();
            }

            didSpin = true;
            readWait.SpinOnce();
        }

        return didSpin;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private CollectionResult ExecuteCollect(CollectionContext collectionContext)
    {
        this.onCollectContext = collectionContext;
        this.collectionExecutionResult = default;
        this.exporter.OnExport = this.onCollectRef;

        try
        {
            var succeeded = this.exporter.Collect!(Timeout.Infinite);
            return this.CreateCollectionResult(collectionContext, succeeded, this.collectionExecutionResult);
        }
        finally
        {
            this.exporter.OnExport = null;
            this.onCollectContext = null;
        }
    }

    private ExportResult OnCollect(in Batch<Metric> metrics)
    {
        if (this.onCollectContext is not { } collectionContext)
        {
            this.collectionExecutionResult = default;
            return ExportResult.Failure;
        }

        var protocols = collectionContext.FreezeProtocols();
        HashSet<PrometheusProtocol>? successfulProtocols = null;

        foreach (var protocol in protocols)
        {
            var state = this.GetProtocolState(protocol);
            if (this.TryWriteResponse(protocol, state, metrics))
            {
                successfulProtocols ??= [];
                successfulProtocols.Add(protocol);
            }
        }

        this.collectionExecutionResult = new CollectionExecutionResult(protocols, successfulProtocols);

        return successfulProtocols is { Count: > 0 }
            ? ExportResult.Success
            : ExportResult.Failure;
    }

    private bool TryWriteResponse(in PrometheusProtocol protocol, PrometheusProtocolState state, in Batch<Metric> metrics)
    {
        try
        {
            var serializer = TextFormatSerializer.GetSerializer(protocol);

            var cursor = this.targetInfoEnabled ? this.WriteTargetInfo(serializer, state) : 0;
            var metricStates = this.GetMetricStates(serializer, metrics);
            var options = new TextFormatSerializerOptions(
                suppressScopeInfo: !this.scopeInfoEnabled,
                resourceConstantLabels: this.GetResourceConstantLabels());

            foreach (var metricState in metricStates)
            {
                while (true)
                {
                    try
                    {
                        cursor = serializer.WriteMetric(
                            state.Buffer,
                            cursor,
                            metricState.Metric,
                            metricState.PrometheusMetric,
                            metricState.WriteType,
                            metricState.WriteUnit,
                            metricState.WriteHelp,
                            metricState.Unit,
                            metricState.Help,
                            options);

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
                    cursor = serializer.WriteEof(state.Buffer, cursor);
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

            return true;
        }
        catch (Exception ex)
        {
            state.ResetView();

            PrometheusExporterEventSource.Log.FailedExport(ex);

            return false;
        }
    }

    private CollectionResult CreateCollectionResult(CollectionContext collectionContext, bool succeeded, CollectionExecutionResult executionResult)
    {
        var protocols = executionResult.Protocols ?? collectionContext.FreezeProtocols();
        var responses = new Dictionary<PrometheusProtocol, CollectionResponse>(protocols.Length);

        if (!succeeded && executionResult.Protocols is not null)
        {
            foreach (var protocol in protocols)
            {
                responses[protocol] = default;
            }
        }

        if (succeeded)
        {
            var generatedAt = this.UtcNow();
            var generatedAtElapsed = this.GetElapsedTime();
            var successfulProtocols = executionResult.SuccessfulProtocols;

            foreach (var protocol in protocols)
            {
                if (successfulProtocols is not null &&
                    !successfulProtocols.Contains(protocol))
                {
                    responses[protocol] = default;
                    continue;
                }

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

                responses[protocol] = new CollectionResponse(view, generatedAt, fromCache: false);
            }
        }

        return new CollectionResult(responses);
    }

    private bool TryGetCachedResponse(in PrometheusProtocol protocol, out CollectionResponse response)
    {
        if (this.protocolStates.TryGetValue(protocol, out var state) &&
            state.GeneratedAt is { } generatedAt &&
            state.GeneratedAtElapsed is { } generatedAtElapsed &&
            this.scrapeResponseCacheDuration > TimeSpan.Zero &&
            this.GetElapsedTime() - generatedAtElapsed < this.scrapeResponseCacheDuration)
        {
            response = new CollectionResponse(state.View, generatedAt, fromCache: true);
            return true;
        }

        response = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PrometheusProtocolState GetProtocolState(in PrometheusProtocol protocol) =>
#if NET
        this.protocolStates.GetOrAdd(protocol, static (_, maxBufferSize) => new(maxBufferSize), this.maxBufferSize);
#else
        this.protocolStates.GetOrAdd(protocol, (_) => new(this.maxBufferSize));
#endif

    private IReadOnlyList<KeyValuePair<string, object>>? GetResourceConstantLabels()
    {
        if (!this.resourceConstantLabelsComputed)
        {
            if (this.resourceConstantLabelsFilter is { } filter)
            {
                List<KeyValuePair<string, object>>? labels = null;

                foreach (var attribute in this.exporter.Resource.Attributes)
                {
                    if (filter(attribute.Key))
                    {
                        labels ??= [];
                        labels.Add(attribute);
                    }
                }

                this.resourceConstantLabels = labels;
            }

            this.resourceConstantLabelsComputed = true;
        }

        return this.resourceConstantLabels;
    }

    private int WriteTargetInfo(TextFormatSerializer serializer, PrometheusProtocolState state)
    {
        while (true)
        {
            try
            {
                return serializer.WriteTargetInfo(state.Buffer, 0, this.exporter.Resource);
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
            prometheusMetric = PrometheusMetric.Create(metric, this.exporter.DisableTotalNameSuffixForCounters, this.exporter.AppendSuffixes);

            // Add to the cache if there is space.
            if (this.metricsCacheCount < MaxCachedMetrics)
            {
                this.metricsCache[metric] = prometheusMetric;
                this.metricsCacheCount++;
            }
        }

        return prometheusMetric;
    }

    private List<MetricState> GetMetricStates(TextFormatSerializer serializer, in Batch<Metric> metrics)
    {
        var precomputedMetricStates = new List<PrecomputedMetricState>();
        var metadataStates = new Dictionary<string, MetadataState>(StringComparer.Ordinal);
        HashSet<string>? droppedMetricNames = null;

        foreach (var metric in metrics)
        {
            if (metric.MetricType == MetricType.ExponentialHistogram)
            {
                // Exponential histograms are not yet supported by Prometheus.
                PrometheusExporterEventSource.Log.MetricIgnored(metric);
                continue;
            }

            var prometheusMetric = this.GetPrometheusMetric(metric);
            var metadataName = serializer.GetMetadataName(prometheusMetric);
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
                droppedMetricNames ??= [with(StringComparer.Ordinal)];
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
            this.Succeeded = true;
        }

        public readonly ArraySegment<byte> View { get; }

        public readonly DateTime GeneratedAtUtc { get; }

        public readonly bool FromCache { get; }

        /// <summary>
        /// Gets a value indicating whether the collection that produced this response succeeded.
        /// </summary>
        /// <remarks>
        /// Used to distinguish between a successful collection that produced an empty response
        /// and a failed collection that produced no response.
        /// </remarks>
        public readonly bool Succeeded { get; }
    }

    private readonly struct CollectStep
    {
        private CollectStep(CollectionResponse response, Task<CollectionResult>? pendingCollectionTask, bool joinedActiveCollection)
        {
            this.Response = response;
            this.PendingCollectionTask = pendingCollectionTask;
            this.JoinedActiveCollection = joinedActiveCollection;
        }

        // A non-null task means the caller must await the in-flight collection;
        // a null task means Response already holds the answer.
        public Task<CollectionResult>? PendingCollectionTask { get; }

        public CollectionResponse Response { get; }

        // True when this scrape registered the protocol with the in-flight
        // collection and took a reader slot for it (so the awaiting caller owns a
        // reader count that must be released), false when it is only observing a
        // collection it could not join.
        public bool JoinedActiveCollection { get; }

        public static CollectStep Completed(CollectionResponse response)
            => new(response, pendingCollectionTask: null, joinedActiveCollection: false);

        public static CollectStep Pending(Task<CollectionResult> pendingCollectionTask, bool joinedActiveCollection)
            => new(default, pendingCollectionTask, joinedActiveCollection);
    }

    private readonly struct CollectionResult
    {
        private readonly IReadOnlyDictionary<PrometheusProtocol, CollectionResponse>? responses;

        public CollectionResult(IReadOnlyDictionary<PrometheusProtocol, CollectionResponse> responses)
        {
            this.responses = responses;
        }

        public bool TryGetResponse(in PrometheusProtocol protocol, out CollectionResponse response)
        {
            if (this.responses?.TryGetValue(protocol, out response) == true)
            {
                return true;
            }

            response = default;
            return false;
        }
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

    private readonly struct CollectionExecutionResult
    {
        public CollectionExecutionResult(PrometheusProtocol[] protocols, HashSet<PrometheusProtocol>? successfulProtocols)
        {
            this.Protocols = protocols;
            this.SuccessfulProtocols = successfulProtocols;
        }

        public PrometheusProtocol[] Protocols { get; }

        public HashSet<PrometheusProtocol>? SuccessfulProtocols { get; }
    }

    private sealed class CollectionContext
    {
        private readonly Lock gate = new();
        private readonly HashSet<PrometheusProtocol> protocols = [];
        private readonly TaskCompletionSource<CollectionResult> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool frozen;

        public CollectionContext(in PrometheusProtocol protocol)
        {
            this.protocols.Add(protocol);
        }

        public Task<CollectionResult> Task => this.tcs.Task;

        public PrometheusProtocol[] FreezeProtocols()
        {
            lock (this.gate)
            {
                this.frozen = true;
                return [.. this.protocols];
            }
        }

        public void SetResult(CollectionResult result)
            => this.tcs.SetResult(result);

        public bool TryRegisterProtocol(in PrometheusProtocol protocol, bool hasActiveReaders)
        {
            lock (this.gate)
            {
                if (this.protocols.Contains(protocol))
                {
                    return true;
                }

                if (this.frozen)
                {
                    return false;
                }

                if (hasActiveReaders)
                {
                    return false;
                }

                this.protocols.Add(protocol);
                return true;
            }
        }
    }

    private sealed class PrometheusProtocolState
    {
        private const int InitialBufferSize = PrometheusExporterOptions.InitialScrapeResponseSizeBytes;
        private readonly int maxBufferSize;

        private int readerCount;

        public PrometheusProtocolState(int maxBufferSize)
        {
            this.maxBufferSize = Math.Max(maxBufferSize, InitialBufferSize);
        }

        public static ArraySegment<byte> EmptyView { get; } =
#if NET
            ArraySegment<byte>.Empty;
#else
            new([]);
#endif

        public byte[] Buffer { get; private set; } = new byte[InitialBufferSize];

        public ArraySegment<byte> View { get; private set; } = EmptyView;

        public DateTime? GeneratedAt { get; private set; }

        public TimeSpan? GeneratedAtElapsed { get; private set; }

        public int DecrementReaderCount()
            => Interlocked.Decrement(ref this.readerCount);

        public bool HasActiveReaders()
            => Interlocked.CompareExchange(ref this.readerCount, 0, 0) != 0;

        public void IncrementReaderCount()
            => Interlocked.Increment(ref this.readerCount);

        public bool TryExpandBuffer()
        {
            if (this.Buffer.Length >= this.maxBufferSize)
            {
                return false;
            }

            // Grow by doubling, but never past the configured maximum. Clamping
            // to the maximum (rather than refusing the grow outright when the
            // doubled size would overshoot) means the entire configured budget
            // is usable, with no unreachable remainder.
            var newBufferSize = (int)Math.Min((long)this.Buffer.Length * 2, this.maxBufferSize);

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
