// <copyright file="TracerShimTests.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Shims.OpenTracing.Tests
{
    using System;
    using System.Collections.Generic;
    using global::OpenTracing;
    using global::OpenTracing.Propagation;
    using Moq;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Trace.Configuration;
    using Xunit;

    public class TracerShimTests
    {
        private readonly Trace.ITracer tracer;

        public TracerShimTests()
        {
            tracer = TracerFactory.Create(b => b.SetProcessor(e => new SimpleSpanProcessor(e))).GetTracer(null);
        }

        [Fact]
        public void CtorArgumentValidation()
        {
            Assert.Throws<ArgumentNullException>(() => TracerShim.Create(null));
        }

        [Fact]
        public void ScopeManager_NotNull()
        {
            var shim = TracerShim.Create(tracer);

            // Internals of the ScopeManagerShim tested elsewhere
            Assert.NotNull(shim.ScopeManager as ScopeManagerShim);
        }

        [Fact]
        public void BuildSpan_NotNull()
        {
            var shim = TracerShim.Create(tracer);

            // Internals of the SpanBuilderShim tested elsewhere
            Assert.NotNull(shim.BuildSpan("foo") as SpanBuilderShim);
        }

        [Fact]
        public void Inject_ArgumentValidation()
        {
            var shim = TracerShim.Create(tracer);

            var spanContextShim = new SpanContextShim(Defaults.GetOpenTelemetrySpanContext());
            var mockFormat = new Mock<IFormat<ITextMap>>();
            var mockCarrier = new Mock<ITextMap>();

            Assert.Throws<ArgumentNullException>(() => shim.Inject(null, mockFormat.Object, mockCarrier.Object));
            Assert.Throws<ArgumentException>(() => shim.Inject(new Mock<ISpanContext>().Object, mockFormat.Object, mockCarrier.Object));
            Assert.Throws<ArgumentNullException>(() => shim.Inject(spanContextShim, null, mockCarrier.Object));
            Assert.Throws<ArgumentNullException>(() => shim.Inject(spanContextShim, mockFormat.Object, null));
        }

        [Fact]
        public void Inject_UnknownFormatIgnored()
        {
            var shim = TracerShim.Create(tracer);

            var spanContextShim = new SpanContextShim(Defaults.GetOpenTelemetrySpanContext());

            // Only two specific types of ITextMap are supported, and neither is a Mock.
            var mockCarrier = new Mock<ITextMap>();
            shim.Inject(spanContextShim, new Mock<IFormat<ITextMap>>().Object, mockCarrier.Object);

            // Verify that the carrier mock was never called.
            mockCarrier.Verify(x => x.Set(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void Inject_Ok()
        {
            var shim = TracerShim.Create(tracer);

            var spanContextShim = new SpanContextShim(Defaults.GetOpenTelemetrySpanContext());

            var mockCarrier = new Mock<ITextMap>();
            shim.Inject(spanContextShim, BuiltinFormats.TextMap, mockCarrier.Object);

            // Verify that the carrier mock was invoked at least one time.
            mockCarrier.Verify(x => x.Set(It.IsAny<string>(), It.IsAny<string>()), Times.AtLeastOnce);
        }

        [Fact]
        public void Extract_ArgumentValidation()
        {
            var shim = TracerShim.Create(tracer);

            Assert.Throws<ArgumentNullException>(() => shim.Extract(null, new Mock<ITextMap>().Object));
            Assert.Throws<ArgumentNullException>(() => shim.Extract(new Mock<IFormat<ITextMap>>().Object, null));
        }

        [Fact]
        public void Extract_UnknownFormatIgnored()
        {
            var shim = TracerShim.Create(tracer);

            var spanContextShim = new SpanContextShim(Defaults.GetOpenTelemetrySpanContext());

            // Only two specific types of ITextMap are supported, and neither is a Mock.
            var mockCarrier = new Mock<ITextMap>();
            var context = shim.Extract(new Mock<IFormat<ITextMap>>().Object, mockCarrier.Object);

            // Verify that the carrier mock was never called.
            mockCarrier.Verify(x => x.GetEnumerator(), Times.Never);
        }

        [Fact]
        public void Extract_InvalidTraceParent()
        {
            var shim = TracerShim.Create(tracer);

            var mockCarrier = new Mock<ITextMap>();

            // The ProxyTracer uses OpenTelemetry.Context.Propagation.TraceContextFormat, so we need to satisfy the traceparent key at the least
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
        public void Extract_Ok()
        {
            var shim = TracerShim.Create(tracer);

            var mockCarrier = new Mock<ITextMap>();

            // The ProxyTracer uses OpenTelemetry.Context.Propagation.TraceContextFormat, so we need to satisfy that.
            var traceContextFormat = new TraceContextFormat();
            var spanContext = Defaults.GetOpenTelemetrySpanContext();

            var carrierMap = new Dictionary<string, string>();

            // inject the SpanContext into the carrier map
            traceContextFormat.Inject<Dictionary<string, string>>(spanContext, carrierMap, (map, key, value) => map.Add(key, value));

            // send the populated carrier map to the extract method.
            mockCarrier.Setup(x => x.GetEnumerator()).Returns(carrierMap.GetEnumerator());
            var spanContextShim = shim.Extract(BuiltinFormats.TextMap, mockCarrier.Object) as SpanContextShim;

            // Verify that the carrier was called
            mockCarrier.Verify(x => x.GetEnumerator(), Times.Once);

            Assert.Equal(spanContext, spanContextShim.SpanContext);
        }


        public interface UnsupportedCarrierType
        { }
    }
}
