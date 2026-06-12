// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Internal;

/// <summary>
/// POC: routes SDK self-observability events (per
/// open-telemetry/semantic-conventions#3723) to a user-supplied
/// <see cref="ILogger"/>. Validates the .NET counterpart of the Rust POC,
/// which dispatches via the global <c>tracing</c> subscriber.
/// </summary>
/// <remarks>
/// <para>
/// This is intentionally a process-global, mutable sink rather than a
/// property on each component. It mirrors the Rust POC's reliance on
/// <c>tracing</c>'s global subscriber and keeps the per-component patch tiny.
/// A production design would replace this with explicit injection (e.g.
/// per-provider) once an emission target is settled on for the SDK.
/// </para>
/// <para>
/// The user MUST configure a separate <see cref="ILoggerFactory"/> backed by a
/// dedicated <see cref="Logs.LoggerProvider"/> for self-observability events:
/// routing them through the same <see cref="Logs.LoggerProvider"/> whose
/// components are emitting them would invite shutdown-time deadlock and lost
/// records.
/// </para>
/// </remarks>
internal static class SdkSelfObservability
{
    private static ILogger? logger;

    /// <summary>
    /// Sets the <see cref="ILogger"/> that SDK self-observability events are
    /// emitted to. Pass <see langword="null"/> to disable emission.
    /// </summary>
    /// <param name="value">The logger, or <see langword="null"/>.</param>
    public static void SetLogger(ILogger? value)
    {
        Volatile.Write(ref logger, value);
    }

    /// <summary>
    /// Emits an <c>otel.sdk.component.shutdown</c> event with the spec-
    /// mandated attributes. No-op when no logger is configured.
    /// </summary>
    /// <param name="componentType">Value for <c>otel.component.type</c>
    /// (e.g. <c>batching_log_processor</c>).</param>
    /// <param name="componentName">Value for <c>otel.component.name</c>
    /// (e.g. <c>batching_log_processor/0</c>).</param>
    /// <param name="result">Value for <c>otel.component.shutdown.result</c>:
    /// one of <c>success</c>, <c>failed</c>, <c>timed_out</c>.</param>
    /// <param name="durationSeconds">Value for
    /// <c>otel.component.shutdown.duration</c>.</param>
    public static void EmitComponentShutdown(
        string componentType,
        string componentName,
        string result,
        double durationSeconds)
    {
        var sink = Volatile.Read(ref logger);
        if (sink is null)
        {
            return;
        }

        // Per spec PR #3723 (post-tightening): MUST INFO on success,
        // MUST WARN otherwise.
        var level = result == "success" ? LogLevel.Information : LogLevel.Warning;

        // EventId.Name flows through OpenTelemetryLogger into
        // LogRecord.EventId.Name, which the OTLP serializer writes as
        // LogRecord.event_name on the wire.
        var eventId = new EventId(0, "otel.sdk.component.shutdown");

        // IReadOnlyList<KVP<string, object?>> is the fast path in
        // OpenTelemetryLogger.ProcessState; attributes flow through as
        // LogRecord attributes.
        var state = new List<KeyValuePair<string, object?>>(4)
        {
            new("otel.component.type", componentType),
            new("otel.component.name", componentName),
            new("otel.component.shutdown.result", result),
            new("otel.component.shutdown.duration", durationSeconds),
        };

        sink.Log(
            level,
            eventId,
            state,
            exception: null,
            formatter: static (_, _) => string.Empty);
    }

    /// <summary>
    /// Classifies a shutdown outcome into the spec's
    /// <c>otel.component.shutdown.result</c> enum.
    /// </summary>
    /// <param name="success">Whether the underlying shutdown reported success.</param>
    /// <param name="timeoutMilliseconds">The configured shutdown timeout
    /// (<see cref="Timeout.Infinite"/> when no timeout applies).</param>
    /// <param name="elapsedMilliseconds">Wall-clock duration of the
    /// shutdown attempt, in milliseconds.</param>
    /// <returns><c>success</c>, <c>failed</c>, or <c>timed_out</c>.</returns>
    /// <remarks>
    /// Per spec: <c>timed_out</c> only when the underlying API explicitly
    /// reports a timeout, or when a finite timeout was supplied and the
    /// shutdown ran out the clock. The latter is what we infer here.
    /// </remarks>
    public static string ClassifyResult(bool success, int timeoutMilliseconds, double elapsedMilliseconds)
    {
        if (success)
        {
            return "success";
        }

        if (timeoutMilliseconds != Timeout.Infinite
            && timeoutMilliseconds > 0
            && elapsedMilliseconds >= timeoutMilliseconds)
        {
            return "timed_out";
        }

        return "failed";
    }
}
