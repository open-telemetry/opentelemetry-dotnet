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

    // Level applied when draining buffered pre-configuration entries, which may have been
    // admitted at a looser level than the resolved configuration.
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

        // Full fence: Monitor.Exit above is a release-only barrier. Without a full fence
        // here a producer can slip past the disposed check and reach a disposed semaphore.
        Interlocked.MemoryBarrier();

        // Bounded: runs from ProcessExit, where a stalled producer must not block
        // indefinitely. SignalPump swallows ObjectDisposedException on timeout.
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

        // The pump owns final drain, flush, and sink disposal. Timeout = best-effort fallback.
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

            // Also guard None here to avoid consuming queue capacity for entries IsEnabled already rejected.
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

    /// <summary>
    /// Reports a failure of the diagnostics machinery itself.
    /// </summary>
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
    /// Thread-pool starvation is a condition self-diagnostics may be investigating;
    /// a dedicated thread ensures the pump stays alive exactly when its output matters most.
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
            // One release per item: coalescing via a flag risks a lost wakeup if a producer
            // is suspended between setting the flag and releasing the semaphore.
            this.signal.Release();
        }
        catch (ObjectDisposedException)
        {
            // The pump disposes the semaphore as it exits. A racing producer or Dispose
            // call may arrive after that; neither has anything to wake.
        }
    }

    /// <summary>Hard exception boundary: an unhandled exception on a dedicated thread terminates the process.</summary>
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
            while (!this.pumpCts.IsCancellationRequested)
            {
                try
                {
                    this.signal.Wait(this.pumpCts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
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
            // Stale generation: skip sink replacement but still finalize the transition
            // if current, to avoid stranding the dispatcher in deferred mode.
            this.FinalizeConfigurationTransitionIfCurrent(workItem.Generation, default);
            return;
        }

        this.latestAppliedGeneration = workItem.Generation;
        var configuration = workItem.Configuration!;

        // Sink construction must not kill the pump: a dead pump would dispose the semaphore
        // while Enqueue is still running on application threads.
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
                    // Best-effort disposal.
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
                            // Record failure so this formatter is not retried for other sinks sharing the instance.
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
