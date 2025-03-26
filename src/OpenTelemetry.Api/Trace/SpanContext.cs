// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Context.Propagation;

namespace OpenTelemetry.Trace;

/// <summary>
/// A struct that represents a span context. A span context contains the portion of a span
/// that must propagate to child <see cref="TelemetrySpan"/> and across process boundaries.
/// It contains the identifiers <see cref="ActivityTraceId"/>and <see cref="ActivitySpanId"/>
/// associated with the <see cref="TelemetrySpan"/> along with a set of
/// common <see cref="TraceFlags"/> and system-specific <see cref="TraceState"/> values>.
/// </summary>
/// <remarks>SpanContext is a wrapper around <see cref="ActivityContext"/>.</remarks>
public readonly struct SpanContext : IEquatable<SpanContext>
{
    internal readonly ActivityContext ActivityContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpanContext"/> struct with the given identifiers and options.
    /// </summary>
    /// <param name="traceId">The <see cref="ActivityTraceId"/> to associate with the <see cref="SpanContext"/>.</param>
    /// <param name="spanId">The <see cref="ActivitySpanId"/> to associate with the <see cref="SpanContext"/>.</param>
    /// <param name="traceFlags">The <see cref="TraceFlags"/> to
    /// associate with the <see cref="SpanContext"/>.</param>
    /// <param name="isRemote">The value indicating whether this <see cref="SpanContext"/> was propagated from the remote parent.</param>
    /// <param name="traceState">The traceState to associate with the <see cref="SpanContext"/>.</param>
    public SpanContext(
        in ActivityTraceId traceId,
        in ActivitySpanId spanId,
        ActivityTraceFlags traceFlags,
        bool isRemote = false,
        IEnumerable<KeyValuePair<string, string>>? traceState = null)
    {
        this.ActivityContext = new ActivityContext(traceId, spanId, traceFlags, TraceStateUtils.GetString(traceState), isRemote);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpanContext"/> struct with the given identifiers and options.
    /// </summary>
    /// <param name="activityContext">The activity context.</param>
    public SpanContext(in ActivityContext activityContext)
    {
        this.ActivityContext = activityContext;
    }

    /// <summary>
    /// Gets the <see cref="ActivityTraceId"/> associated with this <see cref="SpanContext"/>.
    /// </summary>
    public ActivityTraceId TraceId
        => this.ActivityContext.TraceId;

    /// <summary>
    /// Gets the <see cref="ActivitySpanId"/> associated with this <see cref="SpanContext"/>.
    /// </summary>
    public ActivitySpanId SpanId
        => this.ActivityContext.SpanId;

    /// <summary>
    /// Gets the <see cref="ActivityTraceFlags"/> associated with this <see cref="SpanContext"/>.
    /// </summary>
    public ActivityTraceFlags TraceFlags
        => this.ActivityContext.TraceFlags;

    /// <summary>
    /// Gets a value indicating whether this <see cref="SpanContext" />
    /// was propagated from a remote parent.
    /// </summary>
    public bool IsRemote
        => this.ActivityContext.IsRemote;

    /// <summary>
    /// Gets a value indicating whether this <see cref="SpanContext"/> is valid.
    /// </summary>
    public bool IsValid => IsTraceIdValid(this.TraceId) && IsSpanIdValid(this.SpanId);

    /// <summary>
    /// Gets the <see cref="TraceState"/> associated with this <see cref="SpanContext"/>.
    /// </summary>
    public IEnumerable<KeyValuePair<string, string>> TraceState
    {
        get
        {
            var traceState = this.ActivityContext.TraceState;
            if (string.IsNullOrEmpty(traceState))
            {
                return [];
            }

            var traceStateResult = new List<KeyValuePair<string, string>>();
            TraceStateUtils.AppendTraceState(traceState!, traceStateResult);
            return traceStateResult;
        }
    }

    /// <summary>
    /// Converts a <see cref="SpanContext"/> into an <see cref="ActivityContext"/>.
    /// </summary>
    /// <param name="spanContext"><see cref="SpanContext"/> source.</param>
#pragma warning disable CA2225 // Operator overloads have named alternates
    public static implicit operator ActivityContext(SpanContext spanContext)
#pragma warning restore CA2225 // Operator overloads have named alternates
        => spanContext.ActivityContext;

    /// <summary>
    /// Compare two <see cref="SpanContext"/> for equality.
    /// </summary>
    /// <param name="spanContext1">First SpanContext to compare.</param>
    /// <param name="spanContext2">Second SpanContext to compare.</param>
    public static bool operator ==(SpanContext spanContext1, SpanContext spanContext2)
        => spanContext1.Equals(spanContext2);

    /// <summary>
    /// Compare two <see cref="SpanContext"/> for not equality.
    /// </summary>
    /// <param name="spanContext1">First SpanContext to compare.</param>
    /// <param name="spanContext2">Second SpanContext to compare.</param>
    public static bool operator !=(SpanContext spanContext1, SpanContext spanContext2)
        => !spanContext1.Equals(spanContext2);

    /// <inheritdoc/>
    public override int GetHashCode()
        => this.ActivityContext.GetHashCode();

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is SpanContext ctx && this.Equals(ctx);

    /// <inheritdoc/>
    public bool Equals(SpanContext other)
        => this.ActivityContext.Equals(other.ActivityContext);

    private static bool IsTraceIdValid(ActivityTraceId traceId)
        => traceId != default;

    private static bool IsSpanIdValid(ActivitySpanId spanId)
        => spanId != default;
}
