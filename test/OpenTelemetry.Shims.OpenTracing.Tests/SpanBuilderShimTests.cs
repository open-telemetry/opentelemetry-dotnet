// <copyright file="SpanBuilderShimTests.cs" company="OpenTelemetry Authors">
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
namespace OpenTelemetry.Shims.OpenTracing.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Moq;
    using Xunit;

    public class SpanBuilderShimTests
    {
        [Fact]
        public void CtorArgumentValidation()
        {
            Assert.Throws<ArgumentNullException>(() => new SpanBuilderShim(null, "foo"));
            Assert.Throws<ArgumentNullException>(() => new SpanBuilderShim(new Mock<Trace.ITracer>().Object, null));
        }

        [Fact]
        public void IgnoreActiveSpan()
        {
            var spanBuilderMock = GetDefaultSpanBuilderMock();
            var shim = new SpanBuilderShim(GetDefaultTracer(spanBuilderMock), "foo");

            // Add a parent. The shim requires that the ISpan implementation be a SpanShim
            shim.AsChildOf(new SpanShim(Defaults.GetOpenTelemetryMockSpan().Object));

            // Set to Ignore
            shim.IgnoreActiveSpan();

            spanBuilderMock.Verify(o => o.SetParent(It.IsAny<Trace.SpanContext>()), Times.Never);

            // There should be two methods calls to the underlying the builder. SetNoParent is last.
            int callOrder = 0;
            spanBuilderMock.Setup(x => x.SetParent(It.IsAny<Trace.ISpan>())).Callback(() => Assert.Equal(0, callOrder++));
            spanBuilderMock.Setup(x => x.SetNoParent()).Callback(() => Assert.Equal(1, callOrder++));

            // build
            shim.Start();
        }

        [Fact]
        public void AsChildOf_WithNullSpan()
        {
            var spanBuilderMock = GetDefaultSpanBuilderMock();
            var shim = new SpanBuilderShim(GetDefaultTracer(spanBuilderMock), "foo");

            // Add a null parent
            shim.AsChildOf((global::OpenTracing.ISpan)null);

            // build
            shim.Start();

            spanBuilderMock.Verify(o => o.SetParent(It.IsAny<Trace.ISpan>()), Times.Never);
            spanBuilderMock.Verify(o => o.SetParent(It.IsAny<Trace.SpanContext>()), Times.Never);
        }

        [Fact]
        public void AsChildOf_WithSpan()
        {
            var spanBuilderMock = GetDefaultSpanBuilderMock();
            var shim = new SpanBuilderShim(GetDefaultTracer(spanBuilderMock), "foo");

            // Add a parent.
            var span = new SpanShim(Defaults.GetOpenTelemetryMockSpan().Object);
            shim.AsChildOf(span);

            // build
            shim.Start();

            spanBuilderMock.Verify(o => o.SetParent(span.Span), Times.Once);
            spanBuilderMock.Verify(o => o.SetParent(It.IsAny<Trace.SpanContext>()), Times.Never);
        }

        [Fact]
        public void Start_ActivityOperationRootSpanChecks()
        {
            // Create an activity
            var activity = new System.Diagnostics.Activity("foo")
                .SetIdFormat(System.Diagnostics.ActivityIdFormat.W3C)
                .Start();

            try
            {
                // matching root operation name
                var spanBuilderMock = GetDefaultSpanBuilderMock();
                var shim = new SpanBuilderShim(GetDefaultTracer(spanBuilderMock), "foo", new List<string> { "foo" });
                shim.Start();
                spanBuilderMock.Verify(o => o.SetCreateChild(false), Times.Once);

                // mis-matched root operation name
                spanBuilderMock = GetDefaultSpanBuilderMock();
                shim = new SpanBuilderShim(GetDefaultTracer(spanBuilderMock), "foo", new List<string> { "bar" });
                shim.Start();
                spanBuilderMock.Verify(o => o.SetCreateChild(true), Times.Once);
            }
            finally
            {
                activity.Stop();
            }
        }

        [Fact]
        public void AsChildOf_MultipleCallsWithSpan()
        {
            var spanBuilderMock = GetDefaultSpanBuilderMock();
            var shim = new SpanBuilderShim(GetDefaultTracer(spanBuilderMock), "foo");

            // Multiple calls
            var span1 = new SpanShim(Defaults.GetOpenTelemetryMockSpan().Object);
            var span2 = new SpanShim(Defaults.GetOpenTelemetryMockSpan().Object);
            shim.AsChildOf(span1);
            shim.AsChildOf(span2);

            // build
            shim.Start();

            spanBuilderMock.Verify(o => o.SetParent(span1.Span), Times.Once);
            spanBuilderMock.Verify(o => o.SetParent(It.IsAny<Trace.SpanContext>()), Times.Never);

            // The rest become links
            spanBuilderMock.Verify(o => o.AddLink(It.Is<Trace.ILink>(link => link.Context == span2.Span.Context)), Times.Once);
        }

        [Fact]
        public void AsChildOf_WithNullSpanContext()
        {
            var spanBuilderMock = GetDefaultSpanBuilderMock();
            var shim = new SpanBuilderShim(GetDefaultTracer(spanBuilderMock), "foo");

            // Add a null parent
            shim.AsChildOf((global::OpenTracing.ISpanContext)null);

            // build
            shim.Start();

            // should be no parent.
            spanBuilderMock.Verify(o => o.SetParent(It.IsAny<Trace.SpanContext>()), Times.Never);
            spanBuilderMock.Verify(o => o.SetParent(It.IsAny<Trace.ISpan>()), Times.Never);
        }

        [Fact]
        public void AsChildOfWithSpanContext()
        {
            var spanBuilderMock = GetDefaultSpanBuilderMock();
            var shim = new SpanBuilderShim(GetDefaultTracer(spanBuilderMock), "foo");

            // Add a parent
            var spanContext = SpanContextShimTests.GetSpanContextShim();
            shim.AsChildOf(spanContext);

            // build
            shim.Start();

            spanBuilderMock.Verify(o => o.SetParent(spanContext.SpanContext), Times.Once);
            spanBuilderMock.Verify(o => o.SetParent(It.IsAny<Trace.ISpan>()), Times.Never);
        }

        [Fact]
        public void AsChildOf_MultipleCallsWithSpanContext()
        {
            var spanBuilderMock = GetDefaultSpanBuilderMock();
            var shim = new SpanBuilderShim(GetDefaultTracer(spanBuilderMock), "foo");

            // Multiple calls
            var spanContext1 = SpanContextShimTests.GetSpanContextShim();
            var spanContext2 = SpanContextShimTests.GetSpanContextShim();
            shim.AsChildOf(spanContext1);
            shim.AsChildOf(spanContext2);

            // build
            shim.Start();

            // Only a single call to SetParent
            spanBuilderMock.Verify(o => o.SetParent(spanContext1.SpanContext), Times.Once);
            spanBuilderMock.Verify(o => o.SetParent(It.IsAny<Trace.ISpan>()), Times.Never);

            // The rest become links
            spanBuilderMock.Verify(o => o.AddLink(It.Is<Trace.ILink>(link => link.Context == spanContext2.SpanContext)), Times.Once);
        }

        [Fact]
        public void WithTag_KeyisSpanKindStringValue()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var spanBuilderMock = GetDefaultSpanBuilderMock(spanMock);
            var shim = new SpanBuilderShim(GetDefaultTracer(spanBuilderMock), "foo");

            shim.WithTag(global::OpenTracing.Tag.Tags.SpanKind.Key, global::OpenTracing.Tag.Tags.SpanKindClient);

            // build
            shim.Start();

            // Not an attribute
            Assert.Empty(spanMock.Attributes);

            spanBuilderMock.Verify(o => o.SetSpanKind(Trace.SpanKind.Client), Times.Once);
        }

        [Fact]
        public void WithTag_KeyisErrorStringValue()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var spanBuilderMock = GetDefaultSpanBuilderMock(spanMock);
            var shim = new SpanBuilderShim(GetDefaultTracer(spanBuilderMock), "foo");

            shim.WithTag(global::OpenTracing.Tag.Tags.Error.Key, "true");

            // build
            shim.Start();

            // Not an attribute
            Assert.Empty(spanMock.Attributes);

            // Span status should be set
            Assert.Equal(Trace.Status.Unknown, spanMock.Status);
        }

        [Fact]
        public void WithTag_KeyisNullStringValue()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var spanBuilderMock = GetDefaultSpanBuilderMock(spanMock);
            var shim = new SpanBuilderShim(GetDefaultTracer(spanBuilderMock), "foo");

            shim.WithTag((string)null, "unused");

            // build
            shim.Start();

            // Null key was ignored
            Assert.Empty(spanMock.Attributes);
        }

        [Fact]
        public void WithTag_ValueisNullStringValue()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var spanBuilderMock = GetDefaultSpanBuilderMock(spanMock);
            var shim = new SpanBuilderShim(GetDefaultTracer(spanBuilderMock), "foo");

            shim.WithTag("foo", null);

            // build
            shim.Start();

            // Null value was turned into string.empty
            Assert.Equal("foo", spanMock.Attributes.First().Key);
            Assert.Equal(string.Empty, spanMock.Attributes.First().Value);
        }

        [Fact]
        public void WithTag_KeyisErrorBoolValue()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var spanBuilderMock = GetDefaultSpanBuilderMock(spanMock);
            var shim = new SpanBuilderShim(GetDefaultTracer(spanBuilderMock), "foo");

            shim.WithTag(global::OpenTracing.Tag.Tags.Error.Key, true);

            // build
            shim.Start();

            // Not an attribute
            Assert.Empty(spanMock.Attributes);

            // Span status should be set
            Assert.Equal(Trace.Status.Unknown, spanMock.Status);
        }

        [Fact]
        public void WithTag_VariousValueTypes()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var spanBuilderMock = GetDefaultSpanBuilderMock(spanMock);
            var shim = new SpanBuilderShim(GetDefaultTracer(spanBuilderMock), "foo");

            shim.WithTag("foo", "unused");
            shim.WithTag("bar", false);
            shim.WithTag("baz", 1);
            shim.WithTag("bizzle", 1D);
            shim.WithTag(new global::OpenTracing.Tag.BooleanTag("shnizzle"), true);
            shim.WithTag(new global::OpenTracing.Tag.IntOrStringTag("febrizzle"), "unused");
            shim.WithTag(new global::OpenTracing.Tag.StringTag("mobizzle"), "unused");

            // build
            shim.Start();

            // Just verify the count
            Assert.Equal(7, spanMock.Attributes.Count);
        }

        [Fact]
        public void Start()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var spanBuilderMock = GetDefaultSpanBuilderMock(spanMock);
            var shim = new SpanBuilderShim(GetDefaultTracer(spanBuilderMock), "foo");

            // build
            var span = shim.Start() as SpanShim;

            // Just check the return value is a SpanShim and that the underlying OpenTelemetry Span.
            // There is nothing left to verify because the rest of the tests were already calling .Start() prior to verification.
            Assert.NotNull(span);
            Assert.Equal(spanMock, span.Span);
        }

        private static Mock<Trace.ISpanBuilder> GetDefaultSpanBuilderMock(SpanMock spanMock = null)
        {
            var mock = new Mock<Trace.ISpanBuilder>();
            spanMock = spanMock ?? Defaults.GetOpenTelemetrySpanMock();
            mock.Setup(x => x.StartSpan()).Returns(spanMock);

            return mock;
        }

        private static Trace.ITracer GetDefaultTracer(Mock<Trace.ISpanBuilder> spanBuilderMock)
        {
            var tracerMock = new Mock<Trace.ITracer>();
            tracerMock.Setup(x => x.SpanBuilder(It.IsAny<string>())).Returns(spanBuilderMock.Object);
            return tracerMock.Object;
        }
    }
}
