// <copyright file="SpanTest.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Trace.Configuration;

namespace OpenTelemetry.Trace.Test
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;
    using Moq;
    using OpenTelemetry.Abstractions.Utils;
    using OpenTelemetry.Tests;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;
    using Xunit;

    public class SpanTest : IDisposable
    {
        private const string SpanName = "MySpanName";
        private const string EventDescription = "MyEvent";

        private readonly IDictionary<string, object> attributes = new Dictionary<string, object>();
        private readonly List<KeyValuePair<string, object>> expectedAttributes;
        private readonly Mock<SpanProcessor> spanProcessorMock = new Mock<SpanProcessor>(new NoopSpanExporter());
        private readonly SpanProcessor spanProcessor;
        private readonly TracerFactory tracerFactory;

        public SpanTest()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            spanProcessor = spanProcessorMock.Object;
            tracerFactory = TracerFactory.Create(b => b.SetProcessor(e => spanProcessor));
            attributes.Add("MyStringAttributeKey", "MyStringAttributeValue");
            attributes.Add("MyLongAttributeKey", 123L);
            attributes.Add("MyBooleanAttributeKey", false);
            expectedAttributes = new List<KeyValuePair<string, object>>(attributes)
            {
                new KeyValuePair<string, object>("MySingleStringAttributeKey", "MySingleStringAttributeValue"),
            };
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentSpan()
        {
            var tracer = (Tracer)tracerFactory.GetTracer("foo", "semver:1.0.0");

            var traceState = new List<KeyValuePair<string, string>> {new KeyValuePair<string, string>("k1", "v1")};
            var grandParentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, traceState);
            var parentSpan = (Span)tracer.StartSpan(SpanName, grandParentContext);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartSpan(SpanName, parentSpan);

            Assert.True(span.Context.IsValid);
            Assert.Equal(parentSpan.Context.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(parentSpan.Context.SpanId, span.ParentSpanId);
            Assert.Equal(parentSpan.Context.TraceOptions, span.Context.TraceOptions);
            Assert.Same(traceState, span.Context.Tracestate);

            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Internal, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentSpan_IgnoresCurrentParent()
        {
            var tracer = tracerFactory.GetTracer(null);

            tracer.WithSpan(tracer.StartRootSpan("outer"));
            {
                var parentSpan = (Span)tracer.StartRootSpan(SpanName);

                var span = (Span)tracer.StartSpan(SpanName, parentSpan);

                Assert.True(span.Context.IsValid);
                Assert.Equal(parentSpan.Context.TraceId, span.Context.TraceId);
                Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
                Assert.Equal(parentSpan.Context.SpanId, span.ParentSpanId);
                Assert.Equal(parentSpan.Context.TraceOptions, span.Context.TraceOptions);
            }
        }

        [Fact]
        public void StartSpanFrom_NotRecorded_ParentSpan()
        {
            var tracer = tracerFactory.GetTracer(null);

            var grandParentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);
            var parentSpan = (Span)tracer.StartSpan(SpanName, grandParentContext);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartSpan(SpanName, parentSpan);

            Assert.True(span.Context.IsValid);
            Assert.Equal(parentSpan.Context.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(parentSpan.Context.SpanId, span.ParentSpanId);
            Assert.Equal(parentSpan.Context.TraceOptions, span.Context.TraceOptions);
            Assert.Empty(span.Context.Tracestate);

            Assert.False(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Internal, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentSpan_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentSpan = tracer.StartRootSpan(SpanName);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartSpan(SpanName, parentSpan, SpanKind.Client);

            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Client, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentSpan_Kind_Timestamp()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentSpan = tracer.StartRootSpan(SpanName);

            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            var span = (Span)tracer.StartSpan(SpanName, parentSpan, SpanKind.Server, startTimestamp);

            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentSpan_Kind_Links()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentSpan = tracer.StartRootSpan(SpanName);
            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartSpan(SpanName, parentSpan, SpanKind.Server, default, new [] { new Link(linkContext) });

            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Server, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Single(span.Links);
            Assert.Same(linkContext, span.Links.Single().Context);
        }

        [Fact]
        public void StartRootSpan_IgnoresCurrentParent()
        {
            var tracer = tracerFactory.GetTracer(null);

            tracer.WithSpan(tracer.StartRootSpan("outer"));
            {
                var span = (Span)tracer.StartRootSpan(SpanName);

                Assert.True(span.Context.IsValid);
                Assert.Equal(span.Activity.TraceId, span.Context.TraceId);
                Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
                Assert.Equal(default, span.ParentSpanId);
                Assert.Equal(span.Activity.ActivityTraceFlags, span.Context.TraceOptions);
            }
        }

        [Fact]
        public void StartRootSpan_IgnoresCurrentActivity()
        {
            var tracer = tracerFactory.GetTracer(null);

            var outerActivity = new Activity("outer").SetIdFormat(ActivityIdFormat.W3C).Start();

            var span = (Span)tracer.StartRootSpan(SpanName);

            Assert.True(span.Context.IsValid);
            Assert.Equal(span.Activity.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(default, span.ParentSpanId);
            Assert.Equal(span.Activity.ActivityTraceFlags, span.Context.TraceOptions);

            outerActivity.Stop();
        }

        [Fact]
        public void StartRootSpan()
        {
            var tracer = tracerFactory.GetTracer(null);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartRootSpan(SpanName);

            Assert.True(span.Context.IsValid);
            Assert.Equal(span.Activity.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(default, span.ParentSpanId);
            Assert.Equal(span.Activity.ActivityTraceFlags, span.Context.TraceOptions);
            Assert.Empty(span.Context.Tracestate);

            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Internal, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartRootSpanFrom_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartRootSpan(SpanName, SpanKind.Consumer);

            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Consumer, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartRootSpanFrom_Kind_Timestamp()
        {
            var tracer = tracerFactory.GetTracer(null);

            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            var span = (Span)tracer.StartRootSpan(SpanName, SpanKind.Client, startTimestamp);

            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Client, span.Kind);
            Assert.Equal(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartRootSpanFrom_Kind_Timestamp_Links()
        {
            var tracer = tracerFactory.GetTracer(null);

            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartRootSpan(SpanName, SpanKind.Server, startTimestamp, new[] { new Link(linkContext) });

            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(startTimestamp, span.StartTimestamp);
            Assert.Single(span.Links);
            Assert.Same(linkContext, span.Links.Single().Context);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentContext()
        {
            var tracer = tracerFactory.GetTracer(null);

            var traceState = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("k1", "v1") };
            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, traceState);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartSpan(SpanName, parentContext);

            Assert.True(span.Context.IsValid);
            Assert.Equal(parentContext.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(parentContext.SpanId, span.ParentSpanId);
            Assert.Equal(parentContext.TraceOptions, span.Context.TraceOptions);
            Assert.Same(traceState, span.Context.Tracestate);

            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Internal, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartSpanFrom_InvalidContext()
        {
            var tracer = tracerFactory.GetTracer(null);
            var parentContext = SpanContext.Blank;

            var span = (Span)tracer.StartSpan(SpanName, parentContext);

            Assert.True(span.Context.IsValid);
            Assert.NotEqual(default, span.Context.TraceId);
            Assert.NotEqual(default, span.Context.SpanId);
            Assert.Equal(default, span.ParentSpanId);

            // always sample sampler
            Assert.Equal(ActivityTraceFlags.Recorded, span.Context.TraceOptions);
            Assert.True(span.IsRecordingEvents);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentContext_IgnoresCurrentParent()
        {
            var tracer = tracerFactory.GetTracer(null);

            tracer.WithSpan(tracer.StartRootSpan("outer"));
            {
                var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.Recorded);

                var span = (Span)tracer.StartSpan(SpanName, parentContext);

                Assert.True(span.Context.IsValid);
                Assert.Equal(parentContext.TraceId, span.Context.TraceId);
                Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
                Assert.Equal(parentContext.SpanId, span.ParentSpanId);
                Assert.Equal(parentContext.TraceOptions, span.Context.TraceOptions);
            }
        }

        [Fact]
        public void StartSpanFrom_NotRecorded_ParentContext()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartSpan(SpanName, parentContext);

            Assert.True(span.Context.IsValid);
            Assert.Equal(parentContext.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(parentContext.SpanId, span.ParentSpanId);
            Assert.Equal(parentContext.TraceOptions, span.Context.TraceOptions);
            Assert.Empty(span.Context.Tracestate);

            Assert.False(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Internal, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentContext_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartSpan(SpanName, parentContext, SpanKind.Client);

            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Client, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentContext_Kind_Timestamp()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            var span = (Span)tracer.StartSpan(SpanName, parentContext, SpanKind.Server, startTimestamp);

            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentContext_Kind_Links()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartSpan(SpanName, parentContext, SpanKind.Server, default, new[] { new Link(linkContext) });

            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Server, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Single(span.Links);
            Assert.Same(linkContext, span.Links.Single().Context);
        }

        [Fact]
        public void StartSpan_Recorded_FromActivity()
        {
            var tracer = tracerFactory.GetTracer(null);

            var activity = new Activity(SpanName).SetIdFormat(ActivityIdFormat.W3C).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            activity.TraceStateString = "k1=v1,k2=v2";

            var span = (Span)tracer.StartSpanFromActivity(SpanName, activity);

            Assert.Equal(activity.TraceId, span.Context.TraceId);
            Assert.Equal(activity.SpanId, span.Context.SpanId);
            Assert.Equal(activity.ParentSpanId, span.ParentSpanId);
            Assert.Equal(activity.ActivityTraceFlags, span.Context.TraceOptions);
            Assert.Equal(2, span.Context.Tracestate.Count());
            Assert.Contains(span.Context.Tracestate, pair => pair.Key == "k1" && pair.Value == "v1");
            Assert.Contains(span.Context.Tracestate, pair => pair.Key == "k2" && pair.Value == "v2");

            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Internal, span.Kind);
            Assert.Equal(activity.StartTimeUtc, span.StartTimestamp.DateTime);
            Assert.Equal(activity, span.Activity);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartSpan_Recorded_FromActivity_IgnoresCurrentParent()
        {
            var tracer = tracerFactory.GetTracer(null);
            tracer.WithSpan(tracer.StartRootSpan("outer"));
            {
                var activity = new Activity(SpanName).SetIdFormat(ActivityIdFormat.W3C).SetParentId(" ").Start();
                activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

                var span = (Span)tracer.StartSpanFromActivity(SpanName, activity);

                Assert.Equal(activity.TraceId, span.Context.TraceId);
                Assert.Equal(activity.SpanId, span.Context.SpanId);
                Assert.Equal(activity.ParentSpanId, span.ParentSpanId);
                Assert.Equal(activity.ActivityTraceFlags, span.Context.TraceOptions);
            }
        }

        [Fact]
        public void StartSpan_NotRecorded_FromActivity()
        {
            var tracer = tracerFactory.GetTracer(null);

            var activity = new Activity(SpanName).SetIdFormat(ActivityIdFormat.W3C).Start();
            activity.ActivityTraceFlags = ActivityTraceFlags.None;
            activity.TraceStateString = "k1=v1,k2=v2";

            var span = (Span)tracer.StartSpanFromActivity(SpanName, activity);

            Assert.Equal(activity.TraceId, span.Context.TraceId);
            Assert.Equal(activity.SpanId, span.Context.SpanId);
            Assert.Equal(activity.ParentSpanId, span.ParentSpanId);
            Assert.Equal(activity.ActivityTraceFlags, span.Context.TraceOptions);
            Assert.Equal(2, span.Context.Tracestate.Count());
            Assert.Contains(span.Context.Tracestate, pair => pair.Key == "k1" && pair.Value == "v1");
            Assert.Contains(span.Context.Tracestate, pair => pair.Key == "k2" && pair.Value == "v2");

            // activity is not a parent and sampling decision is made 
            // based on sampler alone
            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Internal, span.Kind);
            Assert.Equal(activity.StartTimeUtc, span.StartTimestamp.DateTime);
            Assert.Equal(activity, span.Activity);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartSpan_Recorded_FromActivity_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            var activity = new Activity(SpanName).SetIdFormat(ActivityIdFormat.W3C).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var span = (Span)tracer.StartSpanFromActivity(SpanName, activity, SpanKind.Client);

            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Client, span.Kind);
            Assert.Equal(activity.StartTimeUtc, span.StartTimestamp.DateTime);
            Assert.Equal(activity, span.Activity);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartSpan_Recorded_FromActivity_Kind_Links()
        {
            var tracer = tracerFactory.GetTracer(null);

            var activity = new Activity(SpanName).SetIdFormat(ActivityIdFormat.W3C).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var span = (Span)tracer.StartSpanFromActivity(SpanName, activity, SpanKind.Server, new[] { new Link(linkContext) });

            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(activity.StartTimeUtc, span.StartTimestamp.DateTime);
            Assert.Equal(activity, span.Activity);
            Assert.Single(span.Links);
            Assert.Same(linkContext, span.Links.Single().Context);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ImplicitParentSpan()
        {
            var tracer = tracerFactory.GetTracer(null);
            var traceState = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("k1", "v1") };
            var grandParentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, traceState);
            using (tracer.WithSpan(tracer.StartSpan(SpanName, grandParentContext)))
            {
                var parentSpan = tracer.CurrentSpan;

                var startTimestamp = PreciseTimestamp.GetUtcNow();
                var span = (Span)tracer.StartSpan(SpanName);

                Assert.True(span.Context.IsValid);
                Assert.Equal(parentSpan.Context.TraceId, span.Context.TraceId);
                Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
                Assert.Equal(parentSpan.Context.SpanId, span.ParentSpanId);
                Assert.Equal(parentSpan.Context.TraceOptions, span.Context.TraceOptions);
                Assert.Same(traceState, span.Context.Tracestate);

                Assert.True(span.IsRecordingEvents);
                Assert.Equal(SpanKind.Internal, span.Kind);
                AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
                Assert.Empty(span.Links);
            }
        }

        [Fact]
        public void StartSpanFrom_Recorded_ImplicitParentActivity()
        {
            var tracer = tracerFactory.GetTracer(null);
            
            var parentActivity = new Activity("foo").SetIdFormat(ActivityIdFormat.W3C).Start();
            parentActivity.TraceStateString = "k1=v1,k2=v2";
            parentActivity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartSpan(SpanName);

            Assert.True(span.Context.IsValid);
            Assert.Equal(parentActivity.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(parentActivity.SpanId, span.ParentSpanId);
            Assert.Equal(parentActivity.ActivityTraceFlags, span.Context.TraceOptions);
            Assert.Equal(2, span.Context.Tracestate.Count());
            Assert.Contains(span.Context.Tracestate, pair => pair.Key == "k1" && pair.Value == "v1");
            Assert.Contains(span.Context.Tracestate, pair => pair.Key == "k2" && pair.Value == "v2");

            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Internal, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
            
            parentActivity.Stop();
        }

        [Fact]
        public void StartSpanFrom_NotRecorded_ImplicitParentActivity()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentActivity = new Activity("foo").SetIdFormat(ActivityIdFormat.W3C).Start();
            parentActivity.TraceStateString = "k1=v1,k2=v2";
            parentActivity.ActivityTraceFlags = ActivityTraceFlags.None;

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartSpan(SpanName);

            Assert.True(span.Context.IsValid);
            Assert.Equal(parentActivity.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(parentActivity.SpanId, span.ParentSpanId);
            Assert.Equal(parentActivity.ActivityTraceFlags, span.Context.TraceOptions);
            Assert.Equal(2, span.Context.Tracestate.Count());
            Assert.Contains(span.Context.Tracestate, pair => pair.Key == "k1" && pair.Value == "v1");
            Assert.Contains(span.Context.Tracestate, pair => pair.Key == "k2" && pair.Value == "v2");

            Assert.False(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Internal, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);

            parentActivity.Stop();
        }

        [Fact]
        public void StartSpanFrom_NotRecorded_ImplicitParentSpan()
        {
            var tracer = tracerFactory.GetTracer(null);
            var grandParentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);
            using (tracer.WithSpan(tracer.StartSpan(SpanName, grandParentContext)))
            {
                var parentSpan = tracer.CurrentSpan;

                var startTimestamp = PreciseTimestamp.GetUtcNow();
                var span = (Span)tracer.StartSpan(SpanName);

                Assert.True(span.Context.IsValid);
                Assert.Equal(parentSpan.Context.TraceId, span.Context.TraceId);
                Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
                Assert.Equal(parentSpan.Context.SpanId, span.ParentSpanId);
                Assert.Equal(parentSpan.Context.TraceOptions, span.Context.TraceOptions);
                Assert.Empty(span.Context.Tracestate);

                Assert.False(span.IsRecordingEvents);
                Assert.Equal(SpanKind.Internal, span.Kind);
                AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
                Assert.Empty(span.Links);
            }
        }

        [Fact]
        public void StartSpanFrom_Recorded_ImplicitParentSpan_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            using (tracer.WithSpan(tracer.StartRootSpan(SpanName)))
            {
                var startTimestamp = PreciseTimestamp.GetUtcNow();
                var span = (Span)tracer.StartSpan(SpanName, SpanKind.Producer);

                Assert.True(span.IsRecordingEvents);
                Assert.Equal(SpanKind.Producer, span.Kind);
                AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
                Assert.Empty(span.Links);
            }
        }

        [Fact]
        public void StartSpanFrom_Recorded_ImplicitParentSpan_Kind_Timestamp()
        {
            var tracer = tracerFactory.GetTracer(null);

            using (tracer.WithSpan(tracer.StartRootSpan(SpanName)))
            {
                var startTimestamp = DateTimeOffset.UtcNow.AddSeconds(-10);
                var span = (Span)tracer.StartSpan(SpanName, SpanKind.Server, startTimestamp);

                Assert.True(span.IsRecordingEvents);
                Assert.Equal(SpanKind.Server, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Empty(span.Links);
            }
        }

        [Fact]
        public void StartSpanFrom_Recorded_ImplicitParentSpan_Kind_Timestamp_Links()
        {
            var tracer = tracerFactory.GetTracer(null);

            using (tracer.WithSpan(tracer.StartRootSpan(SpanName)))
            {
                var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.Recorded);

                var startTimestamp = DateTimeOffset.UtcNow.AddSeconds(-10);
                var span = (Span)tracer.StartSpan(SpanName, SpanKind.Server, startTimestamp, new[] {new Link(linkContext)});

                Assert.True(span.IsRecordingEvents);
                Assert.Equal(SpanKind.Server, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Single(span.Links);
                Assert.Same(linkContext, span.Links.Single().Context);
            }
        }

        [Fact]
        public void EndSpan_EventsNotRecorded()
        {
            var link = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var tracer = tracerFactory.GetTracer(null);

            var span = (Span)tracer.StartRootSpan(SpanName, SpanKind.Client);

            var spanEndTime = DateTimeOffset.UtcNow.AddSeconds(10);
            span.End(spanEndTime);

            // Check that adding trace events after Span#End() does not throw any exception and are not
            // recorded.
            foreach (var attribute in attributes)
            {
                span.SetAttribute(attribute);
            }

            span.SetAttribute(
                "MySingleStringAttributeKey",
                "MySingleStringAttributeValue");

            span.AddEvent(new Event(EventDescription));
            span.AddEvent(EventDescription, attributes);
            span.AddLink(new Link(link));

            Assert.NotEqual(default, span.StartTimestamp);
            Assert.Empty(span.Attributes);
            Assert.Empty(span.Events);
            Assert.Empty(span.Links);
            Assert.Equal(Status.Ok, span.Status);
            Assert.Equal(spanEndTime, span.EndTimestamp);
        }

        [Fact]
        public async Task StartSpan_PropertiesAccessible()
        {
            var contextLink = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None);

            var tracer = tracerFactory.GetTracer(null);
            var startTime = DateTimeOffset.UtcNow.AddSeconds(-1);
            var span = (Span)tracer.StartRootSpan(SpanName, SpanKind.Client, startTime, null);

            span.SetAttribute(
                "MySingleStringAttributeKey",
                "MySingleStringAttributeValue");
            foreach (var attribute in attributes)
            {
                span.SetAttribute(attribute);
            }

            var firstEventTime = PreciseTimestamp.GetUtcNow();
            span.AddEvent(new Event(EventDescription, firstEventTime));
            await Task.Delay(TimeSpan.FromMilliseconds(100));

            var secondEventTime = PreciseTimestamp.GetUtcNow();
            span.AddEvent(EventDescription, attributes);

            var link = new Link(contextLink);
            span.AddLink(link);

            Assert.Equal(span.Activity.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(span.Activity.ParentSpanId, span.ParentSpanId);
            Assert.Equal(span.Activity.ActivityTraceFlags, span.Context.TraceOptions);
            Assert.Empty(span.Context.Tracestate);

            Assert.Equal(SpanName, span.Name);
            Assert.Equal(span.Activity.ParentSpanId, span.ParentSpanId);

            span.Attributes.AssertAreSame(expectedAttributes);

            Assert.Equal(2, span.Events.Count());
            Assert.Equal(firstEventTime, span.Events.ToList()[0].Timestamp);
            AssertApproxSameTimestamp(span.Events.ToList()[1].Timestamp, secondEventTime);

            Assert.Equal(new Event(EventDescription, firstEventTime), span.Events.ToList()[0]);

            Assert.Equal(EventDescription, span.Events.ToList()[1].Name);
            Assert.Equal(attributes, span.Events.ToList()[1].Attributes);

            Assert.Single(span.Links);
            Assert.Equal(link, span.Links.First());

            Assert.Equal(startTime, span.StartTimestamp);

            Assert.True(span.Status.IsValid);
            Assert.Equal(default, span.EndTimestamp);

            var startEndMock = Mock.Get(spanProcessor);

            spanProcessorMock.Verify(s => s.OnStart(span), Times.Once);
            startEndMock.Verify(s => s.OnEnd(span), Times.Never);
        }

        [Fact]
        public async Task EndSpan_Properties()
        {
            var contextLink = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None);

            var tracer = tracerFactory.GetTracer(null);
            var startTime = DateTimeOffset.UtcNow.AddSeconds(-1);
            var span = (Span)tracer.StartRootSpan(SpanName, SpanKind.Client, startTime, null);
            span.SetAttribute(
                "MySingleStringAttributeKey",
                "MySingleStringAttributeValue");
            foreach (var attribute in attributes)
            {
                span.SetAttribute(attribute);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
            var firstEventTime = PreciseTimestamp.GetUtcNow();
            span.AddEvent(new Event(EventDescription, firstEventTime));

            await Task.Delay(TimeSpan.FromMilliseconds(100));
            var secondEventTime = PreciseTimestamp.GetUtcNow();
            span.AddEvent(EventDescription, attributes);

            var link = new Link(contextLink);
            span.AddLink(link);
            span.Status = Status.Cancelled;

            var spanEndTime = PreciseTimestamp.GetUtcNow();
            span.End();

            Assert.Equal(span.Activity.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(span.Activity.ParentSpanId, span.ParentSpanId);
            Assert.Equal(span.Activity.ActivityTraceFlags, span.Context.TraceOptions);

            Assert.Equal(SpanName, span.Name);
            Assert.Equal(span.Activity.ParentSpanId, span.ParentSpanId);

            span.Attributes.AssertAreSame(expectedAttributes);
            Assert.Equal(2, span.Events.Count());

            Assert.Equal(firstEventTime, span.Events.ToList()[0].Timestamp);
            AssertApproxSameTimestamp(span.Events.ToList()[1].Timestamp, secondEventTime);

            Assert.Equal(new Event(EventDescription, firstEventTime), span.Events.ToList()[0]);
            Assert.Single(span.Links);
            Assert.Equal(link, span.Links.First());
            Assert.Equal(startTime, span.StartTimestamp);
            Assert.Equal(Status.Cancelled, span.Status);
            AssertApproxSameTimestamp(spanEndTime, span.EndTimestamp);

            spanProcessorMock.Verify(s => s.OnStart(span), Times.Once);
            spanProcessorMock.Verify(s => s.OnEnd(span), Times.Once);
        }

        [Fact]
        public void SetStatus()
        {
            var tracer = tracerFactory.GetTracer(null);
            var span = (Span)tracer.StartRootSpan(SpanName);

            Assert.Equal(Status.Ok, span.Status);
            span.Status = Status.Cancelled;
            Assert.Equal(Status.Cancelled, span.Status);
            span.End();
            Assert.Equal(Status.Cancelled, span.Status);

            spanProcessorMock.Verify(s => s.OnStart(span), Times.Once);
            spanProcessorMock.Verify(s => s.OnEnd(span), Times.Once);
        }

        [Fact]
        public void BadArguments()
        {
            var tracer = tracerFactory.GetTracer(null);
            var span = (Span)tracer.StartRootSpan(SpanName);

            Assert.Throws<ArgumentException>(() => span.Status = new Status());
            Assert.Throws<ArgumentNullException>(() => span.UpdateName(null));
            Assert.Throws<ArgumentNullException>(() => span.SetAttribute(null, string.Empty));
            Assert.Throws<ArgumentNullException>(() => span.SetAttribute(string.Empty, null));
            Assert.Throws<ArgumentNullException>(() =>
                span.SetAttribute(null, "foo"));
            Assert.Throws<ArgumentNullException>(() => span.SetAttribute(null, 1L));
            Assert.Throws<ArgumentNullException>(() => span.SetAttribute(null, 0.1d));
            Assert.Throws<ArgumentNullException>(() => span.SetAttribute(null, true));
            Assert.Throws<ArgumentNullException>(() => span.AddEvent((string)null));
            Assert.Throws<ArgumentNullException>(() => span.AddEvent((Event)null));
            Assert.Throws<ArgumentNullException>(() => span.AddLink(null));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EndSpanStopsActivity(bool recordEvents)
        {
            var parentActivity = new Activity("parent").Start();

            var tracer = tracerFactory
                .GetTracer(null);
            var span = (Span)tracer.StartSpan(SpanName);
            span.End();

            Assert.Same(parentActivity, Activity.Current);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EndSpanDoesNotStopActivityWhenDoesNotOwnIt(bool recordEvents)
        {
            var activity = new Activity("foo").Start();
            if (recordEvents)
            {
                activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            }

            var tracer = tracerFactory
                .GetTracer(null);
            var span = (Span)tracer.StartSpanFromActivity(SpanName, activity);
            span.End();

            Assert.Same(activity, Activity.Current);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void EndSpanStopActivity_NotCurrentActivity(bool recordEvents, bool ownsActivity)
        {
            var activity = new Activity(SpanName).Start();
            if (recordEvents)
            {
                activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            }
            var tracer = tracerFactory.GetTracer(null);

            Span span;
            if (ownsActivity)
            {
                span = (Span)tracer.StartSpanFromActivity(SpanName, activity);
            }
            else
            {
                span = (Span)tracer.StartSpan(SpanName);
            }

            var anotherActivity = new Activity(SpanName).Start();
            span.End();

            Assert.Same(anotherActivity, Activity.Current);
        }

        private void AssertApproxSameTimestamp(DateTimeOffset one, DateTimeOffset two)
        {
            var timeShift = Math.Abs((one - two).TotalMilliseconds);
            Assert.InRange(timeShift, 0, 20);
        }

        public void Dispose()
        {
            Activity.Current = null;
        }
    }
}
