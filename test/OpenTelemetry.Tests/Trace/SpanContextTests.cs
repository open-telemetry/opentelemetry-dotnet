// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class SpanContextTests
{
    private static readonly byte[] FirstTraceIdBytes = "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0a"u8.ToArray();
    private static readonly byte[] SecondTraceIdBytes = "\0\0\0\0\0\0\00\0\0\0\0\0\0\0\0"u8.ToArray();
    private static readonly byte[] FirstSpanIdBytes = "\0\0\0\0\0\0\0a"u8.ToArray();
    private static readonly byte[] SecondSpanIdBytes = "0\0\0\0\0\0\0\0"u8.ToArray();

    private static readonly SpanContext First =
      new(
          ActivityTraceId.CreateFromBytes(FirstTraceIdBytes),
          ActivitySpanId.CreateFromBytes(FirstSpanIdBytes),
          ActivityTraceFlags.None);

    private static readonly SpanContext Second =
      new(
          ActivityTraceId.CreateFromBytes(SecondTraceIdBytes),
          ActivitySpanId.CreateFromBytes(SecondSpanIdBytes),
          ActivityTraceFlags.Recorded);

    [Fact]
    public void InvalidSpanContext()
    {
        Assert.Equal(default, default(SpanContext).TraceId);
        Assert.Equal(default, default(SpanContext).SpanId);
        Assert.Equal(ActivityTraceFlags.None, default(SpanContext).TraceFlags);
    }

    [Fact]
    public void IsValid()
    {
        Assert.False(default(SpanContext).IsValid);
        Assert.False(
                new SpanContext(
                        ActivityTraceId.CreateFromBytes(FirstTraceIdBytes), default, ActivityTraceFlags.None)
                    .IsValid);
        Assert.False(
                new SpanContext(
                        default, ActivitySpanId.CreateFromBytes(FirstSpanIdBytes), ActivityTraceFlags.None)
                    .IsValid);
        Assert.True(First.IsValid);
        Assert.True(Second.IsValid);
    }

    [Fact]
    public void GetTraceId()
    {
        Assert.Equal(ActivityTraceId.CreateFromBytes(FirstTraceIdBytes), First.TraceId);
        Assert.Equal(ActivityTraceId.CreateFromBytes(SecondTraceIdBytes), Second.TraceId);
    }

    [Fact]
    public void GetSpanId()
    {
        Assert.Equal(ActivitySpanId.CreateFromBytes(FirstSpanIdBytes), First.SpanId);
        Assert.Equal(ActivitySpanId.CreateFromBytes(SecondSpanIdBytes), Second.SpanId);
    }

    [Fact]
    public void GetTraceOptions()
    {
        Assert.Equal(ActivityTraceFlags.None, First.TraceFlags);
        Assert.Equal(ActivityTraceFlags.Recorded, Second.TraceFlags);
    }

    [Fact]
    public void Equality1()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var context1 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded);
        var context2 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded);

        Assert.Equal(context1, context2);
        Assert.True(context1 == context2);
    }

    [Fact]
    public void Equality2()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var context1 = new SpanContext(traceId, spanId, ActivityTraceFlags.None, true);
        var context2 = new SpanContext(traceId, spanId, ActivityTraceFlags.None, true);

        Assert.Equal(context1, context2);
        Assert.True(context1 == context2);
    }

    [Fact]
    public void Equality3()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        IEnumerable<KeyValuePair<string, string>> tracestate = new List<KeyValuePair<string, string>>();
        var context1 = new SpanContext(traceId, spanId, ActivityTraceFlags.None, false, tracestate);
        var context2 = new SpanContext(traceId, spanId, ActivityTraceFlags.None, false, tracestate);

        Assert.Equal(context1, context2);
        Assert.True(context1 == context2);
    }

    [Fact]
    public void Equality4()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        IEnumerable<KeyValuePair<string, string>> tracestate = new List<KeyValuePair<string, string>>();
        var context1 = new SpanContext(traceId, spanId, ActivityTraceFlags.None, false, tracestate);
        object context2 = new SpanContext(traceId, spanId, ActivityTraceFlags.None, false, tracestate);

        Assert.Equal(context1, context2);
        Assert.True(context1.Equals(context2));
    }

    [Fact]
    public void Not_Equality_DifferentTraceId()
    {
        var spanId = ActivitySpanId.CreateRandom();
        var context1 = new SpanContext(ActivityTraceId.CreateRandom(), spanId, ActivityTraceFlags.Recorded);
        var context2 = new SpanContext(ActivityTraceId.CreateRandom(), spanId, ActivityTraceFlags.Recorded);

        Assert.NotEqual(context1, context2);
        Assert.True(context1 != context2);
    }

    [Fact]
    public void Not_Equality_DifferentSpanId()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var context1 = new SpanContext(traceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, true);
        var context2 = new SpanContext(traceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, true);

        Assert.NotEqual(context1, context2);
        Assert.True(context1 != context2);
    }

    [Fact]
    public void Not_Equality_DifferentTraceFlags()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        IEnumerable<KeyValuePair<string, string>> tracestate = new List<KeyValuePair<string, string>>();
        var context1 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded, false, tracestate);
        var context2 = new SpanContext(traceId, spanId, ActivityTraceFlags.None, false, tracestate);

        Assert.NotEqual(context1, context2);
        Assert.True(context1 != context2);
    }

    [Fact]
    public void Not_Equality_DifferentIsRemote()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        IEnumerable<KeyValuePair<string, string>> tracestate = new List<KeyValuePair<string, string>>();
        var context1 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded, true, tracestate);
        var context2 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded, false, tracestate);

        Assert.NotEqual(context1, context2);
        Assert.True(context1 != context2);
    }

    [Fact]
    public void Not_Equality_DifferentTraceState()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        IEnumerable<KeyValuePair<string, string>> tracestate1 = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("k", "v1") };
        IEnumerable<KeyValuePair<string, string>> tracestate2 = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("k", "v2") };
        var context1 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded, true, tracestate1);
        var context2 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded, true, tracestate2);

        Assert.NotEqual(context1, context2);
        Assert.True(context1 != context2);
    }

    [Fact]
    public void TestGetHashCode()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        IEnumerable<KeyValuePair<string, string>> tracestate = new List<KeyValuePair<string, string>>();
        var context1 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded, true, tracestate);

        Assert.NotEqual(0, context1.GetHashCode());
    }
}
