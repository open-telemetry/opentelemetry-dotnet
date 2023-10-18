// <copyright file="LogRecordData.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

#nullable enable

using System.Diagnostics;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace OpenTelemetry.Logs;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// Stores details about a log message.
/// </summary>
/// <remarks><inheritdoc cref="Logger" path="/remarks"/></remarks>
#if NET8_0_OR_GREATER
[Experimental("OT1001", UrlFormat = "https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/diagnostics/{0}.md")]
#endif
public
#else
/// <summary>
/// Stores details about a log message.
/// </summary>
internal
#endif
    struct LogRecordData
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
        set { this.TimestampBacking = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value; }
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
