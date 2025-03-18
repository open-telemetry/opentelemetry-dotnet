// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Context.Propagation;
using Xunit;

namespace OpenTelemetry.Extensions.Propagators.Tests;

public class JaegerPropagatorTests
{
    private const string JaegerHeader = "uber-trace-id";
    private const string JaegerDelimiter = ":";
    private const string JaegerDelimiterEncoded = "%3A";

    private const string TraceId = "0007651916cd43dd8448eb211c803177";
    private const string TraceIdShort = "7651916cd43dd8448eb211c803177";
    private const string SpanId = "0007c989f9791877";
    private const string SpanIdShort = "7c989f9791877";
    private const string ParentSpanId = "0";
    private const string FlagSampled = "1";
    private const string FlagNotSampled = "0";

    private static readonly Func<IDictionary<string, string[]>, string, IEnumerable<string>> Getter = (headers, name) =>
    {
        return headers.TryGetValue(name, out var value) ? value : [];
    };

    private static readonly Action<IDictionary<string, string>, string, string> Setter = (carrier, name, value) =>
    {
        carrier[name] = value;
    };

    [Fact]
    public void ExtractReturnsOriginalContextIfContextIsAlreadyValid()
    {
        // arrange
        var traceId = ActivityTraceId.CreateFromString(TraceId.AsSpan());
        var spanId = ActivitySpanId.CreateFromString(SpanId.AsSpan());
        var propagationContext = new PropagationContext(
            new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, isRemote: true),
            default);

        var headers = new Dictionary<string, string[]>();

        // act
        var result = new JaegerPropagator().Extract(propagationContext, headers, Getter);

        // assert
        Assert.Equal(propagationContext, result);
    }

    [Fact]
    public void ExtractReturnsOriginalContextIfCarrierIsNull()
    {
        // arrange
        var propagationContext = default(PropagationContext);

        // act
        var result = new JaegerPropagator().Extract(propagationContext, null, Getter!);

        // assert
        Assert.Equal(propagationContext, result);
    }

    [Fact]
    public void ExtractReturnsOriginalContextIfGetterIsNull()
    {
        // arrange
        var propagationContext = default(PropagationContext);

        var headers = new Dictionary<string, string[]>();

        // act
        var result = new JaegerPropagator().Extract(propagationContext, headers, null!);

        // assert
        Assert.Equal(propagationContext, result);
    }

    [Theory]
    [InlineData("", SpanId, ParentSpanId, FlagSampled, JaegerDelimiter)]
    [InlineData(TraceId, "", ParentSpanId, FlagSampled, JaegerDelimiter)]
    [InlineData(TraceId, SpanId, "", FlagSampled, JaegerDelimiter)]
    [InlineData(TraceId, SpanId, ParentSpanId, "", JaegerDelimiter)]
    [InlineData(TraceId, SpanId, ParentSpanId, FlagSampled, "")]
    [InlineData("invalid trace id", SpanId, ParentSpanId, FlagSampled, JaegerDelimiter)]
    [InlineData(TraceId, "invalid span id", ParentSpanId, FlagSampled, JaegerDelimiter)]
    [InlineData(TraceId, SpanId, $"too many {JaegerDelimiter} records", FlagSampled, JaegerDelimiter)]
    public void ExtractReturnsOriginalContextIfHeaderIsNotValid(string traceId, string spanId, string parentSpanId, string flags, string delimiter)
    {
        // arrange
        var propagationContext = default(PropagationContext);

        var formattedHeader = string.Join(
            delimiter,
            traceId,
            spanId,
            parentSpanId,
            flags);

        var headers = new Dictionary<string, string[]> { { JaegerHeader, [formattedHeader] } };

        // act
        var result = new JaegerPropagator().Extract(propagationContext, headers, Getter);

        // assert
        Assert.Equal(propagationContext, result);
    }

    [Theory]
    [InlineData(TraceId, SpanId, ParentSpanId, FlagSampled, JaegerDelimiter)]
    [InlineData(TraceIdShort, SpanIdShort, ParentSpanId, FlagNotSampled, JaegerDelimiterEncoded)]
    public void ExtractReturnsNewContextIfHeaderIsValid(string traceId, string spanId, string parentSpanId, string flags, string delimiter)
    {
#if NET
        Assert.NotNull(traceId);
        Assert.NotNull(spanId);
#else
        if (traceId == null)
        {
            throw new ArgumentNullException(nameof(traceId));
        }

        if (spanId == null)
        {
            throw new ArgumentNullException(nameof(traceId));
        }
#endif

        // arrange
        var propagationContext = default(PropagationContext);

        var formattedHeader = string.Join(
            delimiter,
            traceId,
            spanId,
            parentSpanId,
            flags);

        var headers = new Dictionary<string, string[]> { { JaegerHeader, [formattedHeader] } };

        // act
        var result = new JaegerPropagator().Extract(propagationContext, headers, Getter);

        // assert
        Assert.Equal(traceId.PadLeft(TraceId.Length, '0'), result.ActivityContext.TraceId.ToString());
        Assert.Equal(spanId.PadLeft(SpanId.Length, '0'), result.ActivityContext.SpanId.ToString());
        Assert.Equal(flags == "1" ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None, result.ActivityContext.TraceFlags);
    }

    [Fact]
    public void InjectDoesNoopIfContextIsInvalid()
    {
        // arrange
        var propagationContext = default(PropagationContext);

        var headers = new Dictionary<string, string>();

        // act
        new JaegerPropagator().Inject(propagationContext, headers, Setter);

        // assert
        Assert.Empty(headers);
    }

    [Fact]
    public void InjectDoesNoopIfCarrierIsNull()
    {
        // arrange
        var traceId = ActivityTraceId.CreateFromString(TraceId.AsSpan());
        var spanId = ActivitySpanId.CreateFromString(SpanId.AsSpan());
        var propagationContext = new PropagationContext(
            new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, isRemote: true),
            default);

        // act
        new JaegerPropagator().Inject(propagationContext, null, Setter!);

        // assert
    }

    [Fact]
    public void InjectDoesNoopIfSetterIsNull()
    {
        // arrange
        var traceId = ActivityTraceId.CreateFromString(TraceId.AsSpan());
        var spanId = ActivitySpanId.CreateFromString(SpanId.AsSpan());
        var propagationContext = new PropagationContext(
            new ActivityContext(traceId, spanId, ActivityTraceFlags.Recorded, isRemote: true),
            default);

        var headers = new Dictionary<string, string>();

        // act
        new JaegerPropagator().Inject(propagationContext, headers, null!);

        // assert
        Assert.Empty(headers);
    }

    [Theory]
    [InlineData(FlagSampled)]
    [InlineData(FlagNotSampled)]
    public void InjectWillAddJaegerFormattedTraceToCarrier(string sampledFlag)
    {
        // arrange
        var traceId = ActivityTraceId.CreateFromString(TraceId.AsSpan());
        var spanId = ActivitySpanId.CreateFromString(SpanId.AsSpan());
        var flags = sampledFlag == "1" ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None;

        var propagationContext = new PropagationContext(new ActivityContext(traceId, spanId, flags, isRemote: true), default);

        var expectedValue = string.Join(
            JaegerDelimiter,
            traceId,
            spanId,
            ParentSpanId,
            sampledFlag);

        var headers = new Dictionary<string, string>();

        // act
        new JaegerPropagator().Inject(propagationContext, headers, Setter);

        // assert
        Assert.Single(headers);
        Assert.Equal(expectedValue, headers[JaegerHeader]);
    }
}
