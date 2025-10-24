// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Logs.Tests;

public sealed class LogRecordDataTests
{
    [Fact]
    public void ParameterlessConstructorWithActiveActivityTest()
    {
        using var activity = new Activity("Test");
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.Start();

        var record = new LogRecordData();

        Assert.Equal(activity.TraceId, record.TraceId);
        Assert.Equal(activity.SpanId, record.SpanId);
        Assert.Equal(activity.ActivityTraceFlags, record.TraceFlags);

        record = default;

        Assert.Equal(default, record.TraceId);
        Assert.Equal(default, record.SpanId);
        Assert.Equal(default, record.TraceFlags);
    }

    [Fact]
    public void ParameterlessConstructorWithoutActiveActivityTest()
    {
        var record = new LogRecordData();

        Assert.Equal(default, record.TraceId);
        Assert.Equal(default, record.SpanId);
        Assert.Equal(default, record.TraceFlags);
    }

    [Fact]
    public void ActivityConstructorTest()
    {
        using var activity = new Activity("Test");
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.Start();

        Activity.Current = null;

        var record = new LogRecordData(activity);

        Assert.Equal(activity.TraceId, record.TraceId);
        Assert.Equal(activity.SpanId, record.SpanId);
        Assert.Equal(activity.ActivityTraceFlags, record.TraceFlags);

        record = new LogRecordData(activity: null);

        Assert.Equal(default, record.TraceId);
        Assert.Equal(default, record.SpanId);
        Assert.Equal(default, record.TraceFlags);
    }

    [Fact]
    public void ActivityContextConstructorTest()
    {
        var context = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded,
            traceState: null,
            isRemote: true);

        var record = new LogRecordData(in context);

        Assert.Equal(context.TraceId, record.TraceId);
        Assert.Equal(context.SpanId, record.SpanId);
        Assert.Equal(context.TraceFlags, record.TraceFlags);

        record = new LogRecordData(activityContext: default);

        Assert.Equal(default, record.TraceId);
        Assert.Equal(default, record.SpanId);
        Assert.Equal(default, record.TraceFlags);
    }

    [Fact]
    public void TimestampTest()
    {
        var nowUtc = DateTime.UtcNow;

        var record = new LogRecordData();
        Assert.True(record.Timestamp >= nowUtc);

        record = default;
        Assert.Equal(DateTime.MinValue, record.Timestamp);

        var now = DateTime.Now;

        record.Timestamp = now;

        Assert.Equal(DateTimeKind.Utc, record.Timestamp.Kind);
        Assert.Equal(now.ToUniversalTime(), record.Timestamp);
    }

    [Fact]
    public void SetActivityContextTest()
    {
        LogRecordData record = default;

        using var activity = new Activity("Test");
        activity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
        activity.Start();

        LogRecordData.SetActivityContext(ref record, activity);

        Assert.Equal(activity.TraceId, record.TraceId);
        Assert.Equal(activity.SpanId, record.SpanId);
        Assert.Equal(activity.ActivityTraceFlags, record.TraceFlags);

        LogRecordData.SetActivityContext(ref record, activity: null);

        Assert.Equal(default, record.TraceId);
        Assert.Equal(default, record.SpanId);
        Assert.Equal(default, record.TraceFlags);
    }

    [Fact]
    public void Equals_Object_ReturnsTrueForSameValues()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var left = CreateSample(timestamp, "Info", LogRecordSeverity.Info, "Body", "Event", traceId, spanId, ActivityTraceFlags.Recorded);
        var right = CreateSample(timestamp, "Info", LogRecordSeverity.Info, "Body", "Event", traceId, spanId, ActivityTraceFlags.Recorded);

        Assert.True(left.Equals((object)right));
        Assert.True(((object)left).Equals(right));
    }

    [Fact]
    public void Equals_Object_ReturnsFalseForDifferentValues()
    {
        var left = CreateSample(severityText: "Info");
        var right = CreateSample(severityText: "Warn");

        Assert.False(left.Equals((object)right));
        Assert.False(((object)left).Equals(right));
    }

    [Fact]
    public void Equals_Typed_ReturnsTrueForSameValues()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var left = CreateSample(timestamp, "Info", LogRecordSeverity.Info, "Body", "Event", traceId, spanId, ActivityTraceFlags.Recorded);
        var right = CreateSample(timestamp, "Info", LogRecordSeverity.Info, "Body", "Event", traceId, spanId, ActivityTraceFlags.Recorded);

        Assert.True(left.Equals(right));
    }

    [Fact]
    public void Equals_Typed_ReturnsFalseForDifferent_Timestamp()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();

        var left = CreateSample(timestamp: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), traceId: traceId, spanId: spanId);
        var right = CreateSample(timestamp: new DateTime(2024, 1, 1, 0, 0, 1, DateTimeKind.Utc), traceId: traceId, spanId: spanId);

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void Equals_Typed_ReturnsFalseForDifferent_TraceId()
    {
        var spanId = ActivitySpanId.CreateRandom();

        var left = CreateSample(traceId: ActivityTraceId.CreateRandom(), spanId: spanId);
        var right = CreateSample(traceId: ActivityTraceId.CreateRandom(), spanId: spanId);

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void Equals_Typed_ReturnsFalseForDifferent_SpanId()
    {
        var traceId = ActivityTraceId.CreateRandom();

        var left = CreateSample(spanId: ActivitySpanId.CreateRandom(), traceId: traceId);
        var right = CreateSample(spanId: ActivitySpanId.CreateRandom(), traceId: traceId);

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void Equals_Typed_ReturnsFalseForDifferent_Body()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();

        var left = CreateSample(body: "A", traceId: traceId, spanId: spanId);
        var right = CreateSample(body: "B", traceId: traceId, spanId: spanId);

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void Equals_Typed_ReturnsFalseForDifferent_TraceFlags()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();

        var left = CreateSample(traceFlags: ActivityTraceFlags.Recorded, traceId: traceId, spanId: spanId);
        var right = CreateSample(traceFlags: ActivityTraceFlags.None, traceId: traceId, spanId: spanId);

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void Equals_Typed_ReturnsFalseForDifferent_Severity()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();

        var left = CreateSample(severity: LogRecordSeverity.Debug, traceId: traceId, spanId: spanId);
        var right = CreateSample(severity: LogRecordSeverity.Debug2, traceId: traceId, spanId: spanId);

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void Equals_Typed_ReturnsFalseForDifferent_SeverityText()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();

        var left = CreateSample(severityText: "foo", traceId: traceId, spanId: spanId);
        var right = CreateSample(severityText: "bar", traceId: traceId, spanId: spanId);

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void Equals_Typed_ReturnsFalseForDifferent_EventName()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();

        var left = CreateSample(eventName: "foo", traceId: traceId, spanId: spanId);
        var right = CreateSample(eventName: "bar", traceId: traceId, spanId: spanId);

        Assert.False(left.Equals(right));
    }

    [Fact]
    public void Operator_Equality_ReturnsTrueForEqualStructs()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var left = CreateSample(timestamp, "Info", LogRecordSeverity.Info, "Body", "Event", traceId, spanId, ActivityTraceFlags.Recorded);
        var right = CreateSample(timestamp, "Info", LogRecordSeverity.Info, "Body", "Event", traceId, spanId, ActivityTraceFlags.Recorded);

        Assert.True(left == right);
        Assert.False(left != right);
    }

    [Fact]
    public void Operator_Equality_ReturnsFalseForDifferentStructs()
    {
        var left = CreateSample(eventName: "A");
        var right = CreateSample(eventName: "B");

        Assert.False(left == right);
        Assert.True(left != right);
    }

    [Fact]
    public void GetHashCode_SameForEqualStructs()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var left = CreateSample(timestamp, "Info", LogRecordSeverity.Info, "Body", "Event", traceId, spanId, ActivityTraceFlags.Recorded);
        var right = CreateSample(timestamp, "Info", LogRecordSeverity.Info, "Body", "Event", traceId, spanId, ActivityTraceFlags.Recorded);

        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentForDifferentStructs()
    {
        var left = CreateSample(severity: LogRecordSeverity.Info);
        var right = CreateSample(severity: LogRecordSeverity.Error);

        Assert.NotEqual(left.GetHashCode(), right.GetHashCode());
    }

    private static LogRecordData CreateSample(
        DateTime? timestamp = null,
        string? severityText = "Info",
        LogRecordSeverity? severity = LogRecordSeverity.Info,
        string? body = "Test body",
        string? eventName = "TestEvent",
        ActivityTraceId? traceId = null,
        ActivitySpanId? spanId = null,
        ActivityTraceFlags? traceFlags = null) =>
            new()
            {
                Timestamp = timestamp ?? new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                SeverityText = severityText,
                Severity = severity,
                Body = body,
                EventName = eventName,
                TraceId = traceId ?? ActivityTraceId.CreateRandom(),
                SpanId = spanId ?? ActivitySpanId.CreateRandom(),
                TraceFlags = traceFlags ?? ActivityTraceFlags.Recorded,
            };
}
