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
                GetRequestedDataUsingParentId = (ref ActivityCreationOptions<string> options) => ActivityDataRequest.AllData,
                GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => ActivityDataRequest.AllData,
            };

            ActivitySource.AddActivityListener(listener);
        }

        [Fact]
        public void CtorArgumentValidation()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            Assert.Throws<ArgumentNullException>(() => new SpanBuilderShim(null, "foo"));
            Assert.Throws<ArgumentNullException>(() => new SpanBuilderShim(tracer, null));
        }

        [Fact]
        public void IgnoreActiveSpan()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            // Add a parent. The shim requires that the ISpan implementation be a SpanShim
            shim.AsChildOf(new SpanShim(tracer.StartSpan(SpanName1)));

            // Set to Ignore
            shim.IgnoreActiveSpan();

            // build
            var spanShim = (SpanShim)shim.Start();

            Assert.Equal("foo", spanShim.activity.OperationName);
        }

        [Fact]
        public void StartWithExplicitTimestamp()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            var startTimestamp = DateTimeOffset.Now;
            shim.WithStartTimestamp(startTimestamp);

            // build
            var spanShim = (SpanShim)shim.Start();

            Assert.Equal(startTimestamp, spanShim.activity.StartTimeUtc);
        }

        [Fact]
        public void AsChildOf_WithNullSpan()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            // Add a null parent
            shim.AsChildOf((global::OpenTracing.ISpan)null);

            // build
            var spanShim = (SpanShim)shim.Start();

            Assert.Equal("foo", spanShim.activity.OperationName);
            Assert.Null(spanShim.activity.Parent);
        }

        [Fact]
        public void AsChildOf_WithSpan()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            // Add a parent.
            var span = new SpanShim(tracer.StartSpan(SpanName1));
            shim.AsChildOf(span);

            // build
            var spanShim = (SpanShim)shim.Start();

            Assert.Equal("foo", spanShim.activity.OperationName);
            Assert.NotNull(spanShim.activity.Parent);
            Assert.Equal(SpanName1, spanShim.activity.Parent.OperationName);
        }

        [Fact]
        public void Start_ActivityOperationRootSpanChecks()
        {
            // matching root operation name
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo", new List<string> { "foo" });
            var spanShim = (SpanShim)shim.Start();

            Assert.Equal("foo", spanShim.activity.OperationName);

            // mis-matched root operation name
            shim = new SpanBuilderShim(tracer, "foo", new List<string> { "bar" });
            spanShim = (SpanShim)shim.Start();

            Assert.Equal("foo", spanShim.activity.OperationName);
            Assert.Equal("foo", spanShim.activity.Parent.OperationName);
        }

        [Fact]
        public void AsChildOf_MultipleCallsWithSpan()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            // Multiple calls
            var span1 = new SpanShim(tracer.StartSpan(SpanName1));
            var span2 = new SpanShim(tracer.StartSpan(SpanName2));
            shim.AsChildOf(span1);
            shim.AsChildOf(span2);

            // build
            var spanShim = (SpanShim)shim.Start();

            Assert.Equal("foo", spanShim.activity.OperationName);
            Assert.Equal(SpanName2, spanShim.activity.Parent.OperationName);
            Assert.Equal(SpanName1, spanShim.activity.Parent.Parent.OperationName);
            Assert.Equal(spanShim.Context.TraceId, spanShim.activity.Parent.TraceId.ToHexString());
            Assert.Equal(spanShim.Context.TraceId, spanShim.activity.Parent.Parent.TraceId.ToHexString());
        }

        [Fact]
        public void AsChildOf_WithNullSpanContext()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            // Add a null parent
            shim.AsChildOf((global::OpenTracing.ISpanContext)null);

            // build
            var spanShim = (SpanShim)shim.Start();

            // should be no parent.
            Assert.Null(spanShim.activity.Parent);
        }

        [Fact]
        public void AsChildOfWithSpanContext()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            // Add a parent
            var spanContext = SpanContextShimTests.GetSpanContextShim();
            var test = shim.AsChildOf(spanContext);

            // build
            var spanShim = (SpanShim)shim.Start();

            Assert.NotNull(spanShim.activity.ParentId);
        }

        [Fact]
        public void AsChildOf_MultipleCallsWithSpanContext()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
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
            var linkContext = spanShim.activity.Links.First().Context;

            Assert.Equal("foo", spanShim.activity.OperationName);
            Assert.Contains(spanContext1.TraceId, spanShim.activity.ParentId);
            Assert.Equal(spanContext2.Context.SpanId, spanShim.activity.Links.First().Context.SpanId);
        }

        [Fact]
        public void WithTag_KeyIsSpanKindStringValue()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            shim.WithTag(global::OpenTracing.Tag.Tags.SpanKind.Key, global::OpenTracing.Tag.Tags.SpanKindClient);

            // build
            var spanShim = (SpanShim)shim.Start();

            // Not an attribute
            Assert.Empty(spanShim.activity.Tags);
            Assert.Equal("foo", spanShim.activity.OperationName);
            Assert.Equal(ActivityKind.Client, spanShim.activity.Kind);
        }

        [Fact]
        public void WithTag_KeyIsErrorStringValue()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            shim.WithTag(global::OpenTracing.Tag.Tags.Error.Key, "true");

            // build
            var spanShim = (SpanShim)shim.Start();

            // Span status should be set
            Assert.Equal(Status.Unknown, spanShim.activity.GetStatus());
        }

        [Fact]
        public void WithTag_KeyIsNullStringValue()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            shim.WithTag((string)null, "unused");

            // build
            var spanShim = (SpanShim)shim.Start();

            // Null key was ignored
            Assert.Empty(spanShim.activity.Tags);
        }

        [Fact]
        public void WithTag_ValueIsNullStringValue()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            shim.WithTag("foo", null);

            // build
            var spanShim = (SpanShim)shim.Start();

            // Null value was turned into string.empty
            Assert.Equal("foo", spanShim.activity.Tags.First().Key);
            Assert.Equal(string.Empty, spanShim.activity.Tags.First().Value);
        }

        [Fact]
        public void WithTag_KeyIsErrorBoolValue()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            shim.WithTag(global::OpenTracing.Tag.Tags.Error.Key, true);

            // build
            var spanShim = (SpanShim)shim.Start();

            // Span status should be set
            Assert.Equal(Status.Unknown, spanShim.activity.GetStatus());
        }

        [Fact]
        public void WithTag_VariousValueTypes()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
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
            Assert.Equal(7, spanShim.activity.Tags.Count());
        }

        [Fact]
        public void Start()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new SpanBuilderShim(tracer, "foo");

            // build
            var span = shim.Start() as SpanShim;

            // Just check the return value is a SpanShim and that the underlying OpenTelemetry Span.
            // There is nothing left to verify because the rest of the tests were already calling .Start() prior to verification.
            Assert.NotNull(span);
            Assert.Equal("foo", span.activity.OperationName);
        }
    }
}
