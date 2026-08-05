// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;

// Aliased so XML doc crefs resolve: net462's mscorlib declares an internal System.LogLevel.
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace OpenTelemetry.Diagnostics;

/// <summary>
/// An <see cref="EventListener"/> that subscribes to all <c>OpenTelemetry-*</c>
/// <see cref="EventSource"/>s and forwards their events to a <see cref="SelfDiagnosticsLogger"/>
/// as pre-captured <see cref="SelfDiagnosticsLogEntry"/> values (event-time timestamp, OS
/// thread id, and activity context are all taken here, so entries render correctly even when
/// they sit in the dispatcher queue or deferred buffer).
/// </summary>
internal sealed class SelfDiagnosticsLoggingEventListener : EventListener
{
    internal const string OpenTelemetryEventSourceNamePrefix = "OpenTelemetry-";

    private const int MaxSubscribeAttempts = 5;

    private readonly SelfDiagnosticsLogger logger;
    private readonly Lock subscriptionLock = new();

    // All sources we have subscribed (or would subscribe when the level allows), so
    // UpdateLevel() can re-subscribe.
    private readonly List<EventSource> subscribedSources = [];

    // Sources that fired OnEventSourceCreated before the constructor finished.
    // Nulled out (under lock) once the constructor body has run, signalling that
    // subsequent sources should be subscribed directly.
    private readonly List<EventSource>? preConstructorSources = [];

    // The current subscription level. LogLevel.None means disabled (DisableEvents).
    private volatile LogLevel currentLevel;

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
                lock (this.subscriptionLock)
                {
                    this.subscribedSources.Add(source);
                }

                this.SubscribeSource(source);
            }
        }
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
        // snapshot (re-subscribed below, idempotent) or is subscribed by SubscribeSource, which
        // reads the level after this write (and re-checks after enabling).
        this.currentLevel = logLevel;

        List<EventSource> snapshot;
        lock (this.subscriptionLock)
        {
            snapshot = [.. this.subscribedSources];
        }

        foreach (var source in snapshot)
        {
            this.ApplyLevel(source, logLevel);
        }
    }

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (!eventSource.Name.StartsWith(OpenTelemetryEventSourceNamePrefix, StringComparison.Ordinal))
        {
            base.OnEventSourceCreated(eventSource);
            return;
        }

        lock (this.subscriptionLock)
        {
            if (this.preConstructorSources is not null)
            {
                // Constructor hasn't finished yet - defer to the constructor's post-lock loop.
                this.preConstructorSources.Add(eventSource);
                base.OnEventSourceCreated(eventSource);
                return;
            }

            this.subscribedSources.Add(eventSource);
        }

        this.SubscribeSource(eventSource);

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
            // Diagnostics must never break the SDK. The runtime does catch exceptions thrown
            // from OnEventWritten, but it then calls ReportOutOfBandMessage, which re-dispatches
            // an EventSourceMessage into every listener (this one included) and writes to the
            // attached debugger; and any source created with
            // EventSourceSettings.ThrowOnEventWriteErrors rethrows as EventSourceException at
            // the SDK's WriteEvent call site. Swallowing is deliberate: reporting the failure
            // would re-enter the write path that just failed. There is no seam to force a
            // failure here (payload values are runtime-serialized primitives), so this guard is
            // untested by design.
        }
    }

    /// <summary>
    /// Renders the event body: the manifest message with its payload substituted when both are
    /// present, otherwise the raw payload. Self-describing (TraceLogging) sources carry a
    /// <see langword="null"/> <see cref="EventWrittenEventArgs.Message"/> and put everything in
    /// the payload, so falling back to raw rendering is what keeps those events readable.
    /// </summary>
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
    /// Subscribes a newly-discovered source at the current level, re-applying if the level
    /// changed concurrently (closing the UpdateLevel race without holding the subscription
    /// lock across EnableEvents, which would risk lock-order inversion with the runtime's
    /// internal EventListener lock).
    /// </summary>
    /// <remarks>
    /// The retry budget is finite by design. The retry only closes the interleaving where a
    /// concurrent <see cref="UpdateLevel"/> applies its level before this call applies an
    /// already-stale one; because the source is added to <see cref="subscribedSources"/> before
    /// this method is called, it is included in every subsequent <see cref="UpdateLevel"/>
    /// snapshot, so exhausting the budget can only leave a level stale until the next update.
    /// </remarks>
    private void SubscribeSource(EventSource source)
    {
        for (var attempt = 0; attempt < MaxSubscribeAttempts; attempt++)
        {
            var level = this.currentLevel;
            this.ApplyLevel(source, level);

            if (this.currentLevel == level)
            {
                return;
            }
        }
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
}
