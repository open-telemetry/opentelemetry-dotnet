// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
using System.Collections;
using System.Diagnostics;
using Moq;
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
        Assert.Throws<ArgumentNullException>(() => new TracerShim((TracerProvider)null, null));

        // null tracer provider
        Assert.Throws<ArgumentNullException>(() => new TracerShim((TracerProvider)null, new TraceContextPropagator()));
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
        var mockFormat = new Mock<IFormat<ITextMap>>();
        var mockCarrier = new Mock<ITextMap>();

        Assert.Throws<ArgumentNullException>(() => shim.Inject(null, mockFormat.Object, mockCarrier.Object));
        Assert.Throws<InvalidCastException>(() => shim.Inject(new Mock<ISpanContext>().Object, mockFormat.Object, mockCarrier.Object));
        Assert.Throws<ArgumentNullException>(() => shim.Inject(spanContextShim, null, mockCarrier.Object));
        Assert.Throws<ArgumentNullException>(() => shim.Inject(spanContextShim, mockFormat.Object, null));
    }

    [Fact]
    public void Inject_UnknownFormatIgnored()
    {
        var shim = new TracerShim(TracerProvider.Default, new TraceContextPropagator());

        var spanContextShim = new SpanContextShim(new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded));

        // Only two specific types of ITextMap are supported, and neither is a Mock.
        var mockCarrier = new Mock<ITextMap>();
        shim.Inject(spanContextShim, new Mock<IFormat<ITextMap>>().Object, mockCarrier.Object);

        // Verify that the carrier mock was never called.
        mockCarrier.Verify(x => x.Set(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Extract_ArgumentValidation()
    {
        var shim = new TracerShim(TracerProvider.Default, new TraceContextPropagator());

        Assert.Throws<ArgumentNullException>(() => shim.Extract(null, new Mock<ITextMap>().Object));
        Assert.Throws<ArgumentNullException>(() => shim.Extract(new Mock<IFormat<ITextMap>>().Object, null));
    }

    [Fact]
    public void Extract_UnknownFormatIgnored()
    {
        var shim = new TracerShim(TracerProvider.Default, new TraceContextPropagator());

        var spanContextShim = new SpanContextShim(new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None));

        // Only two specific types of ITextMap are supported, and neither is a Mock.
        var mockCarrier = new Mock<ITextMap>();
        var context = shim.Extract(new Mock<IFormat<ITextMap>>().Object, mockCarrier.Object);

        // Verify that the carrier mock was never called.
        mockCarrier.Verify(x => x.GetEnumerator(), Times.Never);
    }

    [Fact]
    public void Extract_InvalidTraceParent()
    {
        var shim = new TracerShim(TracerProvider.Default, new TraceContextPropagator());

        var mockCarrier = new Mock<ITextMap>();

        // The ProxyTracer uses OpenTelemetry.Context.Propagation.TraceContextPropagator, so we need to satisfy the traceparent key at the least
        var carrierMap = new Dictionary<string, string>
        {
            // This is an invalid traceparent value
            { "traceparent", "unused" },
        };

        mockCarrier.Setup(x => x.GetEnumerator()).Returns(carrierMap.GetEnumerator());
        var spanContextShim = shim.Extract(BuiltinFormats.TextMap, mockCarrier.Object) as SpanContextShim;

        // Verify that the carrier was called
        mockCarrier.Verify(x => x.GetEnumerator(), Times.Once);

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

        AssertOpenTracerSpanContextEqual(spanContextShim, extractedSpanContext);
    }

    private static void AssertOpenTracerSpanContextEqual(ISpanContext source, ISpanContext target)
    {
        Assert.Equal(source.TraceId, target.TraceId);
        Assert.Equal(source.SpanId, target.SpanId);

        // TODO BaggageItems are not implemented yet.
    }

    /// <summary>
    /// Simple ITextMap implementation used for the inject/extract tests.
    /// </summary>
    /// <seealso cref="OpenTracing.Propagation.ITextMap" />
    private class TextMapCarrier : ITextMap
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

    /// <summary>
    /// Simple IBinary implementation used for the inject/extract tests.
    /// </summary>
    /// <seealso cref="OpenTracing.Propagation.IBinary" />
    private class BinaryCarrier : IBinary
    {
        private readonly MemoryStream carrierStream = new();

        public MemoryStream Get() => this.carrierStream;

        public void Set(MemoryStream stream)
        {
            this.carrierStream.SetLength(stream.Length);
            this.carrierStream.Seek(0, SeekOrigin.Begin);
            stream.CopyTo(this.carrierStream, (int)this.carrierStream.Length);
        }
    }
}
