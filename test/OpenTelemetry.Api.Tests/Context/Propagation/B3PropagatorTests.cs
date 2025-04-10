// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Context.Propagation.Tests;

public class B3PropagatorTests
{
    private const string TraceIdBase16 = "ff000000000000000000000000000041";
    private const string TraceIdBase16EightBytes = "0000000000000041";
    private const string SpanIdBase16 = "ff00000000000041";
    private const string InvalidId = "abcdefghijklmnop";
    private const string InvalidSizeId = "0123456789abcdef00";
    private const ActivityTraceFlags TraceOptions = ActivityTraceFlags.Recorded;

    private static readonly ActivityTraceId TraceId = ActivityTraceId.CreateFromString(TraceIdBase16.AsSpan());
    private static readonly ActivityTraceId TraceIdEightBytes = ActivityTraceId.CreateFromString(("0000000000000000" + TraceIdBase16EightBytes).AsSpan());
    private static readonly ActivitySpanId SpanId = ActivitySpanId.CreateFromString(SpanIdBase16.AsSpan());

    private static readonly Action<IDictionary<string, string>, string, string> Setter = (d, k, v) => d[k] = v;
    private static readonly Func<IDictionary<string, string>, string, IEnumerable<string>> Getter =
        (d, k) =>
        {
            if (d.TryGetValue(k, out var v))
            {
                return [v];
            }

            return [];
        };

    private readonly B3Propagator b3propagator = new();
    private readonly B3Propagator b3PropagatorSingleHeader = new(true);

    private readonly ITestOutputHelper output;

    public B3PropagatorTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void Serialize_SampledContext()
    {
        var carrier = new Dictionary<string, string>();
        this.b3propagator.Inject(new PropagationContext(new ActivityContext(TraceId, SpanId, TraceOptions), default), carrier, Setter);
        this.ContainsExactly(carrier, new Dictionary<string, string> { { B3Propagator.XB3TraceId, TraceIdBase16 }, { B3Propagator.XB3SpanId, SpanIdBase16 }, { B3Propagator.XB3Sampled, "1" } });
    }

    [Fact]
    public void Serialize_NotSampledContext()
    {
        var carrier = new Dictionary<string, string>();
        var context = new ActivityContext(TraceId, SpanId, ActivityTraceFlags.None);
        this.output.WriteLine(context.ToString());
        this.b3propagator.Inject(new PropagationContext(context, default), carrier, Setter);
        this.ContainsExactly(carrier, new Dictionary<string, string> { { B3Propagator.XB3TraceId, TraceIdBase16 }, { B3Propagator.XB3SpanId, SpanIdBase16 } });
    }

    [Fact]
    public void ParseMissingSampledAndMissingFlag()
    {
        var headersNotSampled = new Dictionary<string, string>
        {
            { B3Propagator.XB3TraceId, TraceIdBase16 }, { B3Propagator.XB3SpanId, SpanIdBase16 },
        };
        var spanContext = new ActivityContext(TraceId, SpanId, ActivityTraceFlags.None, isRemote: true);
        Assert.Equal(new PropagationContext(spanContext, default), this.b3propagator.Extract(default, headersNotSampled, Getter));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    public void ParseSampled(string sampledValue)
    {
        var headersSampled = new Dictionary<string, string>
        {
            { B3Propagator.XB3TraceId, TraceIdBase16 }, { B3Propagator.XB3SpanId, SpanIdBase16 }, { B3Propagator.XB3Sampled, sampledValue },
        };
        var activityContext = new ActivityContext(TraceId, SpanId, TraceOptions, isRemote: true);
        Assert.Equal(new PropagationContext(activityContext, default), this.b3propagator.Extract(default, headersSampled, Getter));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("something_else")]
    public void ParseNotSampled(string sampledValue)
    {
        var headersNotSampled = new Dictionary<string, string>
        {
            { B3Propagator.XB3TraceId, TraceIdBase16 }, { B3Propagator.XB3SpanId, SpanIdBase16 }, { B3Propagator.XB3Sampled, sampledValue },
        };
        var activityContext = new ActivityContext(TraceId, SpanId, ActivityTraceFlags.None, isRemote: true);
        Assert.Equal(new PropagationContext(activityContext, default), this.b3propagator.Extract(default, headersNotSampled, Getter));
    }

    [Fact]
    public void ParseFlag()
    {
        var headersFlagSampled = new Dictionary<string, string>
        {
            { B3Propagator.XB3TraceId, TraceIdBase16 }, { B3Propagator.XB3SpanId, SpanIdBase16 }, { B3Propagator.XB3Flags, "1" },
        };
        var activityContext = new ActivityContext(TraceId, SpanId, TraceOptions, isRemote: true);
        Assert.Equal(new PropagationContext(activityContext, default), this.b3propagator.Extract(default, headersFlagSampled, Getter));
    }

    [Fact]
    public void ParseZeroFlag()
    {
        var headersFlagNotSampled = new Dictionary<string, string>
        {
            { B3Propagator.XB3TraceId, TraceIdBase16 }, { B3Propagator.XB3SpanId, SpanIdBase16 }, { B3Propagator.XB3Flags, "0" },
        };
        var activityContext = new ActivityContext(TraceId, SpanId, ActivityTraceFlags.None, isRemote: true);
        Assert.Equal(new PropagationContext(activityContext, default), this.b3propagator.Extract(default, headersFlagNotSampled, Getter));
    }

    [Fact]
    public void ParseEightBytesTraceId()
    {
        var headersEightBytes = new Dictionary<string, string>
        {
            { B3Propagator.XB3TraceId, TraceIdBase16EightBytes },
            { B3Propagator.XB3SpanId, SpanIdBase16 },
            { B3Propagator.XB3Sampled, "1" },
        };
        var activityContext = new ActivityContext(TraceIdEightBytes, SpanId, TraceOptions, isRemote: true);
        Assert.Equal(new PropagationContext(activityContext, default), this.b3propagator.Extract(default, headersEightBytes, Getter));
    }

    [Fact]
    public void ParseEightBytesTraceId_NotSampledSpanContext()
    {
        var headersEightBytes = new Dictionary<string, string>
        {
            { B3Propagator.XB3TraceId, TraceIdBase16EightBytes }, { B3Propagator.XB3SpanId, SpanIdBase16 },
        };
        var activityContext = new ActivityContext(TraceIdEightBytes, SpanId, ActivityTraceFlags.None, isRemote: true);
        Assert.Equal(new PropagationContext(activityContext, default), this.b3propagator.Extract(default, headersEightBytes, Getter));
    }

    [Fact]
    public void ParseInvalidTraceId()
    {
        var invalidHeaders = new Dictionary<string, string>
        {
            { B3Propagator.XB3TraceId, InvalidId }, { B3Propagator.XB3SpanId, SpanIdBase16 },
        };
        Assert.Equal(default, this.b3propagator.Extract(default, invalidHeaders, Getter));
    }

    [Fact]
    public void ParseInvalidTraceId_Size()
    {
        var invalidHeaders = new Dictionary<string, string>
        {
            { B3Propagator.XB3TraceId, InvalidSizeId }, { B3Propagator.XB3SpanId, SpanIdBase16 },
        };

        Assert.Equal(default, this.b3propagator.Extract(default, invalidHeaders, Getter));
    }

    [Fact]
    public void ParseMissingTraceId()
    {
        var invalidHeaders = new Dictionary<string, string> { { B3Propagator.XB3SpanId, SpanIdBase16 }, };
        Assert.Equal(default, this.b3propagator.Extract(default, invalidHeaders, Getter));
    }

    [Fact]
    public void ParseInvalidSpanId()
    {
        var invalidHeaders = new Dictionary<string, string>
        {
            { B3Propagator.XB3TraceId, TraceIdBase16 }, { B3Propagator.XB3SpanId, InvalidId },
        };
        Assert.Equal(default, this.b3propagator.Extract(default, invalidHeaders, Getter));
    }

    [Fact]
    public void ParseInvalidSpanId_Size()
    {
        var invalidHeaders = new Dictionary<string, string>
        {
            { B3Propagator.XB3TraceId, TraceIdBase16 }, { B3Propagator.XB3SpanId, InvalidSizeId },
        };
        Assert.Equal(default, this.b3propagator.Extract(default, invalidHeaders, Getter));
    }

    [Fact]
    public void ParseMissingSpanId()
    {
        var invalidHeaders = new Dictionary<string, string> { { B3Propagator.XB3TraceId, TraceIdBase16 } };
        Assert.Equal(default, this.b3propagator.Extract(default, invalidHeaders, Getter));
    }

    [Fact]
    public void Serialize_SampledContext_SingleHeader()
    {
        var carrier = new Dictionary<string, string>();
        var activityContext = new ActivityContext(TraceId, SpanId, TraceOptions);
        this.b3PropagatorSingleHeader.Inject(new PropagationContext(activityContext, default), carrier, Setter);
        this.ContainsExactly(carrier, new Dictionary<string, string> { { B3Propagator.XB3Combined, $"{TraceIdBase16}-{SpanIdBase16}-1" } });
    }

    [Fact]
    public void Serialize_NotSampledContext_SingleHeader()
    {
        var carrier = new Dictionary<string, string>();
        var activityContext = new ActivityContext(TraceId, SpanId, ActivityTraceFlags.None);
        this.output.WriteLine(activityContext.ToString());
        this.b3PropagatorSingleHeader.Inject(new PropagationContext(activityContext, default), carrier, Setter);
        this.ContainsExactly(carrier, new Dictionary<string, string> { { B3Propagator.XB3Combined, $"{TraceIdBase16}-{SpanIdBase16}" } });
    }

    [Fact]
    public void ParseMissingSampledAndMissingFlag_SingleHeader()
    {
        var headersNotSampled = new Dictionary<string, string>
        {
            { B3Propagator.XB3Combined, $"{TraceIdBase16}-{SpanIdBase16}" },
        };
        var activityContext = new ActivityContext(TraceId, SpanId, ActivityTraceFlags.None, isRemote: true);
        Assert.Equal(new PropagationContext(activityContext, default), this.b3PropagatorSingleHeader.Extract(default, headersNotSampled, Getter));
    }

    [Fact]
    public void ParseSampled_SingleHeader()
    {
        var headersSampled = new Dictionary<string, string>
        {
            { B3Propagator.XB3Combined, $"{TraceIdBase16}-{SpanIdBase16}-1" },
        };

        Assert.Equal(
            new PropagationContext(new ActivityContext(TraceId, SpanId, TraceOptions, isRemote: true), default),
            this.b3PropagatorSingleHeader.Extract(default, headersSampled, Getter));
    }

    [Fact]
    public void ParseZeroSampled_SingleHeader()
    {
        var headersNotSampled = new Dictionary<string, string>
        {
            { B3Propagator.XB3Combined, $"{TraceIdBase16}-{SpanIdBase16}-0" },
        };

        Assert.Equal(
            new PropagationContext(new ActivityContext(TraceId, SpanId, ActivityTraceFlags.None, isRemote: true), default),
            this.b3PropagatorSingleHeader.Extract(default, headersNotSampled, Getter));
    }

    [Fact]
    public void ParseFlag_SingleHeader()
    {
        var headersFlagSampled = new Dictionary<string, string>
        {
            { B3Propagator.XB3Combined, $"{TraceIdBase16}-{SpanIdBase16}-1" },
        };
        var activityContext = new ActivityContext(TraceId, SpanId, TraceOptions, isRemote: true);
        Assert.Equal(new PropagationContext(activityContext, default), this.b3PropagatorSingleHeader.Extract(default, headersFlagSampled, Getter));
    }

    [Fact]
    public void ParseZeroFlag_SingleHeader()
    {
        var headersFlagNotSampled = new Dictionary<string, string>
        {
            { B3Propagator.XB3Combined, $"{TraceIdBase16}-{SpanIdBase16}-0" },
        };
        var activityContext = new ActivityContext(TraceId, SpanId, ActivityTraceFlags.None, isRemote: true);
        Assert.Equal(new PropagationContext(activityContext, default), this.b3PropagatorSingleHeader.Extract(default, headersFlagNotSampled, Getter));
    }

    [Fact]
    public void ParseEightBytesTraceId_SingleHeader()
    {
        var headersEightBytes = new Dictionary<string, string>
        {
            { B3Propagator.XB3Combined, $"{TraceIdBase16EightBytes}-{SpanIdBase16}-1" },
        };
        var activityContext = new ActivityContext(TraceIdEightBytes, SpanId, TraceOptions, isRemote: true);
        Assert.Equal(new PropagationContext(activityContext, default), this.b3PropagatorSingleHeader.Extract(default, headersEightBytes, Getter));
    }

    [Fact]
    public void ParseEightBytesTraceId_NotSampledSpanContext_SingleHeader()
    {
        var headersEightBytes = new Dictionary<string, string>
        {
            { B3Propagator.XB3Combined, $"{TraceIdBase16EightBytes}-{SpanIdBase16}" },
        };
        var activityContext = new ActivityContext(TraceIdEightBytes, SpanId, ActivityTraceFlags.None, isRemote: true);
        Assert.Equal(new PropagationContext(activityContext, default), this.b3PropagatorSingleHeader.Extract(default, headersEightBytes, Getter));
    }

    [Fact]
    public void ParseInvalidTraceId_SingleHeader()
    {
        var invalidHeaders = new Dictionary<string, string>
        {
            { B3Propagator.XB3Combined, $"{InvalidId}-{SpanIdBase16}" },
        };
        Assert.Equal(default, this.b3PropagatorSingleHeader.Extract(default, invalidHeaders, Getter));
    }

    [Fact]
    public void ParseInvalidTraceId_Size_SingleHeader()
    {
        var invalidHeaders = new Dictionary<string, string>
        {
            { B3Propagator.XB3Combined, $"{InvalidSizeId}-{SpanIdBase16}" },
        };

        Assert.Equal(default, this.b3PropagatorSingleHeader.Extract(default, invalidHeaders, Getter));
    }

    [Fact]
    public void ParseMissingTraceId_SingleHeader()
    {
        var invalidHeaders = new Dictionary<string, string> { { B3Propagator.XB3Combined, $"-{SpanIdBase16}" } };
        Assert.Equal(default, this.b3PropagatorSingleHeader.Extract(default, invalidHeaders, Getter));
    }

    [Fact]
    public void ParseInvalidSpanId_SingleHeader()
    {
        var invalidHeaders = new Dictionary<string, string>
        {
            { B3Propagator.XB3Combined, $"{TraceIdBase16}-{InvalidId}" },
        };
        Assert.Equal(default, this.b3PropagatorSingleHeader.Extract(default, invalidHeaders, Getter));
    }

    [Fact]
    public void ParseInvalidSpanId_Size_SingleHeader()
    {
        var invalidHeaders = new Dictionary<string, string>
        {
            { B3Propagator.XB3Combined, $"{TraceIdBase16}-{InvalidSizeId}" },
        };
        Assert.Equal(default, this.b3PropagatorSingleHeader.Extract(default, invalidHeaders, Getter));
    }

    [Fact]
    public void ParseMissingSpanId_SingleHeader()
    {
        var invalidHeaders = new Dictionary<string, string> { { B3Propagator.XB3Combined, $"{TraceIdBase16}-" } };
        Assert.Equal(default, this.b3PropagatorSingleHeader.Extract(default, invalidHeaders, Getter));
    }

    [Fact]
    public void Fields_list()
    {
        ContainsExactly(
            this.b3propagator.Fields,
            [B3Propagator.XB3TraceId, B3Propagator.XB3SpanId, B3Propagator.XB3ParentSpanId, B3Propagator.XB3Sampled, B3Propagator.XB3Flags]);
    }

    private static void ContainsExactly(ISet<string> list, List<string> items)
    {
        Assert.Equal(items.Count, list.Count);
        foreach (var item in items)
        {
            Assert.Contains(item, list);
        }
    }

    private void ContainsExactly(Dictionary<string, string> dict, Dictionary<string, string> items)
    {
        foreach (var d in dict)
        {
            this.output.WriteLine(d.Key + "=" + d.Value);
        }

        Assert.Equal(items.Count, dict.Count);
        foreach (var item in items)
        {
            Assert.Contains(item, dict);
        }
    }
}
