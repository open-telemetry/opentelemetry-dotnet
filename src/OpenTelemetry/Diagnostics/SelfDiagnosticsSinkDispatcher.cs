// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Diagnostics;

/// <summary>
/// Owns the single bounded entry queue, the single background pump task, the active sink set,
/// and the minimum-level filter for SDK self-diagnostics. All sinks - file, console, external
/// loggers - are fed from here, so application threads never block on sink I/O.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lifecycle:</b> the dispatcher starts in <i>deferred</i> mode: entries are buffered (still
/// bounded) and the pump is not running. <see cref="QueueConfiguration"/> (production) and
/// <see cref="Activate"/> (tests) each start the pump, install the resolved sink set, and drain
/// the buffer discarding entries below the resolved minimum level.
/// </para>
/// <para>
/// <b>Backpressure:</b> the queue is capped at <see cref="DefaultMaxQueuedEntries"/> entries.
/// When full, new entries are counted and discarded (drop-newest: the entries leading up to a
/// stall are usually the diagnostic gold). The pump emits one warning summarizing the drop count
/// when pressure subsides, so data loss is always visible in the output.
/// </para>
/// <para>
/// <b>Format-once:</b> per entry, each distinct <see cref="ISelfDiagnosticsFormatter"/> instance
/// among the enabled sinks is invoked at most once; sinks sharing an instance share the result.
/// Sinks with a <see langword="null"/> formatter receive the raw entry only.
/// </para>
/// </remarks>
internal sealed class SelfDiagnosticsSinkDispatcher : IDisposable
{
    internal const int DefaultMaxQueuedEntries = 2048;

    private static readonly TimeSpan DisposeDrainTimeout = TimeSpan.FromSeconds(5);

    private readonly ConcurrentQueue<WorkItem> queue = new();
    private readonly SemaphoreSlim signal = new(0);
    private readonly Lock stateLock = new();
    private readonly int maxQueuedEntries;

    private volatile ISelfDiagnosticsSink[] sinks = [];
    private volatile LogLevel minimumLevel = LogLevel.Trace;
    private volatile bool deferred = true;
    private volatile bool configuredSinkExpected = true;
    private volatile bool useConfiguredSinkGate;
    private volatile bool disposed;

    private SelfDiagnosticsSinkManager? sinkManager;
    private Task? pumpTask; // guarded by stateLock
    private bool activated; // guarded by stateLock
    private int queuedCount;
    private int enqueueWriters;
    private int signalPending;
    private long droppedCount;
    private long latestAppliedGeneration = -1;

    // Applies only when draining the buffer captured before the first configuration was applied,
    // where the level entries were admitted under can differ from the level that configuration
    // resolved to. Live entries are never re-filtered - see the Entry case in PumpAsync.
    private LogLevel drainMinimumLevel = LogLevel.Trace;

    internal SelfDiagnosticsSinkDispatcher(int maxQueuedEntries = DefaultMaxQueuedEntries)
    {
        this.maxQueuedEntries = maxQueuedEntries;
    }

    private enum WorkItemKind
    {
        Entry,
        Configuration,
        ReplaceSinks,
        Level,
        Shutdown,
    }

    /// <summary>
    /// Gets the number of entries dropped since the last pump report. Exposed for tests.
    /// </summary>
    internal long DroppedCount => Interlocked.Read(ref this.droppedCount);

    /// <summary>
    /// Gets the pump task, or <see langword="null"/> before activation. Exposed for tests.
    /// </summary>
    internal Task? PumpTask
    {
        get
        {
            lock (this.stateLock)
            {
                return this.pumpTask;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Task? pump;

        lock (this.stateLock)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            pump = this.pumpTask;
        }

        // Full fence. The handshake with Enqueue is a store (disposed) followed by a load
        // (enqueueWriters) on this side, and a store (enqueueWriters) followed by a load
        // (disposed) on the producer side. Monitor.Exit is a release barrier and does not
        // order a later load against the earlier store, so without this both sides can miss
        // each other and a producer can reach the semaphore after the pump has disposed it.
        Interlocked.MemoryBarrier();

        SpinWait spinner = default;
        while (Volatile.Read(ref this.enqueueWriters) != 0)
        {
            spinner.SpinOnce();
        }

        if (pump is null)
        {
            while (this.queue.TryDequeue(out var abandoned))
            {
                if (abandoned.Kind == WorkItemKind.Entry)
                {
                    Interlocked.Decrement(ref this.queuedCount);
                }
            }

            this.signal.Dispose();
            return;
        }

        this.queue.Enqueue(WorkItem.ForShutdown());
        this.SignalPump();

        try
        {
            pump.Wait(DisposeDrainTimeout);
        }
        catch (AggregateException)
        {
            // The pump isolates sink failures, but disposal remains best-effort if an unexpected
            // task failure is surfaced here.
        }
    }

    internal void SetSinkManager(SelfDiagnosticsSinkManager sinkManager)
    {
        lock (this.stateLock)
        {
            if (this.activated)
            {
                throw new InvalidOperationException("The sink manager cannot be replaced after activation.");
            }

            this.sinkManager = sinkManager;
        }
    }

    internal void PreparePending(SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration)
    {
        this.minimumLevel = configuration.EffectiveLevel;
        this.configuredSinkExpected = configuration.HasConfiguredSink;
        this.useConfiguredSinkGate = true;
        this.deferred = true;
    }

    /// <summary>
    /// Determines whether an entry at <paramref name="level"/> would currently be accepted.
    /// </summary>
    /// <param name="level">The candidate log level.</param>
    /// <returns><see langword="true"/> when the entry should be captured.</returns>
    internal bool IsEnabled(LogLevel level)
    {
        if (this.disposed || level == LogLevel.None || level < this.minimumLevel)
        {
            return false;
        }

        if (this.deferred || this.useConfiguredSinkGate)
        {
            return this.configuredSinkExpected;
        }

        foreach (var sink in this.sinks)
        {
            if (sink.IsEnabled(level))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Enqueues an entry for the pump. When the bounded entry capacity is full, the newest
    /// entry is counted as dropped. Lifecycle commands do not consume this capacity.
    /// </summary>
    /// <param name="entry">The captured entry.</param>
    internal void Enqueue(in SelfDiagnosticsLogEntry entry)
    {
        Interlocked.Increment(ref this.enqueueWriters);
        try
        {
            if (this.disposed)
            {
                return;
            }

            if (!this.deferred && entry.Level < this.minimumLevel)
            {
                return;
            }

            if (Interlocked.Increment(ref this.queuedCount) > this.maxQueuedEntries)
            {
                Interlocked.Decrement(ref this.queuedCount);
                Interlocked.Increment(ref this.droppedCount);
                return;
            }

            this.queue.Enqueue(WorkItem.ForEntry(entry));
            this.SignalPump();
        }
        finally
        {
            Interlocked.Decrement(ref this.enqueueWriters);
        }
    }

    internal bool QueueConfiguration(
        SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration,
        long generation,
        Action<long, bool, SelfDiagnosticsOptions.SelfDiagnosticsConfiguration>? appliedCallback)
    {
        lock (this.stateLock)
        {
            if (this.disposed || this.sinkManager is null)
            {
                return false;
            }

            this.minimumLevel = configuration.EffectiveLevel;
            this.configuredSinkExpected = configuration.HasConfiguredSink;
            this.useConfiguredSinkGate = true;

            this.queue.Enqueue(WorkItem.ForConfiguration(configuration, generation, appliedCallback));
            this.EnsurePumpStartedUnderLock();
            this.SignalPump();
        }

        return true;
    }

    /// <summary>
    /// Transitions from deferred to active with a caller-provided sink set. This surface is used
    /// by focused dispatcher tests; production configuration uses <see cref="QueueConfiguration"/>.
    /// </summary>
    /// <param name="sinks">The initial sinks.</param>
    /// <param name="minimumLevel">The minimum captured level.</param>
    /// <returns><see langword="true"/> when activation was queued.</returns>
    internal bool Activate(ISelfDiagnosticsSink[] sinks, LogLevel minimumLevel)
    {
        lock (this.stateLock)
        {
            if (this.disposed || this.activated)
            {
                return false;
            }

            this.minimumLevel = minimumLevel;
            this.configuredSinkExpected = sinks.Length > 0;
            this.useConfiguredSinkGate = true;
            this.queue.Enqueue(WorkItem.ForSinks(sinks, minimumLevel));
            this.EnsurePumpStartedUnderLock();
            this.SignalPump();
        }

        return true;
    }

    /// <summary>Queues a replacement caller-provided sink set.</summary>
    /// <param name="newSinks">The replacement sinks.</param>
    /// <returns><see langword="true"/> when replacement was queued.</returns>
    internal bool UpdateSinks(ISelfDiagnosticsSink[] newSinks)
    {
        lock (this.stateLock)
        {
            if (this.disposed || !this.activated)
            {
                return false;
            }

            this.configuredSinkExpected = newSinks.Length > 0;
            this.useConfiguredSinkGate = true;
            this.queue.Enqueue(WorkItem.ForSinks(newSinks));
            this.SignalPump();
        }

        return true;
    }

    /// <summary>Updates the minimum level applied to subsequently captured entries.</summary>
    /// <param name="level">The new minimum level.</param>
    internal void UpdateLevel(LogLevel level)
    {
        lock (this.stateLock)
        {
            if (this.disposed || !this.activated)
            {
                return;
            }

            this.minimumLevel = level;
            this.queue.Enqueue(WorkItem.ForLevel(level));
            this.SignalPump();
        }
    }

    /// <summary>Reports a failure of the diagnostics machinery itself.</summary>
    /// <param name="message">The failure description.</param>
    internal void ReportInternalError(string message)
    {
        try
        {
            Console.Error.WriteLine(message);
        }
        catch
        {
            // Nowhere left to report.
        }

        if (this.IsEnabled(LogLevel.Error))
        {
            var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Error, default, message, null);
            this.Enqueue(in entry);
        }
    }

    private void EnsurePumpStartedUnderLock()
    {
        if (this.pumpTask is null)
        {
            this.activated = true;
            this.pumpTask = Task.Run(this.PumpAsync);
        }
    }

    /// <summary>
    /// Wakes the pump, coalescing signals: one wakeup drains the whole queue, so a release is
    /// only needed when no wakeup is already pending. Without coalescing, N enqueued items
    /// accumulate N semaphore counts and the pump spins through N-1 empty passes per burst.
    /// </summary>
    private void SignalPump()
    {
        if (Interlocked.Exchange(ref this.signalPending, 1) == 0)
        {
            try
            {
                this.signal.Release();
            }
            catch (ObjectDisposedException)
            {
                // The pump owns the semaphore and disposes it as it exits. Two callers can race
                // that exit: a producer that slipped past the disposed check, and Dispose itself
                // signalling a pump that had already faulted out. Neither has anything left to
                // wake, and Dispose runs from an AppDomain.ProcessExit handler where an escaping
                // exception is not recoverable.
            }
        }
    }

    private async Task PumpAsync()
    {
        var deferredEntries = new List<SelfDiagnosticsLogEntry>();

        try
        {
            // Terminates on the Shutdown work item, which Dispose always enqueues before
            // waiting on this task; the loop has no other exit and needs no attempt budget.
            while (true)
            {
                await this.signal.WaitAsync().ConfigureAwait(false);

                // Allow the next producer to signal again before draining: an item enqueued
                // during the drain either lands in this pass or triggers one more wakeup.
                Interlocked.Exchange(ref this.signalPending, 0);

                var wroteAny = false;
                var shutdown = false;

                while (this.queue.TryDequeue(out var workItem))
                {
                    switch (workItem.Kind)
                    {
                        case WorkItemKind.Entry:
                            Interlocked.Decrement(ref this.queuedCount);
                            var entry = workItem.Entry;
                            if (this.deferred)
                            {
                                deferredEntries.Add(entry);
                            }
                            else if (entry.Level != LogLevel.None)
                            {
                                // Admission is decided once, in Enqueue. Re-filtering here
                                // against a level the pump has not applied yet would silently
                                // discard entries accepted under a level that a concurrent
                                // QueueConfiguration had already published - exactly the
                                // entries a level raise during an incident is meant to capture.
                                //
                                // QueueConfiguration publishes minimumLevel before it enqueues
                                // its work item and Enqueue takes no lock, so a producer
                                // preempting it in between lands ahead of the configuration.
                                // That interleaving needs a context switch at a specific
                                // instruction boundary and is not reproducible from a test, so
                                // this is guarded by construction rather than by a regression
                                // test: the pump never second-guesses an accepted entry.
                                wroteAny |= this.WriteToSinks(in entry);
                            }

                            break;

                        case WorkItemKind.Configuration:
                            this.ApplyConfiguration(workItem);
                            if (!this.deferred)
                            {
                                wroteAny |= this.DrainDeferredEntries(deferredEntries);
                            }

                            break;

                        case WorkItemKind.ReplaceSinks:
                            this.ReplaceSinks(workItem.Sinks!);
                            if (workItem.MinimumLevel.HasValue)
                            {
                                this.drainMinimumLevel = workItem.MinimumLevel.GetValueOrDefault();
                            }

                            this.deferred = false;
                            this.useConfiguredSinkGate = false;
                            wroteAny |= this.DrainDeferredEntries(deferredEntries);
                            break;

                        case WorkItemKind.Level:
                            this.drainMinimumLevel = workItem.MinimumLevel.GetValueOrDefault();
                            break;

                        case WorkItemKind.Shutdown:
                            shutdown = true;
                            break;
                    }

                    if (shutdown)
                    {
                        break;
                    }
                }

                wroteAny |= this.ReportDroppedEntries();
                if (wroteAny)
                {
                    this.FlushSinks();
                }

                if (shutdown)
                {
                    return;
                }
            }
        }
        finally
        {
            this.DisposeSinks();
            this.signal.Dispose();
        }
    }

    private void ApplyConfiguration(in WorkItem workItem)
    {
        if (workItem.Generation <= this.latestAppliedGeneration)
        {
            // Superseded generation: skip the sink apply and the applied-callback, but still
            // clear the transition gates. Callers only queue monotonic generations today, so
            // this is defence in depth - returning with the gates still set would strand the
            // pump in deferred buffering with no further work item to release it.
            this.useConfiguredSinkGate = false;
            this.deferred = false;
            return;
        }

        this.latestAppliedGeneration = workItem.Generation;
        var configuration = workItem.Configuration!;

        // Sink construction is designed not to throw, but the pump must survive if it ever
        // does: a dead pump disposes the semaphore while the dispatcher is still accepting
        // entries, surfacing ObjectDisposedException from Enqueue on application threads.
        // On failure the current sink set is retained and the state transitions below still
        // run, so the dispatcher cannot get stuck buffering in deferred mode.
        try
        {
            var newSinks = this.sinkManager!.ApplyOptions(configuration);
            this.ReplaceSinks(newSinks);
        }
        catch (Exception ex)
        {
            this.ReportInternalError(
                $"OpenTelemetry SDK self-diagnostics: applying sink configuration failed: {ex.Message}");
        }

        this.drainMinimumLevel = configuration.EffectiveLevel;
        this.minimumLevel = configuration.EffectiveLevel;
        this.configuredSinkExpected = this.sinks.Length > 0;
        this.useConfiguredSinkGate = false;
        this.deferred = false;

        try
        {
            workItem.AppliedCallback?.Invoke(
                workItem.Generation,
                this.sinks.Length > 0,
                configuration);
        }
        catch
        {
            // A lifecycle callback must not terminate the diagnostics pump.
        }
    }

    private bool DrainDeferredEntries(List<SelfDiagnosticsLogEntry> entries)
    {
        var wroteAny = false;

        foreach (var entry in entries)
        {
            if (entry.Level >= this.drainMinimumLevel && entry.Level != LogLevel.None)
            {
                wroteAny |= this.WriteToSinks(in entry);
            }
        }

        entries.Clear();
        return wroteAny;
    }

    private bool ReportDroppedEntries()
    {
        var dropped = Interlocked.Exchange(ref this.droppedCount, 0);
        if (dropped == 0)
        {
            return false;
        }

        var notice = SelfDiagnosticsLogEntry.Capture(
            LogLevel.Warning,
            default,
            $"{dropped} self-diagnostics entries were dropped because the buffer was full.",
            null);
        return this.WriteToSinks(in notice);
    }

    private void ReplaceSinks(ISelfDiagnosticsSink[] newSinks)
    {
        var oldSinks = this.sinks;
        this.sinks = newSinks;

        foreach (var oldSink in oldSinks)
        {
            if (Array.IndexOf(newSinks, oldSink) < 0)
            {
                try
                {
                    oldSink.Dispose();
                }
                catch
                {
                    // Best-effort disposal. All sink lifecycle operations remain on this thread.
                }
            }
        }
    }

    private void DisposeSinks()
    {
        var snapshot = this.sinks;
        this.sinks = [];

        foreach (var sink in snapshot)
        {
            try
            {
                sink.Dispose();
            }
            catch
            {
                // Best-effort disposal.
            }
        }
    }

    private void FlushSinks()
    {
        foreach (var sink in this.sinks)
        {
            try
            {
                sink.Flush();
            }
            catch
            {
                // A sink must not terminate the diagnostics pump.
            }
        }
    }

    private bool WriteToSinks(in SelfDiagnosticsLogEntry entry)
    {
        var snapshot = this.sinks;
        var wrote = false;
        string? formatted = null;
        ISelfDiagnosticsFormatter? formattedBy = null;

        foreach (var sink in snapshot)
        {
            try
            {
                if (!sink.IsEnabled(entry.Level))
                {
                    continue;
                }

                string? text = null;
                var formatter = sink.Formatter;
                if (formatter is not null)
                {
                    if (!ReferenceEquals(formatter, formattedBy))
                    {
                        formatted = formatter.Format(in entry);
                        formattedBy = formatter;
                    }

                    text = formatted;
                }

                sink.Write(in entry, text);
                wrote = true;
            }
            catch
            {
                // Isolate sink and formatter failures.
            }
        }

        return wrote;
    }

    private readonly struct WorkItem
    {
        private WorkItem(
            WorkItemKind kind,
            SelfDiagnosticsLogEntry entry = default,
            SelfDiagnosticsOptions.SelfDiagnosticsConfiguration? configuration = null,
            long generation = 0,
            ISelfDiagnosticsSink[]? sinks = null,
            LogLevel? minimumLevel = null,
            Action<long, bool, SelfDiagnosticsOptions.SelfDiagnosticsConfiguration>? appliedCallback = null)
        {
            this.Kind = kind;
            this.Entry = entry;
            this.Configuration = configuration;
            this.Generation = generation;
            this.Sinks = sinks;
            this.MinimumLevel = minimumLevel;
            this.AppliedCallback = appliedCallback;
        }

        internal WorkItemKind Kind { get; }

        internal SelfDiagnosticsLogEntry Entry { get; }

        internal SelfDiagnosticsOptions.SelfDiagnosticsConfiguration? Configuration { get; }

        internal long Generation { get; }

        internal ISelfDiagnosticsSink[]? Sinks { get; }

        internal LogLevel? MinimumLevel { get; }

        internal Action<long, bool, SelfDiagnosticsOptions.SelfDiagnosticsConfiguration>? AppliedCallback { get; }

        internal static WorkItem ForEntry(in SelfDiagnosticsLogEntry entry)
            => new(WorkItemKind.Entry, entry: entry);

        internal static WorkItem ForConfiguration(
            SelfDiagnosticsOptions.SelfDiagnosticsConfiguration configuration,
            long generation,
            Action<long, bool, SelfDiagnosticsOptions.SelfDiagnosticsConfiguration>? appliedCallback)
            => new(
                WorkItemKind.Configuration,
                configuration: configuration,
                generation: generation,
                appliedCallback: appliedCallback);

        internal static WorkItem ForSinks(ISelfDiagnosticsSink[] sinks, LogLevel? minimumLevel = null)
            => new(WorkItemKind.ReplaceSinks, sinks: sinks, minimumLevel: minimumLevel);

        internal static WorkItem ForLevel(LogLevel minimumLevel)
            => new(WorkItemKind.Level, minimumLevel: minimumLevel);

        internal static WorkItem ForShutdown() => new(WorkItemKind.Shutdown);
    }
}
