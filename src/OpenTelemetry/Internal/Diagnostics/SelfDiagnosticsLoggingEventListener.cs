// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace OpenTelemetry.Internal;

/// <summary>
/// An <see cref="EventListener"/> that subscribes to all <c>OpenTelemetry-*</c>
/// <see cref="EventSource"/>s and forwards their events to a <see cref="SelfDiagnosticsLogger"/>.
/// </summary>
/// <remarks>
/// Contextual data (timestamp, thread, activity context) is captured at event time so entries
/// remain accurate after spending time in the dispatcher queue or deferred buffer.
/// </remarks>
internal sealed class SelfDiagnosticsLoggingEventListener : EventListener
{
    internal const string OpenTelemetryEventSourceNamePrefix = "OpenTelemetry-";

    private static readonly WaitCallback ProcessReconciliationCallback = static state =>
    {
        var workItem = (SourceReconciliationWorkItem)state!;

        try
        {
            workItem.Listener.ProcessReconciliation(workItem.Subscription);
        }
        catch
        {
            // A failed deferred subscription update must not escape a ThreadPool callback.
        }
    };

    private readonly SelfDiagnosticsLogger logger;
    private readonly Lock subscriptionLock = new();

    // All sources we have subscribed (or would subscribe when the level allows), so
    // UpdateLevel() can re-subscribe. Each source owns its reconciliation state because
    // EnableEvents must not be called concurrently for the same source.
    private readonly List<SourceSubscription> subscribedSources = [];

    // Sources that fired OnEventSourceCreated before the constructor finished.
    // Nulled out (under lock) once the constructor body has run, signalling that
    // subsequent sources should be subscribed directly.
    private readonly List<EventSource>? preConstructorSources = [];

    private volatile LogLevel currentLevel;
    private volatile bool disposed;

    internal SelfDiagnosticsLoggingEventListener(SelfDiagnosticsLogger logger, LogLevel logLevel)
    {
        this.logger = logger;
        this.currentLevel = logLevel;

        List<EventSource>? pending;

        lock (this.subscriptionLock)
        {
            pending = this.preConstructorSources;
            this.preConstructorSources = null; // signal: constructor is done
        }

        if (pending is not null)
        {
            foreach (var source in pending)
            {
                SourceSubscription subscription;

                lock (this.subscriptionLock)
                {
                    subscription = new SourceSubscription(source);
                    this.subscribedSources.Add(subscription);
                }

                this.ReconcileSource(subscription);
            }
        }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        this.disposed = true;
        base.Dispose();
    }

    /// <summary>
    /// Updates the EventSource subscription level for all already-subscribed sources.
    /// Called by <see cref="SelfDiagnostics"/> when options reload.
    /// <see cref="LogLevel.None"/> disables event delivery entirely.
    /// </summary>
    /// <param name="logLevel">The new minimum log level.</param>
    internal void UpdateLevel(LogLevel logLevel)
    {
        // Volatile write BEFORE the snapshot: a source added concurrently either appears in the
        // snapshot or requests reconciliation itself, and both paths read this level.
        this.currentLevel = logLevel;

        List<SourceSubscription> snapshot;
        lock (this.subscriptionLock)
        {
            snapshot = [.. this.subscribedSources];
        }

        foreach (var subscription in snapshot)
        {
            this.ReconcileSource(subscription);
        }
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name.StartsWith(OpenTelemetryEventSourceNamePrefix, StringComparison.Ordinal))
        {
            SourceSubscription? subscription = null;

            lock (this.subscriptionLock)
            {
                if (this.preConstructorSources is not null)
                {
                    // Constructor hasn't finished yet - defer to the constructor's post-lock loop.
                    this.preConstructorSources.Add(eventSource);
                }
                else
                {
                    subscription = new SourceSubscription(eventSource);
                    this.subscribedSources.Add(subscription);
                }
            }

            if (subscription is not null)
            {
                this.ReconcileSource(subscription);
            }
        }

        base.OnEventSourceCreated(eventSource);
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        try
        {
            this.OnEventWrittenCore(eventData);
        }
        catch
        {
            // Swallowing is deliberate: the runtime re-dispatches OnEventWritten exceptions as
            // EventSourceMessage events (looping back into this handler), and sources created
            // with ThrowOnEventWriteErrors rethrow at the SDK's WriteEvent call site.
            // Reporting the failure would re-enter the write path that just failed.
        }
    }

    private static void AppendEventBody(StringBuilder builder, EventWrittenEventArgs eventData)
    {
        var payload = eventData.Payload;
        var message = eventData.Message;

        if (message is not null)
        {
            if (payload is null || payload.Count == 0)
            {
                builder.Append(message);
                return;
            }

            try
            {
                builder.Append(string.Format(CultureInfo.InvariantCulture, message, [.. payload]));
                return;
            }
            catch
            {
                // The message and its payload disagree (or a payload value failed to format);
                // fall through and render the payload raw rather than losing the event.
            }
        }

        AppendRawPayload(builder, eventData);
    }

    private static void AppendRawPayload(StringBuilder builder, EventWrittenEventArgs eventData)
    {
        var payload = eventData.Payload;
        if (payload is null || payload.Count == 0)
        {
            return;
        }

        var names = eventData.PayloadNames;

        for (var i = 0; i < payload.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(" | ");
            }

            if (names is not null && i < names.Count)
            {
                builder.Append(names[i]).Append('=');
            }

            try
            {
                builder.Append(payload[i]?.ToString() ?? "null");
            }
            catch (Exception ex)
            {
                // One payload value with a throwing ToString must not cost the whole event.
                builder.Append("(unrenderable: ").Append(ex.GetType().Name).Append(')');
            }
        }
    }

    private static LogLevel MapEventLevel(EventLevel level) => level switch
    {
        EventLevel.Critical => LogLevel.Critical,
        EventLevel.Error => LogLevel.Error,
        EventLevel.Warning => LogLevel.Warning,
        EventLevel.Informational => LogLevel.Information,
        EventLevel.Verbose => LogLevel.Debug,
        EventLevel.LogAlways => LogLevel.Information,
        _ => LogLevel.None,
    };

    private static EventLevel MapLogLevel(LogLevel level) => level switch
    {
        LogLevel.Trace or LogLevel.Debug => EventLevel.Verbose,
        LogLevel.Information => EventLevel.Informational,
        LogLevel.Warning => EventLevel.Warning,
        LogLevel.Error => EventLevel.Error,
        LogLevel.Critical => EventLevel.Critical,

        // LogLevel.None never reaches here: ApplyLevel routes it to DisableEvents.
        _ => EventLevel.LogAlways,
    };

    private void OnEventWrittenCore(EventWrittenEventArgs eventData)
    {
        // Workaround for https://github.com/dotnet/runtime/issues/31927
        // EventCounters are published to all EventListeners regardless of which EventSource
        // providers the listener is enabled for.
        if (!eventData.EventSource.Name.StartsWith(OpenTelemetryEventSourceNamePrefix, StringComparison.Ordinal))
        {
            return;
        }

        var logLevel = MapEventLevel(eventData.Level);

        if (!this.logger.IsEnabled(logLevel))
        {
            return;
        }

#if NETSTANDARD2_0 || NET462
        var timestamp = DateTime.UtcNow; // best effort in absence of real event timestamp
        var osThreadId = 0L;
#else
        var timestamp = eventData.TimeStamp;
        var osThreadId = eventData.OSThreadId;
#endif

        var builder = StringBuilderCache.Acquire();
        builder.Append(eventData.EventSource.Name).Append(": ");
        AppendEventBody(builder, eventData);

        var message = StringBuilderCache.GetStringAndRelease(builder);

        // Capture timestamp, thread, and Activity.Current at event time so that entries queued
        // in the dispatcher (or the deferred buffer) render with the context of the moment they
        // were emitted, not of the pump thread that later writes them.
        var entry = new SelfDiagnosticsLogEntry(
            timestamp,
            osThreadId,
            logLevel,
            new EventId(eventData.EventId, eventData.EventName),
            message,
            exception: null,
            Activity.Current?.Context);

        this.logger.Write(in entry);
    }

    /// <summary>
    /// Requests reconciliation of a source with the current level. Concurrent callers only
    /// request another pass: exactly one caller applies levels for a source at a time. This
    /// avoids a stale <c>EnableEvents</c> call from one reconfiguration racing a later
    /// <c>DisableEvents</c> or <c>EnableEvents</c> call from another.
    /// </summary>
    /// <remarks>
    /// No lock is held across the runtime subscription calls because this method can run from
    /// <see cref="OnEventSourceCreated(EventSource)"/>, while the runtime holds its internal
    /// EventListener lock.
    /// </remarks>
    private void ReconcileSource(SourceSubscription subscription)
    {
        if (this.disposed)
        {
            return;
        }

        Volatile.Write(ref subscription.ReconciliationRequested, 1);

        if (Interlocked.CompareExchange(ref subscription.ReconciliationInProgress, 1, 0) != 0)
        {
            return;
        }

        this.ProcessReconciliation(subscription);
    }

    private void ProcessReconciliation(SourceSubscription subscription)
    {
        Volatile.Write(ref subscription.ReconciliationRequested, 0);

        var level = this.currentLevel;
        try
        {
            if (!this.disposed)
            {
                this.ApplyLevel(subscription.Source, level);
            }
        }
        catch
        {
            Volatile.Write(ref subscription.ReconciliationInProgress, 0);
            throw;
        }

        Volatile.Write(ref subscription.ReconciliationInProgress, 0);

        if (this.disposed
            || (this.currentLevel == level
                && Volatile.Read(ref subscription.ReconciliationRequested) == 0)
            || Interlocked.CompareExchange(ref subscription.ReconciliationInProgress, 1, 0) != 0)
        {
            return;
        }

        // The source changed level while the pass was running. Schedule its next pass rather
        // than spinning inside an EventListener callback if configuration keeps changing.
        ThreadPool.UnsafeQueueUserWorkItem(
            ProcessReconciliationCallback,
            new SourceReconciliationWorkItem(this, subscription));
    }

    private void ApplyLevel(EventSource source, LogLevel level)
    {
        if (level == LogLevel.None)
        {
            this.DisableEvents(source);
        }
        else
        {
            // Re-calling EnableEvents on an active source updates the level in place.
            this.EnableEvents(source, MapLogLevel(level), EventKeywords.All);
        }
    }

    private sealed class SourceSubscription
    {
        internal int ReconciliationInProgress;
        internal int ReconciliationRequested;

        internal SourceSubscription(EventSource source)
        {
            this.Source = source;
        }

        internal EventSource Source { get; }
    }

    private sealed class SourceReconciliationWorkItem
    {
        internal SourceReconciliationWorkItem(
            SelfDiagnosticsLoggingEventListener listener,
            SourceSubscription subscription)
        {
            this.Listener = listener;
            this.Subscription = subscription;
        }

        internal SelfDiagnosticsLoggingEventListener Listener { get; }

        internal SourceSubscription Subscription { get; }
    }
}
