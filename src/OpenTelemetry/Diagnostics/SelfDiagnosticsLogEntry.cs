// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Diagnostics;

/// <summary>
/// A fully-captured self-diagnostics log entry. This is the single currency passed between
/// the capture side (<see cref="SelfDiagnosticsLogger"/>, <see cref="SelfDiagnosticsLoggingEventListener"/>)
/// and the sink side (<see cref="SelfDiagnosticsSinkDispatcher"/> and its
/// <see cref="ISelfDiagnosticsSink"/>s).
/// </summary>
/// <remarks>
/// All contextual data (timestamp, thread, activity context) is captured at log time, never at
/// format time. This guarantees that entries which sit in the dispatcher queue - or in the
/// deferred-activation buffer, potentially for many seconds - render with the context of the
/// moment they were emitted rather than the context of the background thread that eventually
/// writes them.
/// </remarks>
internal readonly struct SelfDiagnosticsLogEntry
{
    public SelfDiagnosticsLogEntry(
        DateTime timestampUtc,
        long threadId,
        LogLevel level,
        EventId eventId,
        string message,
        Exception? exception,
        ActivityContext? activityContext)
    {
        this.TimestampUtc = timestampUtc;
        this.ThreadId = threadId;
        this.Level = level;
        this.EventId = eventId;
        this.Message = message;
        this.Exception = exception;
        this.ActivityContext = activityContext;
    }

    /// <summary>
    /// Gets the UTC timestamp captured when the entry was emitted.
    /// </summary>
    public DateTime TimestampUtc { get; }

    /// <summary>
    /// Gets the thread id captured when the entry was emitted. This is the OS thread id for
    /// entries sourced from <see cref="System.Diagnostics.Tracing.EventSource"/> events (on
    /// runtimes that expose it) and the managed thread id otherwise. Values &lt;= 0 indicate
    /// the id was unavailable and render as dashes.
    /// </summary>
    public long ThreadId { get; }

    /// <summary>Gets the level at which the entry was emitted. Never escalated or rewritten.</summary>
    public LogLevel Level { get; }

    /// <summary>Gets the event id associated with the entry, if any.</summary>
    public EventId EventId { get; }

    /// <summary>Gets the pre-rendered message text.</summary>
    public string Message { get; }

    /// <summary>Gets the exception associated with the entry, if any.</summary>
    public Exception? Exception { get; }

    /// <summary>
    /// Gets the <see cref="System.Diagnostics.ActivityContext"/> that was current when the
    /// entry was emitted, or <see langword="null"/> if no activity was active.
    /// </summary>
    public ActivityContext? ActivityContext { get; }

    /// <summary>
    /// Captures a new entry using the current time, thread, and <see cref="Activity.Current"/>.
    /// </summary>
    /// <param name="level">The level at which the entry is being emitted.</param>
    /// <param name="eventId">The event id associated with the entry.</param>
    /// <param name="message">The pre-rendered message text.</param>
    /// <param name="exception">The exception associated with the entry, if any.</param>
    /// <returns>The captured entry.</returns>
    public static SelfDiagnosticsLogEntry Capture(
        LogLevel level,
        EventId eventId,
        string message,
        Exception? exception)
        => new(
            DateTime.UtcNow,
            Environment.CurrentManagedThreadId,
            level,
            eventId,
            message,
            exception,
            Activity.Current?.Context);
}
