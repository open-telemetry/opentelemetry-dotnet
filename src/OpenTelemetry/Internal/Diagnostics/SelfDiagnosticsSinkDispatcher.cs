// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.SelfDiagnostics;

namespace OpenTelemetry.Internal;

/// <summary>
/// Owns the single bounded entry queue, the single background pump task, the active sink set,
/// and the minimum-level filter for SDK self-diagnostics. All sinks - file, console, external
/// loggers - are fed from here, so application threads never block on sink I/O.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lifecycle:</b> the dispatcher starts in <i>deferred</i> mode: entries are buffered (still
/// bounded) and the pump is not running. <see cref="QueueConfiguration"/> starts the pump,
/// installs the resolved sink set, and drains the buffer discarding entries below the resolved
/// minimum level.
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
    private static readonly TimeSpan EnqueueQuiesceTimeout = TimeSpan.FromSeconds(1);

    private readonly ConcurrentQueue<WorkItem> queue = new();
    private readonly SemaphoreSlim signal = new(0);
    private readonly Lock stateLock = new();
    private readonly int maxQueuedEntries;
    private readonly CancellationTokenSource pumpCts = new();
    private readonly Func<SelfDiagnosticsOptions.SelfDiagnosticsConfiguration, ISelfDiagnosticsSink[]>? sinkResolver;

    private volatile ISelfDiagnosticsSink[] sinks = [];
    private volatile LogLevel minimumLevel = LogLevel.Trace;
    private volatile bool deferred = true;
    private volatile bool configuredSinkExpected = true;
    private volatile bool useConfiguredSinkGate;
    private volatile bool disposed;
    private SelfDiagnosticsSinkManager? sinkManager;
    private Thread? pumpThread; // guarded by stateLock
    private bool activated; // guarded by stateLock
    private long latestQueuedGeneration = -1; // guarded by stateLock
    private int queuedCount;
    private int enqueueWriters;
    private long droppedCount;
    private long latestAppliedGeneration = -1;

    // Applies only when draining the buffer captured before the first configuration was applied,
    // where the level entries were admitted under can differ from the level that configuration
    // resolved to. Live entries are never re-filtered - see the Entry case in PumpAsync.
    private LogLevel drainMinimumLevel = LogLevel.Trace;

    internal SelfDiagnosticsSinkDispatcher(
        int maxQueuedEntries = DefaultMaxQueuedEntries,
        Func<SelfDiagnosticsOptions.SelfDiagnosticsConfiguration, ISelfDiagnosticsSink[]>? sinkResolver = null)
    {
        this.maxQueuedEntries = maxQueuedEntries;
        this.sinkResolver = sinkResolver;
    }

    private enum WorkItemKind
    {
        Entry,
        Configuration,
        Shutdown,
    }

    /// <summary>
    /// Gets the number of entries dropped since the last pump report. Exposed for tests.
    /// </summary>
    internal long DroppedCount => Interlocked.Read(ref this.droppedCount);

    /// <summary>
    /// Gets the pump thread, or <see langword="null"/> before activation. Exposed for tests.
    /// </summary>
    internal Thread? PumpThread
    {
        get
        {
            lock (this.stateLock)
            {
                return this.pumpThread;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Thread? pump;

        lock (this.stateLock)
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            pump = this.pumpThread;
        }

        // Full fence. The handshake with Enqueue is a store (disposed) followed by a load
        // (enqueueWriters) on this side, and a store (enqueueWriters) followed by a load
        // (disposed) on the producer side. Monitor.Exit is a release barrier and does not
        // order a later load against the earlier store, so without this both sides can miss
        // each other and a producer can reach the semaphore after the pump has disposed it.
        Interlocked.MemoryBarrier();

        // Bounded: this runs from an AppDomain.ProcessExit handler, where a producer thread
        // descheduled inside Enqueue must not be able to stall process exit indefinitely.
        // Giving up early risks an ObjectDisposedException on the semaphore, which SignalPump
        // already swallows, so the deadline is the safer trade.
        SpinWait spinner = default;
        var drainTimer = Stopwatch.StartNew();
        while (Volatile.Read(ref this.enqueueWriters) != 0 && drainTimer.Elapsed < EnqueueQuiesceTimeout)
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
            this.pumpCts.Dispose();
            return;
        }

        this.queue.Enqueue(WorkItem.ForShutdown());
        this.SignalPump();

        // Join rather than abandon: the pump owns the final drain, the last flush, and sink
        // disposal. The pump body never lets an exception escape, so this cannot rethrow; a
        // timeout means the drain is abandoned and disposal falls back to best-effort.
        pump.Join(DisposeDrainTimeout);

        // Safeguard: if the join timed out and the pump is still blocking on Wait (e.g. the
        // signal permit was not delivered), cancel the token to wake it.
        this.pumpCts.Cancel();
        this.pumpCts.Dispose();
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

            // LogLevel.None is rejected here as well as in IsEnabled. Without it a None entry
            // would consume bounded queue capacity only to be discarded by the pump, so the two
            // admission checks would disagree about what counts as an entry.
            if (entry.Level == LogLevel.None
                || (!this.deferred && entry.Level < this.minimumLevel))
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
            if (this.disposed || (this.sinkManager is null && this.sinkResolver is null))
            {
                return false;
            }

            this.minimumLevel = configuration.EffectiveLevel;
            this.configuredSinkExpected = configuration.HasConfiguredSink;
            this.useConfiguredSinkGate = true;
            this.latestQueuedGeneration = generation;

            this.queue.Enqueue(WorkItem.ForConfiguration(configuration, generation, appliedCallback));
            this.EnsurePumpStartedUnderLock();
            this.SignalPump();
        }

        return true;
    }

    /// <summary>Reports a failure of the diagnostics machinery itself.</summary>
    /// <remarks>
    /// Deliberately reported twice: standard error guarantees the message escapes even when the
    /// pump is dead or every sink is broken, and the queued entry puts it in the log file where
    /// support will actually look for it. The cost is one duplicated line on standard error when
    /// <see cref="SelfDiagnosticsOptions.LogToStderr"/> is set, which is preferred over a
    /// diagnostics failure that leaves no trace anywhere.
    /// </remarks>
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

    /// <summary>
    /// Starts the pump on a dedicated background thread rather than the thread pool.
    /// </summary>
    /// <remarks>
    /// Self-diagnostics exists to explain unhealthy processes, and thread-pool starvation is a
    /// condition it may be switched on to investigate. A pump that needed a pool
    /// thread in order to wake would stall exactly when its output matters most, so it gets a
    /// thread of its own. The cost is paid only once a configuration with a sink arrives: a
    /// silent SDK - the default - never creates it.
    /// </remarks>
    private void EnsurePumpStartedUnderLock()
    {
        if (this.pumpThread is null)
        {
            this.activated = true;
            this.pumpThread = new Thread(this.Pump)
            {
                IsBackground = true,
                Name = "OpenTelemetry.SelfDiagnostics",
            };

            this.pumpThread.Start();
        }
    }

    /// <summary>Wakes the pump after a work item has been queued.</summary>
    private void SignalPump()
    {
        try
        {
            // Each queued item releases the semaphore. Coalescing with a separate pending flag
            // leaves a lost-wakeup window between publishing that flag and releasing the
            // semaphore: if the publishing thread is suspended there, every other producer skips
            // the release and the pump remains blocked. Extra permits can only cause bounded
            // empty passes after a burst; a missing permit can stall the pump indefinitely.
            this.signal.Release();
        }
        catch (ObjectDisposedException)
        {
            // The pump owns the semaphore and disposes it as it exits. A producer can slip past
            // the disposed check, and Dispose can signal after a pump fault. Neither has anything
            // left to wake, and Dispose runs from an AppDomain.ProcessExit handler where an
            // escaping exception is not recoverable.
        }
    }

    /// <summary>
    /// Hard exception boundary for the pump thread.
    /// </summary>
    /// <remarks>
    /// An unhandled exception on a dedicated thread terminates the process, so nothing may
    /// escape. Per-sink, per-formatter, and per-callback failures are already isolated inside
    /// <see cref="PumpCore"/>, so reaching here means the dispatcher itself failed and diagnostics
    /// stop for the rest of the process.
    /// </remarks>
    private void Pump()
    {
        try
        {
            this.PumpCore();
        }
        catch
        {
            // Nothing left to report: the machinery that would report it is what just failed.
        }
    }

    private void PumpCore()
    {
        var deferredEntries = new List<SelfDiagnosticsLogEntry>();

        try
        {
            // Terminates on the Shutdown work item, which Dispose always enqueues before
            // waiting on this task; the loop has no other exit and needs no attempt budget.
            while (true)
            {
                try
                {
                    this.signal.Wait(this.pumpCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break; // Dispose cancelled the wait; exit so finally can clean up.
                }

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
                                // Admission is decided once in Enqueue; the pump never
                                // re-filters an accepted entry against a later level update.
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
            // A stale generation can only finalize a pending transition when it is still the
            // most recently queued configuration. Callers queue monotonic generations today, but
            // retaining this guard prevents a defensive stale-generation path from stranding the
            // dispatcher in deferred mode.
            this.FinalizeConfigurationTransitionIfCurrent(workItem.Generation, default);
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
            var newSinks = this.sinkResolver is not null
                ? this.sinkResolver(configuration)
                : this.sinkManager!.ApplyOptions(configuration);
            this.ReplaceSinks(newSinks);
        }
        catch (Exception ex)
        {
            this.ReportInternalError(
                $"OpenTelemetry SDK self-diagnostics: applying sink configuration failed: {ex.Message}");
        }

        this.FinalizeConfigurationTransitionIfCurrent(workItem.Generation, configuration);

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

    private void FinalizeConfigurationTransitionIfCurrent(
        long generation,
        SelfDiagnosticsOptions.SelfDiagnosticsConfiguration? configuration)
    {
        lock (this.stateLock)
        {
            if (generation != this.latestQueuedGeneration)
            {
                return;
            }

            if (configuration is not null)
            {
                this.drainMinimumLevel = configuration.EffectiveLevel;
                this.minimumLevel = configuration.EffectiveLevel;
                this.configuredSinkExpected = this.sinks.Length > 0;
            }

            this.useConfiguredSinkGate = false;
            this.deferred = false;
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

        foreach (var newSink in newSinks)
        {
            try
            {
                newSink.OnInstalled();
            }
            catch
            {
                // A post-install callback must not terminate the diagnostics pump.
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
        List<FormatterResult>? formatterResults = null;

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
                    formatterResults ??= [];
                    FormatterResult? existing = null;
                    foreach (var fr in formatterResults)
                    {
                        if (ReferenceEquals(fr.Formatter, formatter))
                        {
                            existing = fr;
                            break;
                        }
                    }

                    if (existing is { } existingValue)
                    {
                        if (!existingValue.Succeeded)
                        {
                            continue;
                        }

                        text = existingValue.Formatted;
                    }
                    else
                    {
                        try
                        {
                            text = formatter.Format(in entry);
                            formatterResults.Add(new FormatterResult(formatter, text, succeeded: true));
                        }
                        catch
                        {
                            // Record failures as well as results so a formatter is invoked at
                            // most once per entry even when several sinks share the instance.
                            formatterResults.Add(new FormatterResult(formatter, null, succeeded: false));
                            continue;
                        }
                    }
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

    private readonly struct FormatterResult
    {
        internal readonly ISelfDiagnosticsFormatter Formatter;
        internal readonly string? Formatted;
        internal readonly bool Succeeded;

        internal FormatterResult(ISelfDiagnosticsFormatter formatter, string? formatted, bool succeeded)
        {
            this.Formatter = formatter;
            this.Formatted = formatted;
            this.Succeeded = succeeded;
        }
    }

    private readonly struct WorkItem
    {
        private WorkItem(
            WorkItemKind kind,
            SelfDiagnosticsLogEntry entry = default,
            SelfDiagnosticsOptions.SelfDiagnosticsConfiguration? configuration = null,
            long generation = 0,
            Action<long, bool, SelfDiagnosticsOptions.SelfDiagnosticsConfiguration>? appliedCallback = null)
        {
            this.Kind = kind;
            this.Entry = entry;
            this.Configuration = configuration;
            this.Generation = generation;
            this.AppliedCallback = appliedCallback;
        }

        internal WorkItemKind Kind { get; }

        internal SelfDiagnosticsLogEntry Entry { get; }

        internal SelfDiagnosticsOptions.SelfDiagnosticsConfiguration? Configuration { get; }

        internal long Generation { get; }

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

        internal static WorkItem ForShutdown() => new(WorkItemKind.Shutdown);
    }
}
