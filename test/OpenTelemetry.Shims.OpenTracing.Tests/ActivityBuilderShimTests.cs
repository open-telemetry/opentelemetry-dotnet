// <copyright file="ActivityBuilderShimTests.cs" company="OpenTelemetry Authors">
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
using Moq;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests
{
    public class ActivityBuilderShimTests
    {
        private const string ActivityName1 = "MyActivityName/1";
        private const string ActivityName2 = "MyActivityName/2";
        private const string ActivitySourceName = "defaultactivitysource";

        static ActivityBuilderShimTests()
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
            using var activitySource = new ActivitySource(ActivitySourceName);
            Assert.Throws<ArgumentNullException>(() => new ActivityBuilderShim(null, "foo"));
            Assert.Throws<ArgumentNullException>(() => new ActivityBuilderShim(activitySource, null));
        }

        [Fact]
        public void IgnoreActiveSpan()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityBuilderShim(activitySource, "foo");

            // Add a parent. The shim requires that the ISpan implementation be a SpanShim
            shim.AsChildOf(new ActivityShim(activitySource.StartActivity(ActivityName1)));

            // Set to Ignore
            shim.IgnoreActiveSpan();

            // build
            var activityShim = (ActivityShim)shim.Start();

            Assert.Equal("foo", activityShim.activity.OperationName);
        }

        [Fact]
        public void StartWithExplicitTimestamp()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityBuilderShim(activitySource, "foo");

            var startTimestamp = DateTimeOffset.Now;
            shim.WithStartTimestamp(startTimestamp);

            // build
            var activityShim = (ActivityShim)shim.Start();

            Assert.Equal(startTimestamp, activityShim.activity.StartTimeUtc);
        }

        [Fact]
        public void AsChildOf_WithNullSpan()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityBuilderShim(activitySource, "foo");

            // Add a null parent
            shim.AsChildOf((global::OpenTracing.ISpan)null);

            // build
            var activityShim = (ActivityShim)shim.Start();

            Assert.Equal("foo", activityShim.activity.OperationName);
            Assert.Null(activityShim.activity.Parent);
        }

        [Fact]
        public void AsChildOf_WithSpan()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityBuilderShim(activitySource, "foo");

            // Add a parent.
            var span = new ActivityShim(activitySource.StartActivity(ActivityName1));
            shim.AsChildOf(span);

            // build
            var activityShim = (ActivityShim)shim.Start();

            Assert.Equal("foo", activityShim.activity.OperationName);
            Assert.NotNull(activityShim.activity.Parent);
            Assert.Equal(ActivityName1, activityShim.activity.Parent.OperationName);
        }

        [Fact]
        public void Start_ActivityOperationRootSpanChecks()
        {
            // matching root operation name
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityBuilderShim(activitySource, "foo", new List<string> { "foo" });
            var activityShim = (ActivityShim)shim.Start();

            Assert.Equal("foo", activityShim.activity.OperationName);

            // mis-matched root operation name
            shim = new ActivityBuilderShim(activitySource, "foo", new List<string> { "bar" });
            activityShim = (ActivityShim)shim.Start();

            Assert.Equal("foo", activityShim.activity.OperationName);
            Assert.Equal("foo", activityShim.activity.Parent.OperationName);
        }

        [Fact]
        public void AsChildOf_MultipleCallsWithSpan()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityBuilderShim(activitySource, "foo");

            // Multiple calls
            var span1 = new ActivityShim(activitySource.StartActivity(ActivityName1));
            var span2 = new ActivityShim(activitySource.StartActivity(ActivityName2));
            shim.AsChildOf(span1);
            shim.AsChildOf(span2);

            // build
            var activityShim = (ActivityShim)shim.Start();

            Assert.Equal("foo", activityShim.activity.OperationName);
            Assert.Equal(ActivityName2, activityShim.activity.Parent.OperationName);
            Assert.Equal(ActivityName1, activityShim.activity.Parent.Parent.OperationName);
            Assert.Equal(activityShim.Context.TraceId, activityShim.activity.Parent.TraceId.ToHexString());
            Assert.Equal(activityShim.Context.TraceId, activityShim.activity.Parent.Parent.TraceId.ToHexString());
        }

        [Fact]
        public void AsChildOf_WithNullSpanContext()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityBuilderShim(activitySource, "foo");

            // Add a null parent
            shim.AsChildOf((global::OpenTracing.ISpanContext)null);

            // build
            var activityShim = (ActivityShim)shim.Start();

            // should be no parent.
            Assert.Null(activityShim.activity.Parent);
        }

        [Fact]
        public void AsChildOfWithSpanContext()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityBuilderShim(activitySource, "foo");

            // Add a parent
            var spanContext = ActivityContextShimTests.GetSpanContextShim();
            var test = shim.AsChildOf(spanContext);

            // build
            var activityShim = (ActivityShim)shim.Start();

            Assert.NotNull(activityShim.activity.ParentId);
        }

        [Fact]
        public void AsChildOf_MultipleCallsWithSpanContext()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityBuilderShim(activitySource, "foo");

            // Multiple calls
            var spanContext1 = ActivityContextShimTests.GetSpanContextShim();
            var spanContext2 = ActivityContextShimTests.GetSpanContextShim();

            // Add parent context
            shim.AsChildOf(spanContext1);

            // Adds as link as parent context already exists
            shim.AsChildOf(spanContext2);

            // build
            var activityShim = (ActivityShim)shim.Start();
            var linkContext = activityShim.activity.Links.First().Context;

            Assert.Equal("foo", activityShim.activity.OperationName);
            Assert.Contains(spanContext1.TraceId, activityShim.activity.ParentId);
            Assert.Equal(spanContext2.Context, activityShim.activity.Links.First().Context);
        }

        [Fact]
        public void WithTag_KeyIsSpanKindStringValue()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityBuilderShim(activitySource, "foo");

            shim.WithTag(global::OpenTracing.Tag.Tags.SpanKind.Key, global::OpenTracing.Tag.Tags.SpanKindClient);

            // build
            var activityShim = (ActivityShim)shim.Start();

            // Not an attribute
            Assert.Empty(activityShim.activity.Tags);
            Assert.Equal("foo", activityShim.activity.OperationName);
            Assert.Equal(ActivityKind.Client, activityShim.activity.Kind);
        }

        [Fact]
        public void WithTag_KeyIsErrorStringValue()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityBuilderShim(activitySource, "foo");

            shim.WithTag(global::OpenTracing.Tag.Tags.Error.Key, "true");

            // build
            var activityShim = (ActivityShim)shim.Start();

            // Span status should be set
            Assert.Equal(Status.Unknown, activityShim.activity.GetStatus());
        }

        [Fact]
        public void WithTag_KeyIsNullStringValue()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityBuilderShim(activitySource, "foo");

            shim.WithTag((string)null, "unused");

            // build
            var activityShim = (ActivityShim)shim.Start();

            // Null key was ignored
            Assert.Empty(activityShim.activity.Tags);
        }

        [Fact]
        public void WithTag_ValueIsNullStringValue()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityBuilderShim(activitySource, "foo");

            shim.WithTag("foo", null);

            // build
            var activityShim = (ActivityShim)shim.Start();

            // Null value was turned into string.empty
            Assert.Equal("foo", activityShim.activity.Tags.First().Key);
            Assert.Equal(string.Empty, activityShim.activity.Tags.First().Value);
        }

        [Fact]
        public void WithTag_KeyIsErrorBoolValue()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityBuilderShim(activitySource, "foo");

            shim.WithTag(global::OpenTracing.Tag.Tags.Error.Key, true);

            // build
            var activityShim = (ActivityShim)shim.Start();

            // Span status should be set
            Assert.Equal(Status.Unknown, activityShim.activity.GetStatus());
        }

        [Fact]
        public void WithTag_VariousValueTypes()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityBuilderShim(activitySource, "foo");

            shim.WithTag("foo", "unused");
            shim.WithTag("bar", false);
            shim.WithTag("baz", 1);
            shim.WithTag("bizzle", 1D);
            shim.WithTag(new global::OpenTracing.Tag.BooleanTag("shnizzle"), true);
            shim.WithTag(new global::OpenTracing.Tag.IntOrStringTag("febrizzle"), "unused");
            shim.WithTag(new global::OpenTracing.Tag.StringTag("mobizzle"), "unused");

            // build
            var activityShim = (ActivityShim)shim.Start();

            // Just verify the count
            Assert.Equal(7, activityShim.activity.Tags.Count());
        }

        [Fact]
        public void Start()
        {
            using var activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ActivityBuilderShim(activitySource, "foo");

            // build
            var span = shim.Start() as ActivityShim;

            // Just check the return value is a SpanShim and that the underlying OpenTelemetry Span.
            // There is nothing left to verify because the rest of the tests were already calling .Start() prior to verification.
            Assert.NotNull(span);
            Assert.Equal("foo", span.activity.OperationName);
        }
    }
}
