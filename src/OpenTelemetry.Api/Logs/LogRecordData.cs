// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if EXPOSE_EXPERIMENTAL_FEATURES
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
#endif

namespace OpenTelemetry.Logs;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// Stores details about a log message.
/// </summary>
/// <remarks><inheritdoc cref="Logger" path="/remarks"/></remarks>
[Experimental(DiagnosticDefinitions.LogsBridgeExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
public
#else
/// <summary>
/// Stores details about a log message.
/// </summary>
internal
#endif
    struct LogRecordData : IEquatable<LogRecordData>
{
    internal DateTime TimestampBacking = DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogRecordData"/> struct.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>The <see cref="Timestamp"/> property is initialized to <see
    /// cref="DateTime.UtcNow"/> automatically.</item>
    /// <item>The <see cref="TraceId"/>, <see cref="SpanId"/>, and <see
    /// cref="TraceFlags"/> properties will be set using the <see
    /// cref="Activity.Current"/> instance.</item>
    /// </list>
    /// </remarks>
    public LogRecordData()
        : this(Activity.Current?.Context ?? default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LogRecordData"/> struct.
    /// </summary>
    /// <remarks>
    /// Note: The <see cref="Timestamp"/> property is initialized to <see
    /// cref="DateTime.UtcNow"/> automatically.
    /// </remarks>
    /// <param name="activity">Optional <see cref="Activity"/> used to populate
    /// trace context properties (<see cref="TraceId"/>, <see cref="SpanId"/>,
    /// and <see cref="TraceFlags"/>).</param>
    public LogRecordData(Activity? activity)
        : this(activity?.Context ?? default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LogRecordData"/> struct.
    /// </summary>
    /// <remarks>
    /// Note: The <see cref="Timestamp"/> property is initialized to <see
    /// cref="DateTime.UtcNow"/> automatically.
    /// </remarks>
    /// <param name="activityContext"><see cref="ActivityContext"/> used to
    /// populate trace context properties (<see cref="TraceId"/>, <see
    /// cref="SpanId"/>, and <see cref="TraceFlags"/>).</param>
    public LogRecordData(in ActivityContext activityContext)
    {
        this.TraceId = activityContext.TraceId;
        this.SpanId = activityContext.SpanId;
        this.TraceFlags = activityContext.TraceFlags;
    }

    /// <summary>
    /// Gets or sets the log timestamp.
    /// </summary>
    /// <remarks>
    /// Note: If <see cref="Timestamp"/> is set to a value with <see
    /// cref="DateTimeKind.Local"/> it will be automatically converted to
    /// UTC using <see cref="DateTime.ToUniversalTime"/>.
    /// </remarks>
    public DateTime Timestamp
    {
        readonly get => this.TimestampBacking;
        set => this.TimestampBacking = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value;
    }

    /// <summary>
    /// Gets or sets the log <see cref="ActivityTraceId"/>.
    /// </summary>
    public ActivityTraceId TraceId { get; set; }

    /// <summary>
    /// Gets or sets the log <see cref="ActivitySpanId"/>.
    /// </summary>
    public ActivitySpanId SpanId { get; set; }

    /// <summary>
    /// Gets or sets the log <see cref="ActivityTraceFlags"/>.
    /// </summary>
    public ActivityTraceFlags TraceFlags { get; set; }

    /// <summary>
    /// Gets or sets the original string representation of the severity as it is
    /// known at the source.
    /// </summary>
    public string? SeverityText { get; set; } = null;

    /// <summary>
    /// Gets or sets the log severity.
    /// </summary>
    public LogRecordSeverity? Severity { get; set; } = null;

    /// <summary>
    /// Gets or sets the log body.
    /// </summary>
    public string? Body { get; set; } = null;

    /// <summary>
    /// Gets or sets the name of the event associated with the log.
    /// </summary>
    public string? EventName { get; set; } = null;

    /// <summary>
    /// Determines whether the two instances of <see cref="LogRecordData"/> are equal.
    /// </summary>
    /// <param name="left">An instance of <see cref="LogRecordData"/>.</param>
    /// <param name="right"> Another instance of <see cref="LogRecordData"/>.</param>
    /// <returns>
    /// <see langword="true"/> if the values are considered equal; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool operator ==(LogRecordData left, LogRecordData right) => left.Equals(right);

    /// <summary>
    /// Determines whether the two instances of <see cref="LogRecordData"/> are not equal.
    /// </summary>
    /// <param name="left">An instance of <see cref="LogRecordData"/>.</param>
    /// <param name="right"> Another instance of <see cref="LogRecordData"/>.</param>
    /// <returns>
    /// <see langword="true"/> if the values are not considered equal; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool operator !=(LogRecordData left, LogRecordData right) => !(left == right);

    /// <inheritdoc/>
    public override readonly bool Equals(object? obj) =>
        obj is LogRecordData other && this.Equals(other);

    /// <inheritdoc/>
    public readonly bool Equals(LogRecordData other) =>
        this.TimestampBacking == other.TimestampBacking &&
        this.TraceId == other.TraceId &&
        this.SpanId == other.SpanId &&
        this.TraceFlags == other.TraceFlags &&
        this.Severity == other.Severity &&
        this.SeverityText == other.SeverityText &&
        this.Body == other.Body &&
        this.EventName == other.EventName;

    /// <inheritdoc/>
    public override readonly int GetHashCode()
#if NET
        => HashCode.Combine(
               this.TimestampBacking,
               this.TraceId,
               this.SpanId,
               this.TraceFlags,
               this.Severity,
               this.SeverityText,
               this.Body,
               this.EventName);
#else
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 31) + this.TimestampBacking.GetHashCode();
            hash = (hash * 31) + this.TraceId.GetHashCode();
            hash = (hash * 31) + this.SpanId.GetHashCode();
            hash = (hash * 31) + this.TraceFlags.GetHashCode();
            hash = (hash * 31) + (this.Severity?.GetHashCode() ?? 0);
            hash = (hash * 31) + (this.SeverityText?.GetHashCode() ?? 0);
            hash = (hash * 31) + (this.Body?.GetHashCode() ?? 0);
            hash = (hash * 31) + (this.EventName?.GetHashCode() ?? 0);
            return hash;
        }
    }
#endif

    internal static void SetActivityContext(ref LogRecordData data, Activity? activity)
    {
        if (activity != null)
        {
            data.TraceId = activity.TraceId;
            data.SpanId = activity.SpanId;
            data.TraceFlags = activity.ActivityTraceFlags;
        }
        else
        {
            data.TraceId = default;
            data.SpanId = default;
            data.TraceFlags = ActivityTraceFlags.None;
        }
    }
}
