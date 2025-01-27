// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Context.Propagation.Tests;

public class TraceContextPropagatorTest
{
    private const string TraceParent = "traceparent";
    private const string TraceState = "tracestate";
    private const string TraceId = "0af7651916cd43dd8448eb211c80319c";
    private const string SpanId = "b9c7c989f97918e1";

    private static readonly string[] Empty = [];
    private static readonly Func<IDictionary<string, string>, string, IEnumerable<string>> Getter = (headers, name) =>
    {
        if (headers.TryGetValue(name, out var value))
        {
            return [value];
        }

        return Empty;
    };

    private static readonly Func<IDictionary<string, string[]>, string, IEnumerable<string>> ArrayGetter = (headers, name) =>
    {
        if (headers.TryGetValue(name, out var value))
        {
            return value;
        }

        return [];
    };

    private static readonly Action<IDictionary<string, string>, string, string> Setter = (carrier, name, value) =>
    {
        carrier[name] = value;
    };

    [Fact]
    public void CanParseExampleFromSpec()
    {
        var headers = new Dictionary<string, string>
        {
            { TraceParent, $"00-{TraceId}-{SpanId}-01" },
            { TraceState, $"congo=lZWRzIHRoNhcm5hbCBwbGVhc3VyZS4,rojo=00-{TraceId}-00f067aa0ba902b7-01" },
        };

        var f = new TraceContextPropagator();
        var ctx = f.Extract(default, headers, Getter);

        Assert.Equal(ActivityTraceId.CreateFromString(TraceId.AsSpan()), ctx.ActivityContext.TraceId);
        Assert.Equal(ActivitySpanId.CreateFromString(SpanId.AsSpan()), ctx.ActivityContext.SpanId);

        Assert.True(ctx.ActivityContext.IsRemote);
        Assert.True(ctx.ActivityContext.IsValid());
        Assert.True((ctx.ActivityContext.TraceFlags & ActivityTraceFlags.Recorded) != 0);

        Assert.Equal($"congo=lZWRzIHRoNhcm5hbCBwbGVhc3VyZS4,rojo=00-{TraceId}-00f067aa0ba902b7-01", ctx.ActivityContext.TraceState);
    }

    [Fact]
    public void NotSampled()
    {
        var headers = new Dictionary<string, string>
        {
            { TraceParent, $"00-{TraceId}-{SpanId}-00" },
        };

        var f = new TraceContextPropagator();
        var ctx = f.Extract(default, headers, Getter);

        Assert.Equal(ActivityTraceId.CreateFromString(TraceId.AsSpan()), ctx.ActivityContext.TraceId);
        Assert.Equal(ActivitySpanId.CreateFromString(SpanId.AsSpan()), ctx.ActivityContext.SpanId);
        Assert.True((ctx.ActivityContext.TraceFlags & ActivityTraceFlags.Recorded) == 0);

        Assert.True(ctx.ActivityContext.IsRemote);
        Assert.True(ctx.ActivityContext.IsValid());
    }

    [Fact]
    public void IsBlankIfNoHeader()
    {
        var headers = new Dictionary<string, string>();

        var f = new TraceContextPropagator();
        var ctx = f.Extract(default, headers, Getter);

        Assert.False(ctx.ActivityContext.IsValid());
    }

    [Theory]
    [InlineData($"00-xyz7651916cd43dd8448eb211c80319c-{SpanId}-01")]
    [InlineData($"00-{TraceId}-xyz7c989f97918e1-01")]
    [InlineData($"00-{TraceId}-{SpanId}-x1")]
    [InlineData($"00-{TraceId}-{SpanId}-1x")]
    public void IsBlankIfInvalid(string invalidTraceParent)
    {
        var headers = new Dictionary<string, string>
        {
            { TraceParent, invalidTraceParent },
        };

        var f = new TraceContextPropagator();
        var ctx = f.Extract(default, headers, Getter);

        Assert.False(ctx.ActivityContext.IsValid());
    }

    [Fact]
    public void TracestateToStringEmpty()
    {
        var headers = new Dictionary<string, string>
        {
            { TraceParent, $"00-{TraceId}-{SpanId}-01" },
        };

        var f = new TraceContextPropagator();
        var ctx = f.Extract(default, headers, Getter);

        Assert.Null(ctx.ActivityContext.TraceState);
    }

    [Fact]
    public void TracestateToString()
    {
        var headers = new Dictionary<string, string>
        {
            { TraceParent, $"00-{TraceId}-{SpanId}-01" },
            { TraceState, "k1=v1,k2=v2,k3=v3" },
        };

        var f = new TraceContextPropagator();
        var ctx = f.Extract(default, headers, Getter);

        Assert.Equal("k1=v1,k2=v2,k3=v3", ctx.ActivityContext.TraceState);
    }

    [Fact]
    public void Inject_NoTracestate()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var expectedHeaders = new Dictionary<string, string>
        {
            { TraceParent, $"00-{traceId}-{spanId}-01" },
        };

        var activityContext = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, traceState: null);
        PropagationContext propagationContext = new PropagationContext(activityContext, default);
        var carrier = new Dictionary<string, string>();
        var f = new TraceContextPropagator();
        f.Inject(propagationContext, carrier, Setter);

        Assert.Equal(expectedHeaders, carrier);
    }

    [Fact]
    public void Inject_WithTracestate()
    {
        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var expectedHeaders = new Dictionary<string, string>
        {
            { TraceParent, $"00-{traceId}-{spanId}-01" },
            { TraceState, $"congo=lZWRzIHRoNhcm5hbCBwbGVhc3VyZS4,rojo=00-{traceId}-00f067aa0ba902b7-01" },
        };

        var activityContext = new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, expectedHeaders[TraceState]);
        PropagationContext propagationContext = new PropagationContext(activityContext, default);
        var carrier = new Dictionary<string, string>();
        var f = new TraceContextPropagator();
        f.Inject(propagationContext, carrier, Setter);

        Assert.Equal(expectedHeaders, carrier);
    }

    [Fact]
    public void DuplicateKeys()
    {
        // test_tracestate_duplicated_keys
        Assert.Empty(CallTraceContextPropagator("foo=1,foo=1"));
        Assert.Empty(CallTraceContextPropagator("foo=1,foo=2"));
        Assert.Empty(CallTraceContextPropagator(["foo=1", "foo=1"]));
        Assert.Empty(CallTraceContextPropagator(["foo=1", "foo=2"]));
        Assert.Empty(CallTraceContextPropagator("foo=1,bar=2,baz=3,foo=4"));
    }

    [Fact]
    public void NoDuplicateKeys()
    {
        Assert.Equal("foo=1,bar=foo,baz=2", CallTraceContextPropagator("foo=1,bar=foo,baz=2"));
        Assert.Equal("foo=1,bar=2,baz=foo", CallTraceContextPropagator("foo=1,bar=2,baz=foo"));
        Assert.Equal("foo=1,foo@tenant=2", CallTraceContextPropagator("foo=1,foo@tenant=2"));
        Assert.Equal("foo=1,tenant@foo=2", CallTraceContextPropagator("foo=1,tenant@foo=2"));
    }

    [Fact]
    public void Key_IllegalCharacters()
    {
        // test_tracestate_key_illegal_characters
        Assert.Empty(CallTraceContextPropagator("foo =1"));
        Assert.Empty(CallTraceContextPropagator("FOO =1"));
        Assert.Empty(CallTraceContextPropagator("foo.bar=1"));
    }

    [Fact]
    public void Key_IllegalVendorFormat()
    {
        // test_tracestate_key_illegal_vendor_format
        Assert.Empty(CallTraceContextPropagator("foo@=1,bar=2"));
        Assert.Empty(CallTraceContextPropagator("@foo=1,bar=2"));
        Assert.Empty(CallTraceContextPropagator("foo@@bar=1,bar=2"));
        Assert.Empty(CallTraceContextPropagator("foo@bar@baz=1,bar=2"));
    }

    [Fact]
    public void MemberCountLimit()
    {
        // test_tracestate_member_count_limit
        var output1 = CallTraceContextPropagator(
        [
            "bar01=01,bar02=02,bar03=03,bar04=04,bar05=05,bar06=06,bar07=07,bar08=08,bar09=09,bar10=10",
            "bar11=11,bar12=12,bar13=13,bar14=14,bar15=15,bar16=16,bar17=17,bar18=18,bar19=19,bar20=20",
            "bar21=21,bar22=22,bar23=23,bar24=24,bar25=25,bar26=26,bar27=27,bar28=28,bar29=29,bar30=30",
            "bar31=31,bar32=32"
        ]);
        var expected =
            "bar01=01,bar02=02,bar03=03,bar04=04,bar05=05,bar06=06,bar07=07,bar08=08,bar09=09,bar10=10" + "," +
            "bar11=11,bar12=12,bar13=13,bar14=14,bar15=15,bar16=16,bar17=17,bar18=18,bar19=19,bar20=20" + "," +
            "bar21=21,bar22=22,bar23=23,bar24=24,bar25=25,bar26=26,bar27=27,bar28=28,bar29=29,bar30=30" + "," +
            "bar31=31,bar32=32";
        Assert.Equal(expected, output1);

        var output2 = CallTraceContextPropagator(
        [
            "bar01=01,bar02=02,bar03=03,bar04=04,bar05=05,bar06=06,bar07=07,bar08=08,bar09=09,bar10=10",
            "bar11=11,bar12=12,bar13=13,bar14=14,bar15=15,bar16=16,bar17=17,bar18=18,bar19=19,bar20=20",
            "bar21=21,bar22=22,bar23=23,bar24=24,bar25=25,bar26=26,bar27=27,bar28=28,bar29=29,bar30=30",
            "bar31=31,bar32=32,bar33=33"
        ]);
        Assert.Empty(output2);
    }

    [Fact]
    public void Key_KeyLengthLimit()
    {
        // test_tracestate_key_length_limit
        string input1 = new string('z', 256) + "=1";
        Assert.Equal(input1, CallTraceContextPropagator(input1));
        Assert.Empty(CallTraceContextPropagator(new string('z', 257) + "=1"));
        string input2 = new string('t', 241) + "@" + new string('v', 14) + "=1";
        Assert.Equal(input2, CallTraceContextPropagator(input2));
        Assert.Empty(CallTraceContextPropagator(new string('t', 242) + "@v=1"));
        Assert.Empty(CallTraceContextPropagator("t@" + new string('v', 15) + "=1"));
    }

    [Fact]
    public void Value_IllegalCharacters()
    {
        // test_tracestate_value_illegal_characters
        Assert.Empty(CallTraceContextPropagator("foo=bar=baz"));
        Assert.Empty(CallTraceContextPropagator("foo=,bar=3"));
    }

    [Fact]
    public void Traceparent_Version()
    {
        // test_traceparent_version_0x00
        Assert.NotEqual(
            "12345678901234567890123456789012",
            CallTraceContextPropagatorWithTraceParent("00-12345678901234567890123456789012-1234567890123456-01."));
        Assert.NotEqual(
            "12345678901234567890123456789012",
            CallTraceContextPropagatorWithTraceParent("00-12345678901234567890123456789012-1234567890123456-01-what-the-future-will-be-like"));

        // test_traceparent_version_0xcc
        Assert.Equal(
            "12345678901234567890123456789012",
            CallTraceContextPropagatorWithTraceParent("cc-12345678901234567890123456789012-1234567890123456-01"));
        Assert.Equal(
            "12345678901234567890123456789012",
            CallTraceContextPropagatorWithTraceParent("cc-12345678901234567890123456789012-1234567890123456-01-what-the-future-will-be-like"));
        Assert.NotEqual(
            "12345678901234567890123456789012",
            CallTraceContextPropagatorWithTraceParent("cc-12345678901234567890123456789012-1234567890123456-01.what-the-future-will-be-like"));

        // test_traceparent_version_0xff
        Assert.NotEqual(
            "12345678901234567890123456789012",
            CallTraceContextPropagatorWithTraceParent("ff-12345678901234567890123456789012-1234567890123456-01"));
    }

    private static string CallTraceContextPropagatorWithTraceParent(string traceparent)
    {
        var headers = new Dictionary<string, string>
        {
            { TraceParent, traceparent },
        };
        var f = new TraceContextPropagator();
        var ctx = f.Extract(default, headers, Getter);
        return ctx.ActivityContext.TraceId.ToString();
    }

    private static string CallTraceContextPropagator(string tracestate)
    {
        var headers = new Dictionary<string, string>
        {
            { TraceParent, $"00-{TraceId}-{SpanId}-01" },
            { TraceState, tracestate },
        };
        var f = new TraceContextPropagator();
        var ctx = f.Extract(default, headers, Getter);

        var traceState = ctx.ActivityContext.TraceState;
        Assert.NotNull(traceState);
        return traceState;
    }

    private static string CallTraceContextPropagator(string[] tracestate)
    {
        var headers = new Dictionary<string, string[]>
        {
            { TraceParent, [$"00-{TraceId}-{SpanId}-01"] },
            { TraceState, tracestate },
        };
        var f = new TraceContextPropagator();
        var ctx = f.Extract(default, headers, ArrayGetter);

        var traceState = ctx.ActivityContext.TraceState;
        Assert.NotNull(traceState);
        return traceState;
    }
}
