// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Diagnostics;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using OpenTracing;
using OpenTracing.Propagation;
using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

public class TracerShimTests
{
    [Fact]
    public void CtorArgumentValidation()
    {
        // null tracer provider and text format
        Assert.Throws<ArgumentNullException>(() => new TracerShim(null!, null));

        // null tracer provider
        Assert.Throws<ArgumentNullException>(() => new TracerShim(null!, new TraceContextPropagator()));
    }

    [Fact]
    public void ScopeManager_NotNull()
    {
        var shim = new TracerShim(TracerProvider.Default, new TraceContextPropagator());

        // Internals of the ScopeManagerShim tested elsewhere
        Assert.NotNull(shim.ScopeManager as ScopeManagerShim);
    }

    [Fact]
    public void BuildSpan_NotNull()
    {
        var shim = new TracerShim(TracerProvider.Default, new TraceContextPropagator());

        // Internals of the SpanBuilderShim tested elsewhere
        Assert.NotNull(shim.BuildSpan("foo") as SpanBuilderShim);
    }

    [Fact]
    public void Inject_ArgumentValidation()
    {
        var shim = new TracerShim(TracerProvider.Default, new TraceContextPropagator());

        var spanContextShim = new SpanContextShim(new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None));
        var testFormat = new TestFormatTextMap();
        var testCarrier = new TestTextMap();

        Assert.Throws<ArgumentNullException>(() => shim.Inject(null!, testFormat, testCarrier));
        Assert.Throws<InvalidCastException>(() => shim.Inject(new TestSpanContext(), testFormat, testCarrier));
        Assert.Throws<ArgumentNullException>(() => shim.Inject(spanContextShim, null!, testCarrier));
        Assert.Throws<ArgumentNullException>(() => shim.Inject(spanContextShim, testFormat!, null));
    }

    [Fact]
    public void Inject_UnknownFormatIgnored()
    {
        var shim = new TracerShim(TracerProvider.Default, new TraceContextPropagator());

        var spanContextShim = new SpanContextShim(new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded));

        // Only two specific types of ITextMap are supported, and neither is a Mock.
        var testCarrier = new TestTextMap();
        shim.Inject(spanContextShim, new TestFormatTextMap(), testCarrier);

        // Verify that the test carrier was never called.
        Assert.False(testCarrier.SetCalled);
    }

    [Fact]
    public void Extract_ArgumentValidation()
    {
        var shim = new TracerShim(TracerProvider.Default, new TraceContextPropagator());

        Assert.Throws<ArgumentNullException>(() => shim.Extract(null!, new TestTextMap()));
        Assert.Throws<ArgumentNullException>(() => shim.Extract(new TestFormatTextMap()!, null));
    }

    [Fact]
    public void Extract_UnknownFormatIgnored()
    {
        var shim = new TracerShim(TracerProvider.Default, new TraceContextPropagator());
        _ = new SpanContextShim(new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None));

        // Only two specific types of ITextMap are supported, and neither is a Mock.
        var testCarrier = new TestTextMap();
        _ = shim.Extract(new TestFormatTextMap(), testCarrier);

        // Verify that the test carrier was never called.
        Assert.False(testCarrier.SetCalled);
    }

    [Fact]
    public void Extract_InvalidTraceParent()
    {
        var shim = new TracerShim(TracerProvider.Default, new TraceContextPropagator());

        var testCarrier = new TestTextMap();

        // The ProxyTracer uses OpenTelemetry.Context.Propagation.TraceContextPropagator, so we need to satisfy the traceparent key at the least
        testCarrier.Items["traceparent"] = "unused";

        var spanContextShim = shim.Extract(BuiltinFormats.TextMap, testCarrier) as SpanContextShim;

        // Verify that the carrier was called
        Assert.True(testCarrier.GetEnumeratorCalled);

        Assert.Null(spanContextShim);
    }

    [Fact]
    public void InjectExtract_TextMap_Ok()
    {
        var carrier = new TextMapCarrier();

        var spanContextShim = new SpanContextShim(new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None));

        var format = new TraceContextPropagator();

        var shim = new TracerShim(TracerProvider.Default, format);

        // first inject
        shim.Inject(spanContextShim, BuiltinFormats.TextMap, carrier);

        // then extract
        var extractedSpanContext = shim.Extract(BuiltinFormats.TextMap, carrier);

        AssertOpenTracerSpanContextEqual(spanContextShim, extractedSpanContext!);
    }

    private static void AssertOpenTracerSpanContextEqual(SpanContextShim source, ISpanContext target)
    {
        Assert.Equal(source.TraceId, target.TraceId);
        Assert.Equal(source.SpanId, target.SpanId);

        // TODO BaggageItems are not implemented yet.
    }

    /// <summary>
    /// Simple ITextMap implementation used for the inject/extract tests.
    /// </summary>
    /// <seealso cref="OpenTracing.Propagation.ITextMap" />
    private sealed class TextMapCarrier : ITextMap
    {
        private readonly Dictionary<string, string> map = new();

        public IDictionary<string, string> Map => this.map;

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => this.map.GetEnumerator();

        public void Set(string key, string value)
        {
            this.map[key] = value;
        }

        IEnumerator IEnumerable.GetEnumerator() => this.map.GetEnumerator();
    }
}
