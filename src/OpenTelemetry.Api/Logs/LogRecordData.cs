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

using System;
using System.Diagnostics;

namespace OpenTelemetry.Logs;

/// <summary>
/// Stores details about a log record.
/// </summary>
public struct LogRecordData
{
    internal DateTime TimestampBacking = DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogRecordData"/> struct.
    /// </summary>
    public LogRecordData()
        : this(activity: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LogRecordData"/> struct.
    /// </summary>
    /// <remarks>
    /// Note: The <see cref="Timestamp"/> property is initialized to <see
    /// cref="DateTime.UtcNow"/> automatically.
    /// </remarks>
    /// <param name="activity">Optional <see cref="Activity"/> used to populate context fields.</param>
    public LogRecordData(Activity? activity)
    {
        if (activity != null)
        {
            this.TraceId = activity.TraceId;
            this.SpanId = activity.SpanId;
            this.TraceFlags = activity.ActivityTraceFlags;
        }
        else
        {
            this.TraceId = default;
            this.SpanId = default;
            this.TraceFlags = ActivityTraceFlags.None;
        }
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

    internal static void SetActivityContext(ref LogRecordData data, Activity? activity = null)
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
