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
    using System.Diagnostics;
    using System.Linq;
    using OpenTelemetry.Trace;
    using Moq;
    using Xunit;

    public class SpanBuilderShimTests
    {
        [Fact]
        public void CtorArgumentValidation()
        {
            Assert.Throws<ArgumentNullException>(() => new SpanBuilderShim(null, "foo"));
            Assert.Throws<ArgumentNullException>(() => new SpanBuilderShim(new Mock<ITracer>().Object, null));
        }

        [Fact]
        public void IgnoreActiveSpan()
        {
            var tracerMock = GetDefaultTracerMock();
            var shim = new SpanBuilderShim(tracerMock.Object, "foo");

            // Add a parent. The shim requires that the ISpan implementation be a SpanShim
            shim.AsChildOf(new SpanShim(Defaults.GetOpenTelemetryMockSpan().Object));

            // Set to Ignore
            shim.IgnoreActiveSpan();

            // build
            shim.Start();

            tracerMock.Verify(o => o.StartRootSpan("foo", 0,
                default, It.Is<IEnumerable<Link>>(links => !links.Any())), Times.Once);
        }

        [Fact]
        public void StartWithExplicitTimestamp()
        {
            var tracerMock = GetDefaultTracerMock();
            var shim = new SpanBuilderShim(tracerMock.Object, "foo");

            var startTimestamp = DateTimeOffset.UtcNow;
            shim.WithStartTimestamp(startTimestamp);

            shim.Start();
            tracerMock.Verify(o => o.StartSpan("foo", 0,
                startTimestamp, It.Is<IEnumerable<Link>>(links => !links.Any())), Times.Once);
        }

        [Fact]
        public void AsChildOf_WithNullSpan()
        {
            var tracerMock = GetDefaultTracerMock();
            var shim = new SpanBuilderShim(tracerMock.Object, "foo");

            // Add a null parent
            shim.AsChildOf((global::OpenTracing.ISpan)null);

            // build
            shim.Start();

            tracerMock.Verify(o => o.StartSpan("foo", 0,
                default, It.Is<IEnumerable<Link>>(links => !links.Any())), Times.Once);
        }

        [Fact]
        public void AsChildOf_WithSpan()
        {
            var tracerMock = GetDefaultTracerMock();
            var shim = new SpanBuilderShim(tracerMock.Object, "foo");

            // Add a parent.
            var span = new SpanShim(Defaults.GetOpenTelemetryMockSpan().Object);
            shim.AsChildOf(span);

            // build
            shim.Start();

            tracerMock.Verify(o => o.StartSpan("foo", span.Span, 0,
                default, It.Is<IEnumerable<Link>>(links => !links.Any())), Times.Once);
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
                var tracerMock = GetDefaultTracerMock();
                var shim = new SpanBuilderShim(tracerMock.Object, "foo", new List<string> { "foo" });

                shim.Start();
                tracerMock.Verify(o => o.StartSpanFromActivity("foo", activity, 0, It.Is<IEnumerable<Link>>(links => !links.Any())), Times.Once);

                // mis-matched root operation name
                tracerMock = GetDefaultTracerMock();
                shim = new SpanBuilderShim(tracerMock.Object, "foo", new List<string> { "bar" });
                shim.Start();
                tracerMock.Verify(o => o.StartSpan("foo", 0, default, It.Is<IEnumerable<Link>>(links => !links.Any())), Times.Once);
            }
            finally
            {
                activity.Stop();
            }
        }

        [Fact]
        public void AsChildOf_MultipleCallsWithSpan()
        {
            var tracerMock = GetDefaultTracerMock();
            var shim = new SpanBuilderShim(tracerMock.Object, "foo");

            // Multiple calls
            var span1 = new SpanShim(Defaults.GetOpenTelemetryMockSpan().Object);
            var span2 = new SpanShim(Defaults.GetOpenTelemetryMockSpan().Object);
            shim.AsChildOf(span1);
            shim.AsChildOf(span2);

            // build
            shim.Start();

            tracerMock.Verify(o => o.StartSpan("foo", span1.Span, 0,
                default, It.Is<IEnumerable<Link>>(links => links.Single().Context.Equals(span2.Span.Context))), Times.Once);
        }

        [Fact]
        public void AsChildOf_WithNullSpanContext()
        {
            var tracerMock = GetDefaultTracerMock();
            var shim = new SpanBuilderShim(tracerMock.Object, "foo");

            // Add a null parent
            shim.AsChildOf((global::OpenTracing.ISpanContext)null);

            // build
            shim.Start();

            // should be no parent.
            tracerMock.Verify(o => o.StartSpan("foo", 0,
                default, It.Is<IEnumerable<Link>>(links => !links.Any())), Times.Once);
        }

        [Fact]
        public void AsChildOfWithSpanContext()
        {
            var tracerMock = GetDefaultTracerMock();
            var shim = new SpanBuilderShim(tracerMock.Object, "foo");
            
            // Add a parent
            var spanContext = SpanContextShimTests.GetSpanContextShim();
            shim.AsChildOf(spanContext);

            // build
            shim.Start();

            tracerMock.Verify(o => o.StartSpan("foo", spanContext.SpanContext, 0,
                default, It.Is<IEnumerable<Link>>(links => !links.Any())), Times.Once);
        }

        [Fact]
        public void AsChildOf_MultipleCallsWithSpanContext()
        {
            var tracerMock = GetDefaultTracerMock();
            var shim = new SpanBuilderShim(tracerMock.Object, "foo");

            // Multiple calls
            var spanContext1 = SpanContextShimTests.GetSpanContextShim();
            var spanContext2 = SpanContextShimTests.GetSpanContextShim();
            shim.AsChildOf(spanContext1);
            shim.AsChildOf(spanContext2);

            // build
            shim.Start();

            tracerMock.Verify(o => o.StartSpan("foo", spanContext1.SpanContext, 0, 
                default, It.Is<IEnumerable<Link>>(links => links.Single().Context.Equals(spanContext2.SpanContext))), Times.Once);
        }

        [Fact]
        public void WithTag_KeyisSpanKindStringValue()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var tracerMock = GetDefaultTracerMock(spanMock);
            var shim = new SpanBuilderShim(tracerMock.Object, "foo");

            shim.WithTag(global::OpenTracing.Tag.Tags.SpanKind.Key, global::OpenTracing.Tag.Tags.SpanKindClient);

            // build
            shim.Start();

            // Not an attribute
            Assert.Empty(spanMock.Attributes);

            tracerMock.Verify(o => o.StartSpan("foo", SpanKind.Client, default, It.Is<IEnumerable<Link>>(links => !links.Any())), Times.Once);
        }

        [Fact]
        public void WithTag_KeyisErrorStringValue()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var shim = new SpanBuilderShim(GetDefaultTracerMock(spanMock).Object, "foo");

            shim.WithTag(global::OpenTracing.Tag.Tags.Error.Key, "true");

            // build
            shim.Start();

            // Not an attribute
            Assert.Empty(spanMock.Attributes);

            // Span status should be set
            Assert.Equal(Status.Unknown, spanMock.Status);
        }

        [Fact]
        public void WithTag_KeyisNullStringValue()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var shim = new SpanBuilderShim(GetDefaultTracerMock(spanMock).Object, "foo");

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
            var shim = new SpanBuilderShim(GetDefaultTracerMock(spanMock).Object, "foo");

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
            var shim = new SpanBuilderShim(GetDefaultTracerMock(spanMock).Object, "foo");

            shim.WithTag(global::OpenTracing.Tag.Tags.Error.Key, true);

            // build
            shim.Start();

            // Not an attribute
            Assert.Empty(spanMock.Attributes);

            // Span status should be set
            Assert.Equal(Status.Unknown, spanMock.Status);
        }

        [Fact]
        public void WithTag_VariousValueTypes()
        {
            var spanMock = Defaults.GetOpenTelemetrySpanMock();
            var shim = new SpanBuilderShim(GetDefaultTracerMock(spanMock).Object, "foo");

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
            var tracerMock = GetDefaultTracerMock(spanMock);
            var shim = new SpanBuilderShim(tracerMock.Object, "foo");

            // build
            var span = shim.Start() as SpanShim;

            // Just check the return value is a SpanShim and that the underlying OpenTelemetry Span.
            // There is nothing left to verify because the rest of the tests were already calling .Start() prior to verification.
            Assert.NotNull(span);
            Assert.Equal(spanMock, span.Span);
        }

        private static Mock<ITracer> GetDefaultTracerMock(SpanMock spanMock = null)
        {
            var mock = new Mock<ITracer>();
            spanMock = spanMock ?? Defaults.GetOpenTelemetrySpanMock();

            mock.Setup(x => x.StartRootSpan(It.IsAny<string>(), It.IsAny<SpanKind>(), It.IsAny<DateTimeOffset>(), It.IsAny<IEnumerable<Link>>())).Returns(spanMock);
            mock.Setup(x => x.StartSpan(It.IsAny<string>(), It.IsAny<SpanKind>(), It.IsAny<DateTimeOffset>(), It.IsAny<IEnumerable<Link>>())).Returns(spanMock);
            mock.Setup(x => x.StartSpan(It.IsAny<string>(), It.IsAny<ISpan>(), It.IsAny<SpanKind>(), It.IsAny<DateTimeOffset>(), It.IsAny<IEnumerable<Link>>())).Returns(spanMock);
            mock.Setup(x => x.StartSpan(It.IsAny<string>(), It.IsAny<SpanContext>(), It.IsAny<SpanKind>(), It.IsAny<DateTimeOffset>(), It.IsAny<IEnumerable<Link>>())).Returns(spanMock);
            mock.Setup(x => x.StartSpanFromActivity(It.IsAny<string>(), It.IsAny<Activity>(), It.IsAny<SpanKind>(), It.IsAny<IEnumerable<Link>>())).Returns(spanMock);
            return mock;
        }
    }
}
