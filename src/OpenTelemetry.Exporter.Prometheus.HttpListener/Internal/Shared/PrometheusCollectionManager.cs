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
    // resolving races with concurrent scrapes (a collection completing, a
    // response buffer being freed, or a collection this scrape could not
    // register with).
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
    private readonly SeriesRegistry series = new(); // Only used while a response is being written, which is never concurrent.
    private readonly TagKeyTracker tagKeys = new(); // As above.
    private readonly int maxBufferSize;

    private int metricsCacheCount;
    private IReadOnlyList<KeyValuePair<string, object>>? resourceConstantLabels;
    private bool resourceConstantLabelsComputed;
    private int globalLockState;
    private CollectionContext? collectionContext;
    private CollectionContext? onCollectContext;
    private CollectionExecutionResult collectionExecutionResult;
    private TimeSpan? lastCollectionCompletedElapsed;

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

    /// <summary>
    /// Gets the number of scrapes which have entered a collection and not yet
    /// exited it, across all protocols. Exposed for tests, which use it to tell
    /// when a scrape has joined an in-flight collection.
    /// </summary>
    internal int ActiveReaderCount
    {
        get
        {
            var count = 0;

            foreach (var state in this.protocolStates.Values)
            {
                count += state.ReaderCount;
            }

            return count;
        }
    }

#if NET
    public ValueTask<CollectionResponse> EnterCollect(in PrometheusProtocol protocol, CancellationToken cancellationToken = default)
#else
    public Task<CollectionResponse> EnterCollect(in PrometheusProtocol protocol, CancellationToken cancellationToken = default)
#endif
        => this.EnterCollect(protocol, scrapeTimeout: null, cancellationToken);

#if NET
    public ValueTask<CollectionResponse> EnterCollect(
#else
    public Task<CollectionResponse> EnterCollect(
#endif
        in PrometheusProtocol protocol,
        TimeSpan? scrapeTimeout,
        CancellationToken cancellationToken = default)
    {
        var step = this.TryEnterCollect(protocol);

        if (step.IsCompleted)
        {
#if NET
            return new ValueTask<CollectionResponse>(step.Response);
#else
            return Task.FromResult(step.Response);
#endif
        }

        // Reserve part of the waiting scrape's budget for collecting and sending its response.
        // Very short client deadlines must not impose a subsecond idle limit on other clients.
        var writerIdleTimeout = scrapeTimeout is { } timeout
            ? TimeSpan.FromTicks(Math.Max(timeout.Ticks / 2, TimeSpan.TicksPerSecond))
            : (TimeSpan?)null;

        return this.WaitForCollectionResponseAsync(protocol, step, writerIdleTimeout, cancellationToken);
    }

    /// <summary>
    /// Releases the response buffer which <paramref name="response"/> was read from, so that
    /// a later collection can write to it again.
    /// </summary>
    /// <param name="response">The response returned by <c>EnterCollect</c>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable CA1822 // Mark members as static - an instance member pairs with EnterCollect
    public void ExitCollect(in CollectionResponse response)
#pragma warning restore CA1822 // Mark members as static
        => response.Buffer?.DecrementReaderCount();

    internal ResponseWriter BeginResponseWrite(in CollectionResponse response)
    {
        Debug.Assert(response.Buffer is not null, "Only a leased response can be written.");
        return new ResponseWriter(this, response.Buffer!);
    }

#if !NET
    /// <summary>
    /// A <c>Task.WaitAsync(CancellationToken)</c> equivalent for TFMs that predate it.
    /// Faults and results propagate exactly as a plain <c>await task</c> would (unlike
    /// <see cref="Task.Wait(CancellationToken)"/>, which wraps a fault in an
    /// <see cref="AggregateException"/>), and it never blocks a thread while waiting.
    /// </summary>
    private static async Task<T> WaitForTask<T>(Task<T> task, CancellationToken cancellationToken)
    {
        if (!task.IsCompleted && cancellationToken.CanBeCanceled)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using (cancellationToken.Register(static (state) => ((TaskCompletionSource<bool>?)state)?.TrySetResult(true), tcs))
            {
                if (await Task.WhenAny(task, tcs.Task).ConfigureAwait(false) == tcs.Task)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }

        return await task.ConfigureAwait(false);
    }

    private static async Task WaitForTask(Task<bool> task, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);

        try
        {
            await WaitForTask(task, timeoutCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException();
        }
    }
#endif

    /// <summary>
    /// Performs a single synchronous attempt to enter a collection. Returns a
    /// completed response when one is immediately available from the cache,
    /// otherwise returns the in-flight collection task, or the task for a response
    /// buffer being freed, which the caller must await.
    /// </summary>
    /// <param name="protocol">The protocol for which the collection is being attempted.</param>
    /// <returns>
    /// The <see cref="CollectStep"/> result of the attempt.
    /// </returns>
    private CollectStep TryEnterCollect(in PrometheusProtocol protocol)
    {
        CollectionResponse? immediateResponse = null;
        Task<CollectionResult>? pendingCollectionTask = null;
        CollectionContext? activeCollectionContext = null;
        ResponseBuffer? lease = null;
        var resolved = false;

        for (var attempt = 0; attempt < MaxCollectAttempts; attempt++)
        {
            pendingCollectionTask = null;
            lease = null;
            var retry = false;

            this.EnterGlobalLock();

            try
            {
                var state = this.GetProtocolState(protocol);

                state.OnRequested(this.IsNearCollection());

                // If we are within {ScrapeResponseCacheDurationMilliseconds} of the
                // last successful collect, return the previous view.
                if (this.TryGetCachedResponse(protocol, out var response))
                {
                    immediateResponse = response;
                    resolved = true;
                    break;
                }

                if (this.collectionContext is { } currentCollectionContext)
                {
                    pendingCollectionTask = currentCollectionContext.Task;

                    if (currentCollectionContext.TryRegisterProtocol(protocol, state))
                    {
                        lease = state.AcquireTargetBuffer();
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

            this.EnterGlobalLock();

            try
            {
                var state = this.GetProtocolState(protocol);

                if (this.TryGetCachedResponse(protocol, out var response))
                {
                    immediateResponse = response;
                    resolved = true;
                    break;
                }

                if (this.collectionContext is { } currentCollectionContext)
                {
                    pendingCollectionTask = currentCollectionContext.Task;

                    if (currentCollectionContext.TryRegisterProtocol(protocol, state))
                    {
                        lease = state.AcquireTargetBuffer();
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

                // No collection is in flight, so this scrape has to start one
                if (!state.TryReserveBuffer())
                {
                    // Both buffers are leased and any cached response has expired. Wait for
                    // capacity rather than returning stale metrics or overwriting leased bytes.
                    // The asynchronous wait can cancel idle response writers, but a buffer only
                    // becomes reusable after their ExitCollect calls have actually released it.
                    if (state.GetBufferFreedTask() is { } bufferFreedTask)
                    {
                        return CollectStep.WaitingForBuffer(bufferFreedTask);
                    }

                    // A buffer was freed in the meantime, so try to reserve it
                    continue;
                }

                activeCollectionContext = new CollectionContext(protocol);
                this.collectionContext = activeCollectionContext;

                lease = state.AcquireTargetBuffer();
                resolved = true;
                break;
            }
            finally
            {
                this.ExitGlobalLock();
            }
        }

        if (immediateResponse is { } collectionResponse)
        {
            return CollectStep.Completed(collectionResponse);
        }

        if (!resolved)
        {
            // The attempt budget was exhausted while contending with concurrent
            // scrapes. Degrade to a failed (empty) response rather than spinning
            // forever - this mirrors how an unsuccessful collection is reported.
            Debug.Assert(lease is null, "A response buffer was acquired without a collection to write it.");
            PrometheusExporterEventSource.Log.CollectFailed();

            return CollectStep.Completed(default);
        }

        if (pendingCollectionTask is not null)
        {
            return CollectStep.Pending(pendingCollectionTask, lease);
        }

        // Queue the collection (which invokes user-controlled observable instrument
        // callbacks and the exporter) to run on the thread pool rather than inline on
        // this call stack, so a hung callback pins a pool worker instead of the calling
        // request's own thread. This scrape's own deadline is enforced only while
        // awaiting the resulting task (see WaitForCollectionResponseAsync); a collection
        // started here must not be able to pin the calling thread indefinitely if it hangs.
        var collectionContextToRun = activeCollectionContext!;
        this.QueueCollectAndPublish(collectionContextToRun);

        return CollectStep.Pending(collectionContextToRun.Task, lease);
    }

    /// <summary>
    /// Queues <see cref="ExecuteCollectAndPublish"/> to run on the thread pool without
    /// flowing the calling scrape's <see cref="ExecutionContext"/>.
    /// </summary>
    /// <param name="collectionContext">The collection context to execute and publish.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void QueueCollectAndPublish(CollectionContext collectionContext)
    {
        if (ExecutionContext.IsFlowSuppressed())
        {
            _ = Task.Run(() => this.ExecuteCollectAndPublish(collectionContext));
        }
        else
        {
            // Avoid capturing AsyncLocal state like HttpContext.Current or Activity.Current
            using (ExecutionContext.SuppressFlow())
            {
                _ = Task.Run(() => this.ExecuteCollectAndPublish(collectionContext));
            }
        }
    }

    /// <summary>
    /// Runs the collection for <paramref name="collectionContext"/> and publishes its
    /// result. Never faults <paramref name="collectionContext"/>'s task: every scrape
    /// still awaiting it must get back a completed (if failed) result so its own
    /// unconditional <see cref="ExitCollect"/> is reached, since letting an exception
    /// propagate instead would skip that call for anyone still waiting (see the
    /// <c>EnterCollect</c>/<c>ExitCollect</c> callers in <c>PrometheusHttpListener.cs</c>
    /// and <c>PrometheusExporterMiddleware.cs</c>) and permanently leak their reader
    /// slot. Always clears <paramref name="collectionContext"/> from <see cref="collectionContext"/>
    /// before publishing its result, so a scrape whose continuation runs as soon as the
    /// result is published (which can happen on another thread) can never observe a
    /// completed collection still registered as the in-flight one and join a context
    /// that will never collect again.
    /// </summary>
    /// <param name="collectionContext">The collection context to execute and publish.</param>
    private void ExecuteCollectAndPublish(CollectionContext collectionContext)
    {
        CollectionResult result;

        try
        {
            result = this.ExecuteCollect(collectionContext);
        }
        catch (Exception ex)
        {
            PrometheusExporterEventSource.Log.FailedExport(ex);
            result = this.CreateCollectionResult(collectionContext, succeeded: false, default);
        }

        this.EnterGlobalLock();

        try
        {
            if (ReferenceEquals(this.collectionContext, collectionContext))
            {
                this.collectionContext = null;
            }

            this.lastCollectionCompletedElapsed = this.GetElapsedTime();

            foreach (var protocol in collectionContext.FreezeProtocols())
            {
                this.GetProtocolState(protocol).OnWritten(collectionContext.IsSpeculative(protocol));
            }
        }
        finally
        {
            this.ExitGlobalLock();
        }

        collectionContext.SetResult(result);
    }

#if NET
    private async ValueTask<CollectionResponse> WaitForCollectionResponseAsync(
#else
    private async Task<CollectionResponse> WaitForCollectionResponseAsync(
#endif
        PrometheusProtocol protocol,
        CollectStep step,
        TimeSpan? writerIdleTimeout,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxCollectAttempts; attempt++)
        {
            CollectionResult collectionResult = default;

            try
            {
                if (step.PendingCollectionTask is { } pendingCollectionTask)
                {
                    // Bounded by cancellationToken so that a collection stuck inside a user
                    // callback or the exporter cannot hold this scrape past its own deadline.
                    // The collection itself is not aborted: it keeps running in the background
                    // and ExecuteCollectAndPublish still publishes its result and clears
                    // collectionContext whenever (if ever) it completes.
#if NET
                    collectionResult = await pendingCollectionTask.WaitAsync(cancellationToken).ConfigureAwait(false);
#else
                    collectionResult = await WaitForTask(pendingCollectionTask, cancellationToken).ConfigureAwait(false);
#endif
                }
                else
                {
                    if (writerIdleTimeout is { } idleTimeout)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var delay = this.GetProtocolState(protocol).CancelIdleWriters(this.GetElapsedTime(), idleTimeout);

                        try
                        {
#if NET
                            await step.BufferFreedTask!.WaitAsync(delay, cancellationToken).ConfigureAwait(false);
#else
                            await WaitForTask(step.BufferFreedTask!, delay, cancellationToken).ConfigureAwait(false);
#endif
                        }
                        catch (TimeoutException)
                        {
                            // Recheck each writer's last completed write, including writers that
                            // started after this wait. Cancellation never releases their leases.
                            continue;
                        }
                    }
                    else
                    {
                        // Non-HTTP callers without a scrape budget only wait for readers to exit.
#if NET
                        await step.BufferFreedTask!.WaitAsync(cancellationToken).ConfigureAwait(false);
#else
                        await WaitForTask(step.BufferFreedTask!, cancellationToken).ConfigureAwait(false);
#endif
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The caller's deadline elapsed while waiting (a scrape timeout or, for the
                // ASP.NET Core exporter, a client disconnect) - an expected outcome, not a
                // collection failure, so this is not logged as one; the caller already
                // reports the cancellation itself (e.g. ScrapeTimedOut). Degrade to a failed
                // response (mirroring the MaxCollectAttempts exhaustion path below) instead
                // of continuing to wait indefinitely. Nothing will be read from the response
                // buffer taken when joining, so it is released now.
                step.Lease?.DecrementReaderCount();

                return default;
            }

            if (step.Lease is { } lease)
            {
                // This scrape shared the collection, so the collection's outcome is
                // this scrape's outcome. When it produced no response for this
                // protocol it failed, and this scrape reports that failure rather
                // than collecting again: a retry cannot reuse the collection it just
                // shared, so it would have to start a new one, and every scrape
                // sharing the failure would queue up to do so.
                if (!collectionResult.TryGetResponse(protocol, out var response))
                {
                    lease.DecrementReaderCount();
                    PrometheusExporterEventSource.Log.CollectFailed();

                    return default;
                }

                if (!response.Succeeded)
                {
                    // Nothing will be read from the response buffer taken when joining
                    lease.DecrementReaderCount();

                    return response;
                }

                // The response buffer taken when joining is left outstanding for the
                // caller's ExitCollect to release.
                return new CollectionResponse(response.View, response.GeneratedAtUtc, response.FromCache, lease);
            }

            // This scrape never registered with the collection it observed, so that
            // collection was never going to produce a response for it, or it was
            // waiting for a response buffer to be freed: make another attempt. Loop
            // here (bounded by MaxCollectAttempts) rather than recursing back into
            // EnterCollect: when the awaited task completes synchronously the
            // continuation runs inline, so a recursive retry would grow the stack on
            // every iteration and eventually overflow under contention.
            step = this.TryEnterCollect(protocol);

            if (step.IsCompleted)
            {
                return step.Response;
            }
        }

        // The retry budget was exhausted while contending with concurrent scrapes;
        // give up and degrade to a failed (empty) response instead of
        // awaiting/retrying indefinitely. The last attempt may have joined a
        // collection which is not going to be awaited, so release what it took.
        step.Lease?.DecrementReaderCount();
        PrometheusExporterEventSource.Log.CollectFailed();

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
    private CollectionResult ExecuteCollect(CollectionContext collectionContext)
    {
        var collect = this.exporter.Collect;

        if (collect is null)
        {
            return this.CreateCollectionResult(collectionContext, succeeded: false, default);
        }

        this.onCollectContext = collectionContext;
        this.collectionExecutionResult = default;
        this.exporter.OnExport = this.onCollectRef;

        try
        {
            var succeeded = collect(Timeout.Infinite);
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

        // Other formats are only registered once the metrics have been collected, so that their
        // cached responses can still be served while observable instrument callbacks run.
        this.EnterGlobalLock();

        try
        {
            this.RegisterSpeculativeProtocols(collectionContext);
        }
        finally
        {
            this.ExitGlobalLock();
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

            this.series.Clear();

            var suppressScopeInfo = !this.scopeInfoEnabled;
            var resourceConstantLabels = this.GetResourceConstantLabels();

            foreach (var metricState in metricStates)
            {
                if (metricState.WriteType)
                {
                    // This is the first metric of a new metric family, and only the metrics of a family can have the same series.
                    this.series.StartFamily();
                }

                var metricStart = cursor;
                var seriesMark = this.series.Count;
                var deduplicate = metricState.Deduplicate;

                while (true)
                {
                    try
                    {
                        this.tagKeys.Reset();

                        // Series are only compared when they can be the same, as it is not free to do so.
                        var options = deduplicate
                            ? new TextFormatSerializerOptions(suppressScopeInfo, resourceConstantLabels, series: this.series)
                            : new TextFormatSerializerOptions(suppressScopeInfo, resourceConstantLabels, tagKeys: this.tagKeys);

                        cursor = serializer.WriteMetric(
                            state.Buffer,
                            metricStart,
                            metricState.Metric,
                            metricState.PrometheusMetric,
                            metricState.WriteType,
                            metricState.WriteUnit,
                            metricState.WriteHelp,
                            metricState.Unit,
                            metricState.Help,
                            options);

                        if (!deduplicate && serializer.HasCollidingTagKeys(this.tagKeys, metricState.Metric))
                        {
                            // Tag keys of the metric are the same once they are escaped, so the series of
                            // different points can be too. The metric is written again without any that are.
                            deduplicate = true;
                            continue;
                        }

                        break;
                    }
                    catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
                    {
                        // The metric is written again, so the series that it had written must be forgotten.
                        this.series.Rollback(seriesMark);

                        if (!state.TryExpandBuffer())
                        {
                            throw;
                        }
                    }
                }
            }

            if (this.series.DroppedCount > 0)
            {
                PrometheusExporterEventSource.Log.DuplicateSeriesDropped(this.series.DroppedCount, this.series.FirstDroppedMetricName!);
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
        // A protocol registered with the in-flight collection may still have a cached view,
        // but its buffer is being rewritten, so it must not be read from the cache. Other
        // formats are only registered once the metrics have been collected (see OnCollect),
        // so this never waits for observable instrument callbacks, only for serialization.
        if (this.protocolStates.TryGetValue(protocol, out var state) &&
            this.IsCached(state, this.GetElapsedTime()) &&
            this.collectionContext?.ContainsProtocol(protocol) is not true)
        {
            return state.TryAcquireView(fromCache: true, out response);
        }

        response = default;
        return false;
    }

    private bool IsCached(PrometheusProtocolState state, TimeSpan elapsed)
        => state.GeneratedAtElapsed is { } generatedAtElapsed &&
           this.scrapeResponseCacheDuration > TimeSpan.Zero &&
           elapsed - generatedAtElapsed < this.scrapeResponseCacheDuration;

    /// <summary>
    /// Gets a value indicating whether a scrape arriving now would have been served by the
    /// most recent collection, had that collection also written the scrape's format: either a
    /// collection is in flight, or the last one completed within {ScrapeResponseCacheDurationMilliseconds}.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if a scrape arriving now is near a collection; otherwise <see langword="false"/>.
    /// </returns>
    private bool IsNearCollection()
        => this.collectionContext is not null ||
           (this.lastCollectionCompletedElapsed is { } lastCollectionCompletedElapsed &&
            this.GetElapsedTime() - lastCollectionCompletedElapsed < this.scrapeResponseCacheDuration);

    /// <summary>
    /// Registers other formats with a collection, so that scrapes which keep negotiating
    /// different formats within one {ScrapeResponseCacheDurationMilliseconds} window share a
    /// single collection rather than each of them collecting again for its own format.
    /// </summary>
    /// <remarks>
    /// A format is registered when its most recent scrape arrived near a collection, so it
    /// would have been served from the cache had that collection written it too. That holds
    /// however long ago that scrape was, so scrapers which poll together at an interval longer
    /// than the cache duration keep sharing collections. A format is not rewritten while its
    /// response is still cached. A response written speculatively is not rewritten again until
    /// it has been requested.
    /// </remarks>
    /// <param name="collectionContext">The collection context being executed.</param>
    private void RegisterSpeculativeProtocols(CollectionContext collectionContext)
    {
        if (this.scrapeResponseCacheDuration <= TimeSpan.Zero)
        {
            return;
        }

        var elapsed = this.GetElapsedTime();

        foreach (var entry in this.protocolStates)
        {
            var state = entry.Value;

            if (!state.LastRequestNearCollection ||
                this.IsCached(state, elapsed) ||
                (!state.RequestedSinceWritten && state.WrittenSpeculatively))
            {
                continue;
            }

            // A protocol without a response buffer which is free to rewrite is not registered.
            collectionContext.TryRegisterSpeculativeProtocol(entry.Key, state);
        }
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
        if (!this.resourceConstantLabelsComputed && this.exporter.ParentProvider != null)
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

        // The names of the samples that are written for a metric (such as the "_sum" of a histogram) cannot
        // be the name of the samples of another metric family, so the family which has that name is dropped.
        foreach (var family in metadataStates)
        {
            if (droppedMetricNames?.Contains(family.Key) is true)
            {
                continue;
            }

            foreach (var suffix in serializer.GetGeneratedSampleSuffixes(family.Value.Type))
            {
                var generatedName = family.Key + suffix;

                if (metadataStates.ContainsKey(generatedName))
                {
                    droppedMetricNames ??= [with(StringComparer.Ordinal)];
                    droppedMetricNames.Add(generatedName);
                    PrometheusExporterEventSource.Log.ConflictingSampleName(generatedName, family.Key);
                }
            }
        }

        // A family which has more than one metric can have the same series for different metrics, such as when
        // they are from different meters and the scope information that tells them apart is not written.
        var metricCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var precomputedMetricState in precomputedMetricStates)
        {
            metricCounts.TryGetValue(precomputedMetricState.MetadataName, out var count);
            metricCounts[precomputedMetricState.MetadataName] = count + 1;
        }

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

            // The unit is not appended to a name which already ends with it (e.g. "auth.failedlogins"
            // with unit "logins"), is taken for the '_total' suffix of a counter, or may be changed by
            // escaping. The UNIT metadata is optional, but a unit which is not an underscore-separated
            // suffix of the name makes the whole OpenMetrics exposition invalid, so it is omitted then.
            var writeUnit = writeMetadata &&
                metadataState.Unit != null &&
                PrometheusMetric.HasUnitSuffix(metricState.MetadataName, metadataState.Unit);

            metricStates.Add(
                new MetricState(
                    metricState.Metric,
                    metricState.PrometheusMetric,
                    writeMetadata,
                    writeUnit,
                    writeMetadata && metadataState.Help != null,
                    metadataState.Unit,
                    metadataState.Help,
                    deduplicate: metricCounts[metricState.MetadataName] > 1));
        }

        return metricStates;
    }

    public readonly struct CollectionResponse
    {
        public CollectionResponse(ArraySegment<byte> view, DateTime generatedAtUtc, bool fromCache)
            : this(view, generatedAtUtc, fromCache, buffer: null)
        {
        }

        internal CollectionResponse(ArraySegment<byte> view, DateTime generatedAtUtc, bool fromCache, ResponseBuffer? buffer)
        {
            this.View = view;
            this.GeneratedAtUtc = generatedAtUtc;
            this.FromCache = fromCache;
            this.Succeeded = true;
            this.Buffer = buffer;
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

        /// <summary>
        /// Gets the response buffer which <see cref="View"/> is read from, which is
        /// released by <see cref="ExitCollect"/>.
        /// </summary>
        internal readonly ResponseBuffer? Buffer { get; }
    }

    private readonly struct CollectStep
    {
        private CollectStep(
            CollectionResponse response,
            Task<CollectionResult>? pendingCollectionTask,
            Task<bool>? bufferFreedTask,
            ResponseBuffer? lease)
        {
            this.Response = response;
            this.PendingCollectionTask = pendingCollectionTask;
            this.BufferFreedTask = bufferFreedTask;
            this.Lease = lease;
        }

        // A non-null task means the caller must await the in-flight collection.
        public Task<CollectionResult>? PendingCollectionTask { get; }

        // A non-null task means a collection could not be started because every
        // response buffer for this protocol is still being read, and the caller must
        // await one being freed before attempting to enter a collection again.
        public Task<bool>? BufferFreedTask { get; }

        public CollectionResponse Response { get; }

        // True when there is nothing to await because Response already holds the answer.
        public bool IsCompleted => this.PendingCollectionTask is null && this.BufferFreedTask is null;

        // Non-null when this scrape registered the protocol with the in-flight
        // collection and took a reader on the response buffer it writes (so the
        // awaiting caller owns a reader that must be released), null when it is
        // only observing a collection it could not join.
        public ResponseBuffer? Lease { get; }

        public static CollectStep Completed(CollectionResponse response)
            => new(response, pendingCollectionTask: null, bufferFreedTask: null, lease: null);

        public static CollectStep Pending(Task<CollectionResult> pendingCollectionTask, ResponseBuffer? lease)
            => new(default, pendingCollectionTask, bufferFreedTask: null, lease);

        public static CollectStep WaitingForBuffer(Task<bool> bufferFreedTask)
            => new(default, pendingCollectionTask: null, bufferFreedTask, lease: null);
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
            string? help,
            bool deduplicate)
        {
            this.Metric = metric;
            this.PrometheusMetric = prometheusMetric;
            this.WriteType = writeType;
            this.WriteUnit = writeUnit;
            this.WriteHelp = writeHelp;
            this.Unit = unit;
            this.Help = help;
            this.Deduplicate = deduplicate;
        }

        public readonly Metric Metric { get; }

        public readonly PrometheusMetric PrometheusMetric { get; }

        public readonly bool WriteType { get; }

        public readonly bool WriteUnit { get; }

        public readonly bool WriteHelp { get; }

        public readonly string? Unit { get; }

        public readonly string? Help { get; }

        public readonly bool Deduplicate { get; }
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

    internal sealed class ResponseWriter : IDisposable
    {
        private readonly PrometheusCollectionManager manager;
        private readonly ResponseBuffer buffer;
        private readonly Lock gate = new();
        private readonly CancellationTokenSource cancellation = new();
        private TimeSpan lastProgress;
        private bool cancelling;
        private bool disposed;

        public ResponseWriter(PrometheusCollectionManager manager, ResponseBuffer buffer)
        {
            this.manager = manager;
            this.buffer = buffer;
            this.lastProgress = manager.GetElapsedTime();
            this.CancellationToken = this.cancellation.Token;
            buffer.AddWriter(this);
        }

        public CancellationToken CancellationToken { get; }

        public void RecordProgress()
        {
            lock (this.gate)
            {
                this.lastProgress = this.manager.GetElapsedTime();
            }
        }

        public void Dispose()
        {
            lock (this.gate)
            {
                if (this.disposed)
                {
                    return;
                }

                this.disposed = true;
                if (!this.cancelling)
                {
                    this.cancellation.Dispose();
                }
            }

            this.buffer.RemoveWriter(this);
        }

        public TimeSpan CancelIfIdle(TimeSpan elapsed, TimeSpan idleTimeout)
        {
            lock (this.gate)
            {
                if (this.disposed || this.cancelling || this.cancellation.IsCancellationRequested)
                {
                    return idleTimeout;
                }

                var remaining = idleTimeout - (elapsed - this.lastProgress);
                if (remaining > TimeSpan.Zero)
                {
                    return remaining < TimeSpan.FromMilliseconds(1) ? TimeSpan.FromMilliseconds(1) : remaining;
                }

                this.cancelling = true;
            }

            // Abort callbacks and their continuations must run without either the writer or
            // buffer lock held. Defer CTS disposal until cancellation finishes on older TFMs too.
            try
            {
                this.cancellation.Cancel();
            }
            catch (Exception ex)
            {
                PrometheusExporterEventSource.Log.FailedExport(ex);
            }
            finally
            {
                lock (this.gate)
                {
                    this.cancelling = false;
                    if (this.disposed)
                    {
                        this.cancellation.Dispose();
                    }
                }
            }

            return idleTimeout;
        }
    }

    /// <summary>
    /// A buffer which a collection writes the response for a protocol to, and the
    /// number of scrapes still reading the response written to it.
    /// </summary>
    /// <param name="size">The size of the response buffer.</param>
    /// <param name="onReadersDrained">Invoked once the last scrape reading the buffer has finished.</param>
    internal sealed class ResponseBuffer(int size, Action onReadersDrained)
    {
        private readonly Lock writersGate = new();
        private readonly HashSet<ResponseWriter> writers = [];
        private int readerCount;

        public byte[] Buffer { get; private set; } = new byte[size];

        public int ReaderCount
            => Interlocked.CompareExchange(ref this.readerCount, 0, 0);

        public void DecrementReaderCount()
        {
            if (Interlocked.Decrement(ref this.readerCount) < 1)
            {
                onReadersDrained();
            }
        }

        public void IncrementReaderCount()
            => Interlocked.Increment(ref this.readerCount);

        public void AddWriter(ResponseWriter writer)
        {
            lock (this.writersGate)
            {
                this.writers.Add(writer);
            }
        }

        public void RemoveWriter(ResponseWriter writer)
        {
            lock (this.writersGate)
            {
                this.writers.Remove(writer);
            }
        }

        public TimeSpan CancelIdleWriters(TimeSpan elapsed, TimeSpan idleTimeout)
        {
            ResponseWriter[] activeWriters;
            lock (this.writersGate)
            {
                activeWriters = [.. this.writers];
            }

            var nextCheck = idleTimeout;
            foreach (var writer in activeWriters)
            {
                var delay = writer.CancelIfIdle(elapsed, idleTimeout);
                if (delay < nextCheck)
                {
                    nextCheck = delay;
                }
            }

            return nextCheck;
        }

        public bool TryExpand(int maxBufferSize)
        {
            if (this.Buffer.Length >= maxBufferSize)
            {
                return false;
            }

            // Grow by doubling, but never past the configured maximum. Clamping
            // to the maximum (rather than refusing the grow outright when the
            // doubled size would overshoot) means the entire configured budget
            // is usable, with no unreachable remainder.
            var newBufferSize = (int)Math.Min((long)this.Buffer.Length * 2, maxBufferSize);

            var expanded = new byte[newBufferSize];
            this.Buffer.CopyTo(expanded, 0);
            this.Buffer = expanded;

            return true;
        }
    }

    private sealed class CollectionContext
    {
        private readonly Lock gate = new();
        private readonly HashSet<PrometheusProtocol> protocols = [];
        private readonly TaskCompletionSource<CollectionResult> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private HashSet<PrometheusProtocol>? speculativeProtocols;
        private PrometheusProtocol[]? frozenProtocols;

        public CollectionContext(in PrometheusProtocol protocol)
        {
            this.protocols.Add(protocol);
        }

        public Task<CollectionResult> Task => this.tcs.Task;

        public bool ContainsProtocol(in PrometheusProtocol protocol)
        {
            lock (this.gate)
            {
                return this.protocols.Contains(protocol);
            }
        }

        public PrometheusProtocol[] FreezeProtocols()
        {
            lock (this.gate)
            {
                return this.frozenProtocols ??= [.. this.protocols];
            }
        }

        public bool IsSpeculative(in PrometheusProtocol protocol)
        {
            lock (this.gate)
            {
                return this.speculativeProtocols?.Contains(protocol) is true;
            }
        }

        public void SetResult(CollectionResult result)
            => this.tcs.SetResult(result);

        public bool TryRegisterProtocol(in PrometheusProtocol protocol, PrometheusProtocolState state)
        {
            lock (this.gate)
            {
                if (this.protocols.Contains(protocol))
                {
                    // A scrape is now waiting for this protocol, so it is no longer written speculatively.
                    this.speculativeProtocols?.Remove(protocol);
                    return true;
                }

                if (this.frozenProtocols is not null)
                {
                    return false;
                }

                if (!state.TryReserveBuffer())
                {
                    return false;
                }

                this.protocols.Add(protocol);
                return true;
            }
        }

        public void TryRegisterSpeculativeProtocol(in PrometheusProtocol protocol, PrometheusProtocolState state)
        {
            lock (this.gate)
            {
                if (this.frozenProtocols is not null || this.protocols.Contains(protocol) || !state.TryReserveBuffer())
                {
                    return;
                }

                this.protocols.Add(protocol);
                this.speculativeProtocols ??= [];
                this.speculativeProtocols.Add(protocol);
            }
        }
    }

    private sealed class PrometheusProtocolState
    {
        private const int InitialBufferSize = PrometheusExporterOptions.InitialScrapeResponseSizeBytes;
        private readonly int maxBufferSize;
        private readonly Action onReadersDrained;
        private readonly Lock bufferFreedGate = new();

        // The buffer which View is read from.
        private ResponseBuffer published;

        // A second buffer, only allocated once a collection has to write this protocol
        // while scrapes are still reading the response in the published buffer. A
        // collection never waits for those scrapes to finish, as how long they take is
        // decided by their clients, so it writes the other buffer instead.
        private ResponseBuffer? spare;

        // The buffer which the in-flight collection writes this protocol to, if any.
        private ResponseBuffer? target;

        // Completed once a buffer is free again, for a scrape which has to wait for one.
        private TaskCompletionSource<bool>? bufferFreed;

        public PrometheusProtocolState(int maxBufferSize)
        {
            this.maxBufferSize = Math.Max(maxBufferSize, InitialBufferSize);
            this.onReadersDrained = this.OnReadersDrained;
            this.published = new ResponseBuffer(InitialBufferSize, this.onReadersDrained);
        }

        public static ArraySegment<byte> EmptyView { get; } =
#if NET
            ArraySegment<byte>.Empty;
#else
            new([]);
#endif

        /// <summary>
        /// Gets the buffer which the in-flight collection writes this protocol to.
        /// </summary>
        public byte[] Buffer => this.target!.Buffer;

        public ArraySegment<byte> View { get; private set; } = EmptyView;

        public DateTime? GeneratedAt { get; private set; }

        public TimeSpan? GeneratedAtElapsed { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the most recent scrape for this protocol arrived near a collection.
        /// </summary>
        public bool LastRequestNearCollection { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this protocol has been requested since a collection last wrote it.
        /// </summary>
        public bool RequestedSinceWritten { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the collection which last wrote this protocol did so
        /// speculatively, rather than for a scrape which requested it.
        /// </summary>
        public bool WrittenSpeculatively { get; private set; }

        public int ReaderCount
            => this.published.ReaderCount + (this.spare?.ReaderCount ?? 0);

        /// <summary>
        /// Takes a reader on the buffer reserved for the in-flight collection, for a
        /// scrape which will read the response the collection writes to it.
        /// </summary>
        /// <returns>
        /// The buffer the reader was taken on.
        /// </returns>
        public ResponseBuffer AcquireTargetBuffer()
        {
            var target = this.target!;
            target.IncrementReaderCount();
            return target;
        }

        /// <summary>
        /// Takes a reader on the published buffer and returns the response in it, if there is one.
        /// </summary>
        /// <param name="fromCache">Whether the response is being served from the cache.</param>
        /// <param name="response">The response, when there is one.</param>
        /// <returns>
        /// <see langword="true"/> if there was a response; otherwise <see langword="false"/>.
        /// </returns>
        public bool TryAcquireView(bool fromCache, out CollectionResponse response)
        {
            if (this.GeneratedAt is not { } generatedAt)
            {
                response = default;
                return false;
            }

            this.published.IncrementReaderCount();
            response = new CollectionResponse(this.View, generatedAt, fromCache, this.published);

            return true;
        }

        /// <summary>
        /// Reserves a buffer which no scrape is reading for a collection to write this protocol to.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if a buffer was reserved; <see langword="false"/> if
        /// scrapes are still reading the responses in every buffer.
        /// </returns>
        public bool TryReserveBuffer()
        {
            Debug.Assert(this.target is null, "A buffer is already reserved for this protocol.");

            // Rewrite the published buffer in place whenever possible, so the spare is only
            // allocated for scrapes which overlap. A scrape cannot start reading it while it is
            // reserved, as the protocol is registered with the in-flight collection until then.
            if (this.published.ReaderCount == 0)
            {
                this.target = this.published;
                return true;
            }

            // Scrapes only start reading a buffer while it is published, so the readers of the
            // spare can only drain, and it becomes free again once they have all finished.
            this.spare ??= new ResponseBuffer(this.published.Buffer.Length, this.onReadersDrained);

            if (this.spare.ReaderCount == 0)
            {
                this.target = this.spare;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets a task which completes once a buffer is free for a collection to write this
        /// protocol to, or <see langword="null"/> when one is free right now.
        /// </summary>
        /// <returns>
        /// The task to wait on, or <see langword="null"/> when there is nothing to wait for.
        /// </returns>
        public Task<bool>? GetBufferFreedTask()
        {
            lock (this.bufferFreedGate)
            {
                // A buffer's reader count is published before OnReadersDrained takes the
                // gate, so a buffer which has already been freed is seen here and reported
                // as nothing to wait for. Any reader still active is guaranteed to signal.
                if (this.published.ReaderCount == 0 || this.spare is null || this.spare.ReaderCount == 0)
                {
                    return null;
                }

                this.bufferFreed ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                return this.bufferFreed.Task;
            }
        }

        public TimeSpan CancelIdleWriters(TimeSpan elapsed, TimeSpan idleTimeout)
        {
            var published = this.published;
            var spare = this.spare;
            var delay = published.CancelIdleWriters(elapsed, idleTimeout);
            var spareDelay = spare?.CancelIdleWriters(elapsed, idleTimeout) ?? idleTimeout;
            return delay < spareDelay ? delay : spareDelay;
        }

        public void OnRequested(bool nearCollection)
        {
            this.LastRequestNearCollection = nearCollection;
            this.RequestedSinceWritten = true;
        }

        public void OnWritten(bool speculative)
        {
            this.RequestedSinceWritten = false;
            this.WrittenSpeculatively = speculative;
            this.target = null;
        }

        public bool TryExpandBuffer()
            => this.target!.TryExpand(this.maxBufferSize);

        public void ResetView()
        {
            // A protocol registered with a collection while its previous view was still
            // cached must not serve the empty view from the cache when the write fails.
            this.View = EmptyView;
            this.GeneratedAt = null;
            this.GeneratedAtElapsed = null;
        }

        public void UpdateView(int cursor)
        {
            var target = this.target!;

            this.View = new ArraySegment<byte>(target.Buffer, 0, cursor);

            if (!ReferenceEquals(target, this.published))
            {
                // Scrapes still reading the previous response keep the buffer it is in
                this.spare = this.published;
                this.published = target;
            }
        }

        public void UpdateTimestamps(DateTime timestamp, TimeSpan elapsed)
        {
            this.GeneratedAt = timestamp;
            this.GeneratedAtElapsed = elapsed;
        }

        private void OnReadersDrained()
        {
            TaskCompletionSource<bool>? bufferFreed;

            lock (this.bufferFreedGate)
            {
                bufferFreed = this.bufferFreed;
                this.bufferFreed = null;
            }

            // Signalled outside the gate so that no continuation runs while it is held.
            bufferFreed?.TrySetResult(true);
        }
    }
}
