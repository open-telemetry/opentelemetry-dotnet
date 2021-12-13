// <copyright file="SpanBuilderShimTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests
{
    public class SpanBuilderShimTests
    {
        private const string SpanName1 = "MySpanName/1";
        private const string SpanName2 = "MySpanName/2";
        private const string TracerName = "defaultactivitysource";

        static SpanBuilderShimTests()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            };

            ActivitySource.AddActivityListener(listener);
        }

        [Fact]
        public void CtorArgumentValidation()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            Assert.Throws<ArgumentNullException>(() => new SpanBuilderShim(null, "foo"));
            Assert.Throws<ArgumentNullException>(() => new SpanBuilderShim(tracer, null));
        }

        [Fact]
        public void IgnoreActiveSpan()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            // Add a parent. The shim requires that the ISpan implementation be a SpanShim
            shim.AsChildOf(new SpanShim(tracer.StartSpan(SpanName1)));

            // Set to Ignore
            shim.IgnoreActiveSpan();

            // build
            var spanShim = (SpanShim)shim.Start();

            Assert.Equal("foo", spanShim.Span.Activity.OperationName);
        }

        [Fact]
        public void StartWithExplicitTimestamp()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            var startTimestamp = DateTimeOffset.Now;
            shim.WithStartTimestamp(startTimestamp);

            // build
            var spanShim = (SpanShim)shim.Start();

            Assert.Equal(startTimestamp, spanShim.Span.Activity.StartTimeUtc);
        }

        [Fact]
        public void AsChildOf_WithNullSpan()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            // Add a null parent
            shim.AsChildOf((global::OpenTracing.ISpan)null);

            // build
            var spanShim = (SpanShim)shim.Start();

            Assert.Equal("foo", spanShim.Span.Activity.OperationName);
            Assert.Null(spanShim.Span.Activity.Parent);
        }

        [Fact]
        public void AsChildOf_WithSpan()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            // Add a parent.
            var span = new SpanShim(tracer.StartSpan(SpanName1));
            shim.AsChildOf(span);

            // build
            var spanShim = (SpanShim)shim.Start();

            Assert.Equal("foo", spanShim.Span.Activity.OperationName);
            Assert.NotNull(spanShim.Span.Activity.ParentId);
        }

        [Fact]
        public void Start_ActivityOperationRootSpanChecks()
        {
            // Create an activity
            var activity = new Activity("foo")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();

            // matching root operation name
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo", new List<string> { "foo" });
            var spanShim1 = (SpanShim)shim.Start();

            Assert.Equal("foo", spanShim1.Span.Activity.OperationName);

            // mis-matched root operation name
            shim = new SpanBuilderShim(tracer, "foo", new List<string> { "bar" });
            var spanShim2 = (SpanShim)shim.Start();

            Assert.Equal("foo", spanShim2.Span.Activity.OperationName);
            Assert.Equal(spanShim1.Context.TraceId, spanShim2.Context.TraceId);
        }

        [Fact]
        public void AsChildOf_MultipleCallsWithSpan()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            // Multiple calls
            var span1 = new SpanShim(tracer.StartSpan(SpanName1));
            var span2 = new SpanShim(tracer.StartSpan(SpanName2));
            shim.AsChildOf(span1);
            shim.AsChildOf(span2);

            // build
            var spanShim = (SpanShim)shim.Start();

            Assert.Equal("foo", spanShim.Span.Activity.OperationName);
            Assert.Contains(spanShim.Context.TraceId, spanShim.Span.Activity.TraceId.ToHexString());

            // TODO: Check for multi level parenting
        }

        [Fact]
        public void AsChildOf_WithNullSpanContext()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            // Add a null parent
            shim.AsChildOf((global::OpenTracing.ISpanContext)null);

            // build
            var spanShim = (SpanShim)shim.Start();

            // should be no parent.
            Assert.Null(spanShim.Span.Activity.Parent);
        }

        [Fact]
        public void AsChildOfWithSpanContext()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            // Add a parent
            var spanContext = SpanContextShimTests.GetSpanContextShim();
            var test = shim.AsChildOf(spanContext);

            // build
            var spanShim = (SpanShim)shim.Start();

            Assert.NotNull(spanShim.Span.Activity.ParentId);
        }

        [Fact]
        public void AsChildOf_MultipleCallsWithSpanContext()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            // Multiple calls
            var spanContext1 = SpanContextShimTests.GetSpanContextShim();
            var spanContext2 = SpanContextShimTests.GetSpanContextShim();

            // Add parent context
            shim.AsChildOf(spanContext1);

            // Adds as link as parent context already exists
            shim.AsChildOf(spanContext2);

            // build
            var spanShim = (SpanShim)shim.Start();
            var linkContext = spanShim.Span.Activity.Links.First().Context;

            Assert.Equal("foo", spanShim.Span.Activity.OperationName);
            Assert.Contains(spanContext1.TraceId, spanShim.Span.Activity.ParentId);
            Assert.Equal(spanContext2.SpanId, spanShim.Span.Activity.Links.First().Context.SpanId.ToHexString());
        }

        [Fact]
        public void WithTag_KeyIsSpanKindStringValue()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            shim.WithTag(global::OpenTracing.Tag.Tags.SpanKind.Key, global::OpenTracing.Tag.Tags.SpanKindClient);

            // build
            var spanShim = (SpanShim)shim.Start();

            // Not an attribute
            Assert.Empty(spanShim.Span.Activity.Tags);
            Assert.Equal("foo", spanShim.Span.Activity.OperationName);
            Assert.Equal(ActivityKind.Client, spanShim.Span.Activity.Kind);
        }

        [Fact]
        public void WithTag_KeyIsErrorStringValue()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            shim.WithTag(global::OpenTracing.Tag.Tags.Error.Key, "true");

            // build
            var spanShim = (SpanShim)shim.Start();

            // Span status should be set
            Assert.Equal(Status.Error, spanShim.Span.Activity.GetStatus());
        }

        [Fact]
        public void WithTag_KeyIsNullStringValue()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            shim.WithTag((string)null, "unused");

            // build
            var spanShim = (SpanShim)shim.Start();

            // Null key was ignored
            Assert.Empty(spanShim.Span.Activity.Tags);
        }

        [Fact]
        public void WithTag_ValueIsNullStringValue()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            shim.WithTag("foo", null);

            // build
            var spanShim = (SpanShim)shim.Start();

            // Null value was turned into string.empty
            Assert.Equal("foo", spanShim.Span.Activity.Tags.First().Key);
            Assert.Equal(string.Empty, spanShim.Span.Activity.Tags.First().Value);
        }

        [Fact]
        public void WithTag_KeyIsErrorBoolValue()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            shim.WithTag(global::OpenTracing.Tag.Tags.Error.Key, true);

            // build
            var spanShim = (SpanShim)shim.Start();

            // Span status should be set
            Assert.Equal(Status.Error, spanShim.Span.Activity.GetStatus());
        }

        [Fact]
        public void WithTag_VariousValueTypes()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            shim.WithTag("foo", "unused");
            shim.WithTag("bar", false);
            shim.WithTag("baz", 1);
            shim.WithTag("bizzle", 1D);
            shim.WithTag(new global::OpenTracing.Tag.BooleanTag("shnizzle"), true);
            shim.WithTag(new global::OpenTracing.Tag.IntOrStringTag("febrizzle"), "unused");
            shim.WithTag(new global::OpenTracing.Tag.StringTag("mobizzle"), "unused");

            // build
            var spanShim = (SpanShim)shim.Start();

            // Just verify the count
            Assert.Equal(7, spanShim.Span.Activity.Tags.Count());
        }

        [Fact]
        public void Start()
        {
            var tracer = TracerProvider.Default.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            // build
            var span = shim.Start() as SpanShim;

            // Just check the return value is a SpanShim and that the underlying OpenTelemetry Span.
            // There is nothing left to verify because the rest of the tests were already calling .Start() prior to verification.
            Assert.NotNull(span);
            Assert.Equal("foo", span.Span.Activity.OperationName);
        }
    }
}
