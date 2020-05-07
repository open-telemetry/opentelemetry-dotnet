﻿// <copyright file="TracerShimTests.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using global::OpenTracing;
using global::OpenTracing.Propagation;
using Moq;
using OpenTelemetry.Context.Propagation;
using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests
{
    public class TracerShimTests
    {
        [Fact]
        public void CtorArgumentValidation()
        {
            // null tracer and text format
            Assert.Throws<ArgumentNullException>(() => new TracerShim(null, null));

            // null tracer
            Assert.Throws<ArgumentNullException>(() => new TracerShim(null, new TraceContextFormat()));

            // null context format
            var tracerMock = new Mock<Trace.Tracer>();
            Assert.Throws<ArgumentNullException>(() => new TracerShim(tracerMock.Object, null));
        }

        [Fact]
        public void ScopeManager_NotNull()
        {
            var tracerMock = new Mock<Trace.Tracer>();
            var shim = new TracerShim(tracerMock.Object, new TraceContextFormat());

            // Internals of the ScopeManagerShim tested elsewhere
            Assert.NotNull(shim.ScopeManager as ScopeManagerShim);
        }

        [Fact]
        public void BuildSpan_NotNull()
        {
            var tracerMock = new Mock<Trace.Tracer>();
            var shim = new TracerShim(tracerMock.Object, new TraceContextFormat());

            // Internals of the SpanBuilderShim tested elsewhere
            Assert.NotNull(shim.BuildSpan("foo") as SpanBuilderShim);
        }

        [Fact]
        public void Inject_ArgumentValidation()
        {
            var tracerMock = new Mock<Trace.Tracer>();
            var shim = new TracerShim(tracerMock.Object, new TraceContextFormat());

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
            var tracerMock = new Mock<Trace.Tracer>();
            var shim = new TracerShim(tracerMock.Object, new TraceContextFormat());

            var spanContextShim = new SpanContextShim(Defaults.GetOpenTelemetrySpanContext());

            // Only two specific types of ITextMap are supported, and neither is a Mock.
            var mockCarrier = new Mock<ITextMap>();
            shim.Inject(spanContextShim, new Mock<IFormat<ITextMap>>().Object, mockCarrier.Object);

            // Verify that the carrier mock was never called.
            mockCarrier.Verify(x => x.Set(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void Extract_ArgumentValidation()
        {
            var tracerMock = new Mock<Trace.Tracer>();
            var shim = new TracerShim(tracerMock.Object, new TraceContextFormat());

            Assert.Throws<ArgumentNullException>(() => shim.Extract(null, new Mock<ITextMap>().Object));
            Assert.Throws<ArgumentNullException>(() => shim.Extract(new Mock<IFormat<ITextMap>>().Object, null));
        }

        [Fact]
        public void Extract_UnknownFormatIgnored()
        {
            var tracerMock = new Mock<Trace.Tracer>();
            var shim = new TracerShim(tracerMock.Object, new TraceContextFormat());

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
            var tracerMock = new Mock<Trace.Tracer>();
            var shim = new TracerShim(tracerMock.Object, new TraceContextFormat());

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
        public void InjectExtract_TextMap_Ok()
        {
            var tracerMock = new Mock<Trace.Tracer>();

            var carrier = new TextMapCarrier();

            var spanContextShim = new SpanContextShim(Defaults.GetOpenTelemetrySpanContext());

            var format = new TraceContextFormat();

            var shim = new TracerShim(tracerMock.Object, format);

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

        public interface UnsupportedCarrierType
        { }

        /// <summary>
        /// Simple ITextMap implementation used for the inject/extract tests.
        /// </summary>
        /// <seealso cref="OpenTracing.Propagation.ITextMap" />
        private class TextMapCarrier : ITextMap
        {
            private readonly Dictionary<string, string> map = new Dictionary<string, string>();

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
            private readonly MemoryStream carrierStream = new MemoryStream();

            public MemoryStream Get() => this.carrierStream;

            public void Set(MemoryStream stream)
            {
                this.carrierStream.SetLength(stream.Length);
                this.carrierStream.Seek(0, SeekOrigin.Begin);
                stream.CopyTo(this.carrierStream, (int)this.carrierStream.Length);
            }
        }
    }
}
