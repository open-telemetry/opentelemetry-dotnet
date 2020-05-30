// <copyright file="SpanTest.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Trace.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using OpenTelemetry.Api.Utils;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace.Export;
using OpenTelemetry.Trace.Samplers;
using Xunit;

namespace OpenTelemetry.Trace.Test
{
    public class SpanTest : IDisposable
    {
        private const string SpanName = "MySpanName";
        private const string EventDescription = "MyEvent";

        private readonly IDictionary<string, object> attributes = new Dictionary<string, object>();
        private readonly List<KeyValuePair<string, object>> expectedAttributes;
        private readonly Mock<SpanProcessor> spanProcessorMock = new Mock<SpanProcessor>();
        private readonly SpanProcessor spanProcessor;
        private readonly TracerFactory tracerFactory;

        public SpanTest()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            spanProcessor = spanProcessorMock.Object;
            tracerFactory = TracerFactory.Create(b => b.AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor)));
            attributes.Add("MyStringAttributeKey", "MyStringAttributeValue");
            attributes.Add("MyLongAttributeKey", 123L);
            attributes.Add("MyBooleanAttributeKey", false);
            attributes.Add("MyDoubleAttributeKey", 0.1d);
            expectedAttributes = new List<KeyValuePair<string, object>>(attributes)
            {
                new KeyValuePair<string, object>("MySingleStringAttributeKey", "MySingleStringAttributeValue"),
            };
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentSpan()
        {
            var tracer = (TracerSdk)tracerFactory.GetTracer("foo", "semver:1.0.0");

            var traceState = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("k1", "v1") };
            var grandParentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, false, traceState);
            var parentSpan = (SpanSdk)tracer.StartSpan(SpanName, grandParentContext);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (SpanSdk)tracer.StartSpan(SpanName, parentSpan);

            Assert.True(span.Context.IsValid);
            Assert.Equal(parentSpan.Context.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(parentSpan.Context.SpanId, span.ParentSpanId);
            Assert.Equal(parentSpan.Context.TraceFlags, span.Context.TraceFlags);
            Assert.Same(traceState, span.Context.Tracestate);

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Internal, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Null(span.Links);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentSpan_IgnoresCurrentParent()
        {
            var tracer = tracerFactory.GetTracer(null);

            using (tracer.StartActiveSpan("outer", out _))
            {
                var parentSpan = (SpanSdk)tracer.StartRootSpan(SpanName);

                var span = (SpanSdk)tracer.StartSpan(SpanName, parentSpan);

                Assert.True(span.Context.IsValid);
                Assert.Equal(parentSpan.Context.TraceId, span.Context.TraceId);
                Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
                Assert.Equal(parentSpan.Context.SpanId, span.ParentSpanId);
                Assert.Equal(parentSpan.Context.TraceFlags, span.Context.TraceFlags);
            }
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentSpan_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentSpan = tracer.StartRootSpan(SpanName);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (SpanSdk)tracer.StartSpan(SpanName, parentSpan, SpanKind.Client);

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Client, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Null(span.Links);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentSpan_Kind_Timestamp()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentSpan = tracer.StartRootSpan(SpanName);

            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            var span = (SpanSdk)tracer.StartSpan(SpanName, parentSpan, SpanKind.Server, new SpanCreationOptions { StartTimestamp = startTimestamp });

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(startTimestamp, span.StartTimestamp);
            Assert.Null(span.Links);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentSpan_Kind_Links_Func()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentSpan = tracer.StartRootSpan(SpanName);
            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (SpanSdk)tracer.StartSpan(SpanName, parentSpan, SpanKind.Server, new SpanCreationOptions { LinksFactory = () => new[] { new Link(linkContext) } });

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Server, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Single(span.Links);
            Assert.Equal(linkContext, span.Links.Single().Context);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentSpan_Kind_Links_Enumerable()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentSpan = tracer.StartRootSpan(SpanName);
            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (SpanSdk)tracer.StartSpan(SpanName, parentSpan, SpanKind.Server, new SpanCreationOptions { Links = new[] { new Link(linkContext) } });

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Server, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Single(span.Links);
            Assert.Equal(linkContext, span.Links.Single().Context);
        }

        [Fact]
        public void StartRootSpan_IgnoresCurrentParent()
        {
            var tracer = tracerFactory.GetTracer(null);

            tracer.WithSpan(tracer.StartRootSpan("outer"));
            {
                var span = (SpanSdk)tracer.StartRootSpan(SpanName);

                Assert.True(span.Context.IsValid);
                Assert.Equal(span.Activity.TraceId, span.Context.TraceId);
                Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
                Assert.Equal(default, span.ParentSpanId);
                Assert.Equal(span.Activity.ActivityTraceFlags, span.Context.TraceFlags);
            }
        }

        [Fact]
        public void StartRootSpan_IgnoresCurrentActivity()
        {
            var tracer = tracerFactory.GetTracer(null);

            var outerActivity = new Activity("outer").SetIdFormat(ActivityIdFormat.W3C).Start();

            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            Assert.True(span.Context.IsValid);
            Assert.Equal(span.Activity.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(default, span.ParentSpanId);
            Assert.Equal(span.Activity.ActivityTraceFlags, span.Context.TraceFlags);

            outerActivity.Stop();
        }

        [Fact]
        public void StartRootSpan()
        {
            var tracer = tracerFactory.GetTracer(null);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            Assert.True(span.Context.IsValid);
            Assert.Equal(span.Activity.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(default, span.ParentSpanId);
            Assert.Equal(span.Activity.ActivityTraceFlags, span.Context.TraceFlags);
            Assert.Empty(span.Context.Tracestate);

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Internal, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Null(span.Links);
        }

        [Fact]
        public void StartRootSpanFrom_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (SpanSdk)tracer.StartRootSpan(SpanName, SpanKind.Consumer);

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Consumer, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Null(span.Links);
        }

        [Fact]
        public void StartRootSpanFrom_Kind_Timestamp()
        {
            var tracer = tracerFactory.GetTracer(null);

            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            var span = (SpanSdk)tracer.StartRootSpan(SpanName, SpanKind.Client, new SpanCreationOptions { StartTimestamp = startTimestamp });

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Client, span.Kind);
            Assert.Equal(startTimestamp, span.StartTimestamp);
            Assert.Null(span.Links);
        }

        [Fact]
        public void StartRootSpanFrom_Kind_Timestamp_Links()
        {
            var tracer = tracerFactory.GetTracer(null);

            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (SpanSdk)tracer.StartRootSpan(SpanName, SpanKind.Server, new SpanCreationOptions
            {
                StartTimestamp = startTimestamp,
                Links = new[] { new Link(linkContext) },
            });

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(startTimestamp, span.StartTimestamp);
            Assert.Single(span.Links);
            Assert.Equal(linkContext, span.Links.Single().Context);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentContext()
        {
            var tracer = tracerFactory.GetTracer(null);

            var traceState = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("k1", "v1") };
            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, false, traceState);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (SpanSdk)tracer.StartSpan(SpanName, parentContext);

            Assert.True(span.Context.IsValid);
            Assert.Equal(parentContext.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(parentContext.SpanId, span.ParentSpanId);
            Assert.Equal(parentContext.TraceFlags, span.Context.TraceFlags);
            Assert.Same(traceState, span.Context.Tracestate);

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Internal, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Null(span.Links);
        }

        [Fact]
        public void StartSpanFrom_InvalidContext()
        {
            var tracer = tracerFactory.GetTracer(null);
            var parentContext = default(SpanContext);

            var span = (SpanSdk)tracer.StartSpan(SpanName, parentContext);

            Assert.True(span.Context.IsValid);
            Assert.NotEqual(default, span.Context.TraceId);
            Assert.NotEqual(default, span.Context.SpanId);
            Assert.Equal(default, span.ParentSpanId);

            // always sample sampler
            Assert.Equal(ActivityTraceFlags.Recorded, span.Context.TraceFlags);
            Assert.True(span.IsRecording);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentContext_IgnoresCurrentParent()
        {
            var tracer = tracerFactory.GetTracer(null);

            tracer.WithSpan(tracer.StartRootSpan("outer"));
            {
                var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.Recorded);

                var span = (SpanSdk)tracer.StartSpan(SpanName, parentContext);

                Assert.True(span.Context.IsValid);
                Assert.Equal(parentContext.TraceId, span.Context.TraceId);
                Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
                Assert.Equal(parentContext.SpanId, span.ParentSpanId);
                Assert.Equal(parentContext.TraceFlags, span.Context.TraceFlags);
            }
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentContext_Kind_Timestamp()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            var span = (SpanSdk)tracer.StartSpan(SpanName, parentContext, SpanKind.Server, new SpanCreationOptions { StartTimestamp = startTimestamp });

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(startTimestamp, span.StartTimestamp);
            Assert.Null(span.Links);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentContext_Kind_Links_Func()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (SpanSdk)tracer.StartSpan(SpanName, parentContext, SpanKind.Server, new SpanCreationOptions { LinksFactory = () => new[] { new Link(linkContext) } });

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Server, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Single(span.Links);
            Assert.Equal(linkContext, span.Links.Single().Context);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentContext_Kind_Links_Enumerable()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var firstLinkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var secondLinkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (SpanSdk)tracer.StartSpan(SpanName, parentContext, SpanKind.Server, new SpanCreationOptions { Links = new[] { new Link(firstLinkContext), new Link(secondLinkContext) } });

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Server, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Equal(2, span.Links.Count());
            Assert.Equal(firstLinkContext, span.Links.First().Context);
            Assert.Equal(secondLinkContext, span.Links.Last().Context);
        }

        [Fact]
        public void StartSpan_Recorded_FromActivity()
        {
            var tracer = tracerFactory.GetTracer(null);

            var activity = new Activity(SpanName).SetIdFormat(ActivityIdFormat.W3C).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            activity.TraceStateString = "k1=v1,k2=v2";

            var span = (SpanSdk)tracer.StartSpanFromActivity(SpanName, activity);

            Assert.Equal(activity.TraceId, span.Context.TraceId);
            Assert.Equal(activity.SpanId, span.Context.SpanId);
            Assert.Equal(activity.ParentSpanId, span.ParentSpanId);
            Assert.Equal(activity.ActivityTraceFlags, span.Context.TraceFlags);
            Assert.Equal(2, span.Context.Tracestate.Count());
            Assert.Contains(span.Context.Tracestate, pair => pair.Key == "k1" && pair.Value == "v1");
            Assert.Contains(span.Context.Tracestate, pair => pair.Key == "k2" && pair.Value == "v2");

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Internal, span.Kind);
            Assert.Equal(activity.StartTimeUtc, span.StartTimestamp.DateTime);
            Assert.Equal(activity, span.Activity);
            Assert.Null(span.Links);
        }

        [Fact]
        public void StartSpan_Recorded_FromActivity_IgnoresCurrentParent()
        {
            var tracer = tracerFactory.GetTracer(null);
            tracer.WithSpan(tracer.StartRootSpan("outer"));
            {
                var activity = new Activity(SpanName).SetIdFormat(ActivityIdFormat.W3C).SetParentId(" ").Start();
                activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

                var span = (SpanSdk)tracer.StartSpanFromActivity(SpanName, activity);

                Assert.Equal(activity.TraceId, span.Context.TraceId);
                Assert.Equal(activity.SpanId, span.Context.SpanId);
                Assert.Equal(activity.ParentSpanId, span.ParentSpanId);
                Assert.Equal(activity.ActivityTraceFlags, span.Context.TraceFlags);
            }
        }

        [Fact]
        public void StartSpan_NotRecorded_FromActivity()
        {
            var tracer = tracerFactory.GetTracer(null);

            var activity = new Activity(SpanName).SetIdFormat(ActivityIdFormat.W3C).Start();
            activity.ActivityTraceFlags = ActivityTraceFlags.None;
            activity.TraceStateString = "k1=v1,k2=v2";

            var span = (SpanSdk)tracer.StartSpanFromActivity(SpanName, activity);

            Assert.Equal(activity.TraceId, span.Context.TraceId);
            Assert.Equal(activity.SpanId, span.Context.SpanId);
            Assert.Equal(activity.ParentSpanId, span.ParentSpanId);
            Assert.Equal(activity.ActivityTraceFlags, span.Context.TraceFlags);
            Assert.Equal(2, span.Context.Tracestate.Count());
            Assert.Contains(span.Context.Tracestate, pair => pair.Key == "k1" && pair.Value == "v1");
            Assert.Contains(span.Context.Tracestate, pair => pair.Key == "k2" && pair.Value == "v2");

            // activity is not a parent and sampling decision is made
            // based on sampler alone
            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Internal, span.Kind);
            Assert.Equal(activity.StartTimeUtc, span.StartTimestamp.DateTime);
            Assert.Equal(activity, span.Activity);
            Assert.Null(span.Links);
        }

        [Fact]
        public void StartSpan_Recorded_FromActivity_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            var activity = new Activity(SpanName).SetIdFormat(ActivityIdFormat.W3C).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var span = (SpanSdk)tracer.StartSpanFromActivity(SpanName, activity, SpanKind.Client);

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Client, span.Kind);
            Assert.Equal(activity.StartTimeUtc, span.StartTimestamp.DateTime);
            Assert.Equal(activity, span.Activity);
            Assert.Null(span.Links);
        }

        [Fact]
        public void StartSpan_Recorded_FromActivity_Kind_Links_Enumerable()
        {
            var tracer = tracerFactory.GetTracer(null);

            var activity = new Activity(SpanName).SetIdFormat(ActivityIdFormat.W3C).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var firstLinkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var secondLinkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);

            var span = (SpanSdk)tracer.StartSpanFromActivity(SpanName, activity, SpanKind.Server, new[] { new Link(firstLinkContext), new Link(secondLinkContext), });

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(activity.StartTimeUtc, span.StartTimestamp.DateTime);
            Assert.Equal(activity, span.Activity);
            Assert.Equal(2, span.Links.Count());
            Assert.Equal(firstLinkContext, span.Links.First().Context);
            Assert.Equal(secondLinkContext, span.Links.Last().Context);
        }

        [Fact]
        public void StartSpan_Recorded_FromActivity_Null()
        {
            var tracer = tracerFactory.GetTracer(null);

            var startTime = DateTimeOffset.UtcNow;
            var span = (SpanSdk)tracer.StartSpanFromActivity(SpanName, null);

            Assert.NotNull(span.Activity);
            Assert.Equal(default, span.ParentSpanId);
            Assert.Empty(span.Context.Tracestate);

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Internal, span.Kind);
            AssertApproxSameTimestamp(startTime, span.StartTimestamp);
            Assert.Null(span.Links);
        }

        [Fact]
        public void StartSpan_Recorded_FromActivity_Null_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            var startTime = DateTimeOffset.UtcNow;
            var span = (SpanSdk)tracer.StartSpanFromActivity(SpanName, null, SpanKind.Producer);

            Assert.NotNull(span.Activity);
            Assert.Equal(default, span.ParentSpanId);
            Assert.Empty(span.Context.Tracestate);

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Producer, span.Kind);
            AssertApproxSameTimestamp(startTime, span.StartTimestamp);
            Assert.Null(span.Links);
        }


        [Fact]
        public void StartSpan_Recorded_FromActivity_Null_Kind_Links()
        {
            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var tracer = tracerFactory.GetTracer(null);

            var startTime = DateTimeOffset.UtcNow;
            var span = (SpanSdk)tracer.StartSpanFromActivity(SpanName, null, SpanKind.Consumer, new[] { new Link(linkContext) });

            Assert.NotNull(span.Activity);
            Assert.Equal(default, span.ParentSpanId);
            Assert.Empty(span.Context.Tracestate);

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Consumer, span.Kind);
            AssertApproxSameTimestamp(startTime, span.StartTimestamp);

            Assert.Single(span.Links);
            Assert.Equal(linkContext, span.Links.First().Context);
        }

        [Fact]
        public void StartSpan_Recorded_FromActivity_HierarchicalFormat()
        {
            var tracer = tracerFactory.GetTracer(null);

            var activity = new Activity(SpanName).SetIdFormat(ActivityIdFormat.Hierarchical).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            activity.TraceStateString = "k1=v1,k2=v2";

            var span = (SpanSdk)tracer.StartSpanFromActivity(SpanName, activity);

            Assert.NotEqual(activity, span.Activity);
            Assert.NotEqual(activity, span.Activity.Parent);

            Assert.Equal(default, span.ParentSpanId);
            Assert.Empty(span.Context.Tracestate);

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Internal, span.Kind);
            Assert.NotEqual(activity.StartTimeUtc, span.StartTimestamp.DateTime);
            Assert.Null(span.Links);
        }

        [Fact]
        public void StartSpan_Recorded_FromActivity_NotStarted()
        {
            var tracer = tracerFactory.GetTracer(null);

            var activity = new Activity(SpanName).SetIdFormat(ActivityIdFormat.W3C);
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            activity.TraceStateString = "k1=v1,k2=v2";

            var span = (SpanSdk)tracer.StartSpanFromActivity(SpanName, activity);

            Assert.NotEqual(activity, span.Activity);
            Assert.NotEqual(activity, span.Activity.Parent);

            Assert.Equal(default, span.ParentSpanId);
            Assert.Empty(span.Context.Tracestate);

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Internal, span.Kind);
            Assert.NotEqual(activity.StartTimeUtc, span.StartTimestamp.DateTime);
            Assert.Null(span.Links);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ImplicitParentSpan()
        {
            var tracer = tracerFactory.GetTracer(null);
            var traceState = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("k1", "v1") };
            var grandParentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, false, traceState);
            using (tracer.WithSpan(tracer.StartSpan(SpanName, grandParentContext)))
            {
                var parentSpan = tracer.CurrentSpan;

                var startTimestamp = PreciseTimestamp.GetUtcNow();
                var span = (SpanSdk)tracer.StartSpan(SpanName);

                Assert.True(span.Context.IsValid);
                Assert.Equal(parentSpan.Context.TraceId, span.Context.TraceId);
                Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
                Assert.Equal(parentSpan.Context.SpanId, span.ParentSpanId);
                Assert.Equal(parentSpan.Context.TraceFlags, span.Context.TraceFlags);
                Assert.Same(traceState, span.Context.Tracestate);

                Assert.True(span.IsRecording);
                Assert.Equal(SpanKind.Internal, span.Kind);
                AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
                Assert.Null(span.Links);
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
            var span = (SpanSdk)tracer.StartSpan(SpanName);

            Assert.True(span.Context.IsValid);
            Assert.Equal(parentActivity.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(parentActivity.SpanId, span.ParentSpanId);
            Assert.Equal(parentActivity.ActivityTraceFlags, span.Context.TraceFlags);
            Assert.Equal(2, span.Context.Tracestate.Count());
            Assert.Contains(span.Context.Tracestate, pair => pair.Key == "k1" && pair.Value == "v1");
            Assert.Contains(span.Context.Tracestate, pair => pair.Key == "k2" && pair.Value == "v2");

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Internal, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Null(span.Links);

            parentActivity.Stop();
        }

        [Fact]
        public void StartSpanFrom_Recorded_ImplicitParentSpan_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            using (tracer.WithSpan(tracer.StartRootSpan(SpanName)))
            {
                var startTimestamp = PreciseTimestamp.GetUtcNow();
                var span = (SpanSdk)tracer.StartSpan(SpanName, SpanKind.Producer);

                Assert.True(span.IsRecording);
                Assert.Equal(SpanKind.Producer, span.Kind);
                AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
                Assert.Null(span.Links);
            }
        }

        [Fact]
        public void StartSpanFrom_Recorded_ImplicitParentSpan_Kind_Timestamp()
        {
            var tracer = tracerFactory.GetTracer(null);

            using (tracer.WithSpan(tracer.StartRootSpan(SpanName)))
            {
                var startTimestamp = DateTimeOffset.UtcNow.AddSeconds(-10);
                var span = (SpanSdk)tracer.StartSpan(SpanName, SpanKind.Server, new SpanCreationOptions { StartTimestamp = startTimestamp });

                Assert.True(span.IsRecording);
                Assert.Equal(SpanKind.Server, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Null(span.Links);
            }
        }

        [Fact]
        public void StartSpanFrom_Recorded_ImplicitParentSpan_Kind_Timestamp_Links_Func()
        {
            var tracer = tracerFactory.GetTracer(null);

            using (tracer.WithSpan(tracer.StartRootSpan(SpanName)))
            {
                var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.Recorded);

                var startTimestamp = DateTimeOffset.UtcNow.AddSeconds(-10);
                var span = (SpanSdk)tracer.StartSpan(SpanName, SpanKind.Server, new SpanCreationOptions { StartTimestamp = startTimestamp, LinksFactory = () => new[] { new Link(linkContext) } });

                Assert.True(span.IsRecording);
                Assert.Equal(SpanKind.Server, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Single(span.Links);
                Assert.Equal(linkContext, span.Links.Single().Context);
            }
        }

        [Fact]
        public void StartSpanFrom_Recorded_ImplicitParentSpan_Kind_Timestamp_Links_Enumerable()
        {
            var tracer = tracerFactory.GetTracer(null);

            using (tracer.WithSpan(tracer.StartRootSpan(SpanName)))
            {
                var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.Recorded);

                var startTimestamp = DateTimeOffset.UtcNow.AddSeconds(-10);
                var span = (SpanSdk)tracer.StartSpan(SpanName, SpanKind.Server, new SpanCreationOptions { StartTimestamp = startTimestamp, Links = new[] { new Link(linkContext) } });

                Assert.True(span.IsRecording);
                Assert.Equal(SpanKind.Server, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Single(span.Links);
                Assert.Equal(linkContext, span.Links.Single().Context);
            }
        }

        [Fact]
        public void NotRecordedSpan_NoAttributesOrEventsAdded()
        {
            var tracer = TracerFactory.Create(b => b
                    .SetSampler(new AlwaysOffSampler()))
                .GetTracer(null);

            var span = (SpanSdk)tracer.StartRootSpan(SpanName, SpanKind.Client);

            // Check that adding attributes or events is not possible on not recorded span
            span.SetAttribute("string", "value");
            span.SetAttribute("long", 1L);
            span.SetAttribute("bool", false);
            span.SetAttribute("double", 0.1d);
            span.SetAttribute("decimal", 0.2M);

            span.AddEvent(new Event(EventDescription));
            Assert.Null(span.Attributes);
            Assert.Null(span.Events);
        }

        [Fact]
        public void EndSpan_NotAddingAttributesOrEvents()
        {
            var tracer = tracerFactory.GetTracer(null);

            var span = (SpanSdk)tracer.StartRootSpan(SpanName, SpanKind.Client);

            var spanEndTime = DateTimeOffset.UtcNow.AddSeconds(10);
            span.End(spanEndTime);

            // Check that adding trace events after Span#End() does not throw any exception and are not
            // recorded.
            foreach (var attribute in attributes)
            {
                span.SetAttribute(attribute.Key, attribute.Value);
            }

            span.SetAttribute(
                "MySingleStringAttributeKey",
                "MySingleStringAttributeValue");

            span.AddEvent(new Event(EventDescription));
            span.AddEvent(new Event(EventDescription, attributes));

            Assert.NotEqual(default, span.StartTimestamp);
            Assert.Null(span.Attributes);
            Assert.Null(span.Events);
            Assert.Null(span.Links);
            Assert.Equal(default, span.GetStatus());
            Assert.Equal(spanEndTime, span.EndTimestamp);
        }

        [Fact]
        public async Task StartSpan_PropertiesAccessible()
        {
            var contextLink = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None);
            var link = new Link(contextLink);

            var tracer = tracerFactory.GetTracer(null);
            var startTime = DateTimeOffset.UtcNow.AddSeconds(-1);
            var spanPassedToSpanProcessorHasSpanContext = false;

            spanProcessorMock
                .Setup(s => s.OnStart(It.IsAny<SpanData>()))
                .Callback<SpanData>(s =>
                {
                    spanPassedToSpanProcessorHasSpanContext = s.Context.IsValid;
                });

            var span = (SpanSdk)tracer.StartSpan(SpanName, SpanKind.Client, new SpanCreationOptions { StartTimestamp = startTime, LinksFactory = () => new[] { link } });

            span.SetAttribute(
                "MySingleStringAttributeKey",
                "MySingleStringAttributeValue");
            foreach (var attribute in attributes)
            {
                span.SetAttribute(attribute.Key, attribute.Value);
            }

            var firstEventTime = PreciseTimestamp.GetUtcNow();
            span.AddEvent(new Event(EventDescription, firstEventTime));
            await Task.Delay(TimeSpan.FromMilliseconds(100));

            var secondEventTime = PreciseTimestamp.GetUtcNow();
            span.AddEvent(new Event(EventDescription, attributes));

            Assert.Equal(span.Activity.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(span.Activity.ParentSpanId, span.ParentSpanId);
            Assert.Equal(span.Activity.ActivityTraceFlags, span.Context.TraceFlags);
            Assert.Empty(span.Context.Tracestate);

            Assert.Equal(SpanName, span.Name);
            Assert.Equal(span.Activity.ParentSpanId, span.ParentSpanId);

            span.Attributes.AssertAreSame(expectedAttributes);

            Assert.Equal(2, span.Events.Count());
            AssertApproxSameTimestamp(span.Events.ToList()[1].Timestamp, secondEventTime);

            var event0 = span.Events.ToList()[0];
            Assert.Equal(EventDescription, event0.Name);
            Assert.Equal(firstEventTime, event0.Timestamp);
            Assert.Empty(event0.Attributes);

            Assert.Equal(EventDescription, span.Events.ToList()[1].Name);
            Assert.Equal(attributes, span.Events.ToList()[1].Attributes);

            Assert.Single(span.Links);
            Assert.Equal(link, span.Links.First());

            Assert.Equal(startTime, span.StartTimestamp);

            Assert.False(span.GetStatus().IsValid);
            Assert.Equal(default, span.EndTimestamp);

            var startEndMock = Mock.Get(spanProcessor);

            var spanData = new SpanData(span);
            Assert.True(spanData.Status.IsValid);
            spanProcessorMock.Verify(s => s.OnStart(spanData), Times.Once);
            Assert.True(spanPassedToSpanProcessorHasSpanContext);
            startEndMock.Verify(s => s.OnEnd(spanData), Times.Never);
        }

        [Fact]
        public async Task EndSpan_Properties()
        {
            var contextLink = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None);

            var link = new Link(contextLink);
            var tracer = tracerFactory.GetTracer(null);
            var startTime = DateTimeOffset.UtcNow.AddSeconds(-1);
            var span = (SpanSdk)tracer.StartRootSpan(SpanName, SpanKind.Client, new SpanCreationOptions { StartTimestamp = startTime, LinksFactory = () => new[] { link } });
            span.SetAttribute(
                "MySingleStringAttributeKey",
                "MySingleStringAttributeValue");
            foreach (var attribute in attributes)
            {
                span.SetAttribute(attribute.Key, attribute.Value);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
            var firstEventTime = PreciseTimestamp.GetUtcNow();
            span.AddEvent(new Event(EventDescription, firstEventTime));

            await Task.Delay(TimeSpan.FromMilliseconds(100));
            var secondEventTime = PreciseTimestamp.GetUtcNow();
            span.AddEvent(new Event(EventDescription, attributes));

            span.Status = Status.Cancelled;

            var spanEndTime = PreciseTimestamp.GetUtcNow();
            span.End();

            Assert.Equal(span.Activity.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(span.Activity.ParentSpanId, span.ParentSpanId);
            Assert.Equal(span.Activity.ActivityTraceFlags, span.Context.TraceFlags);

            Assert.Equal(SpanName, span.Name);
            Assert.Equal(span.Activity.ParentSpanId, span.ParentSpanId);

            span.Attributes.AssertAreSame(expectedAttributes);
            Assert.Equal(2, span.Events.Count());

            var event0 = span.Events.ToList()[0];
            Assert.Equal(firstEventTime, event0.Timestamp);
            Assert.Equal(EventDescription, event0.Name);
            Assert.Empty(event0.Attributes);

            AssertApproxSameTimestamp(span.Events.ToList()[1].Timestamp, secondEventTime);

            Assert.Single(span.Links);
            Assert.Equal(link, span.Links.First());
            Assert.Equal(startTime, span.StartTimestamp);
            Assert.Equal(Status.Cancelled, span.GetStatus());
            AssertApproxSameTimestamp(spanEndTime, span.EndTimestamp);

            var spanData = new SpanData(span);
            spanProcessorMock.Verify(s => s.OnStart(spanData), Times.Once);
            spanProcessorMock.Verify(s => s.OnEnd(spanData), Times.Once);
        }

        [Fact]
        public void SetStatus()
        {
            var tracer = tracerFactory.GetTracer(null);
            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            Assert.Equal(default, span.GetStatus());
            span.Status = Status.Cancelled;
            Assert.Equal(Status.Cancelled, span.GetStatus());
            span.End();
            Assert.Equal(Status.Cancelled, span.GetStatus());

            var spanData = new SpanData(span);
            spanProcessorMock.Verify(s => s.OnStart(spanData), Times.Once);
            spanProcessorMock.Verify(s => s.OnEnd(spanData), Times.Once);
        }

        [Fact]
        public void BadArguments_SetInvalidStatus()
        {
            var tracer = tracerFactory.GetTracer(null);
            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            // does not throw
            span.Status = new Status();

            Assert.Equal(default, span.GetStatus());
        }

        [Fact]
        public void BadArguments_UpdateName_Null()
        {
            var tracer = tracerFactory.GetTracer(null);
            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            // does not throw
            span.UpdateName(null);

            Assert.Equal(string.Empty, span.Name);
        }

        [Fact]
        public void BadArguments_SetAttribute_NullKey()
        {
            var tracer = tracerFactory.GetTracer(null);
            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            // does not throw
            span.SetAttribute(null, "foo");
            span.SetAttribute(null, 1L);
            span.SetAttribute(null, 0.1d);
            span.SetAttribute(null, false);

            Assert.Single(span.Attributes);
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == string.Empty && kvp.Value is bool bv && bv == false));
        }

        [Fact]
        public void SetAttribute_ValidTypes()
        {
            var tracer = tracerFactory.GetTracer(null);
            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            span.SetAttribute("string", "foo");
            span.SetAttribute("bool", false);
            span.SetAttribute("long", -1L);
            span.SetAttribute("ulong", 1UL);
            span.SetAttribute("uint", 2U);
            span.SetAttribute("int", -2);
            span.SetAttribute("sbyte", (sbyte)-3);
            span.SetAttribute("byte", (byte)3);
            span.SetAttribute("short", (short)-4);
            span.SetAttribute("ushort", (ushort)4);
            span.SetAttribute("double", 0.1d);
            span.SetAttribute("float", 0.2f);
            span.SetAttribute("decimal", 0.3M);

            Assert.Equal(13, span.Attributes.Count());
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == "string" && kvp.Value is string sv && sv == "foo"));
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == "bool" && kvp.Value is bool bv && bv == false));
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == "long" && kvp.Value is long lv && lv == -1));
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == "ulong" && kvp.Value is double lv && lv == 1));
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == "uint" && kvp.Value is long uv && uv == 2));
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == "int" && kvp.Value is long iv && iv == -2));
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == "sbyte" && kvp.Value is long sv && sv == -3));
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == "byte" && kvp.Value is long bv && bv == 3));
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == "short" && kvp.Value is long sv && sv == -4));
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == "ushort" && kvp.Value is long uv && uv == 4));
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == "double" && kvp.Value is double dv && dv == 0.1));
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == "float" && kvp.Value is double fv && Math.Round(fv, 3) == 0.2));
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == "decimal" && kvp.Value is decimal dv && dv == 0.3M));
        }

        [Theory]
        [InlineData(new[] { true, false, true })]
        [InlineData(new[] { 1L, 2L, 3L })]
        [InlineData(new[] { 1UL, 2UL, 3UL })]
        [InlineData(new[] { 1U, 2U, 3U })]
        [InlineData(new[] { 1, 2, 3 })]
        [InlineData(new sbyte[] { 1, 2, 3 })]
        [InlineData(new byte[] { 1, 2, 3 })]
        [InlineData(new short[] { 1, 2, 3 })]
        [InlineData(new ushort[] { 1, 2, 3 })]
        [InlineData(new[] { 1.1d, 2.2d, 3.3d })]
        [InlineData(new[] { 1.1f, 2.2f, 3.3f })]
        public void SetAttribute_Array(object array)
        {
            var tracer = tracerFactory.GetTracer(null);
            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            span.SetAttribute("intArray", array);

            Assert.Single(span.Attributes);

            var attribute = span.Attributes.Single(kvp => kvp.Key == "intArray");
            Assert.Equal(array, attribute.Value);
        }

        [Fact]
        public void SetAttribute_Array_String()
        {
            var tracer = tracerFactory.GetTracer(null);
            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            var array = new[] { "1", "2", "3" };
            span.SetAttribute("array", array);

            Assert.Single(span.Attributes);

            var attribute = span.Attributes.Single(kvp => kvp.Key == "array");
            Assert.Equal(array, attribute.Value);

        }

        [Fact]
        public void SetAttribute_Array_Decimal()
        {
            var tracer = tracerFactory.GetTracer(null);
            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            var array = new[] { 1.1M, 2.2M, 3.3M };
            span.SetAttribute("array", array);

            Assert.Single(span.Attributes);

            var attribute = span.Attributes.Single(kvp => kvp.Key == "array");
            Assert.Equal(array, attribute.Value);
        }

        [Fact]
        public void SetAttribute_Array_IEnumerable()
        {
            var tracer = tracerFactory.GetTracer(null);
            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            IEnumerable<int> array = new List<int> { 1, 2, 3 };
            span.SetAttribute("array", array);

            Assert.Single(span.Attributes);

            var attribute = span.Attributes.Single(kvp => kvp.Key == "array");
            Assert.Equal(array, attribute.Value);
        }

        [Fact]
        public void SetAttribute_Array_Cant_Mix_Types()
        {
            var tracer = tracerFactory.GetTracer(null);
            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            var array = new object[] { 1, "2", false };
            span.SetAttribute("array", array);

            Assert.Single(span.Attributes);

            var attribute = span.Attributes.Single(kvp => kvp.Key == "array");
            Assert.Equal("array", attribute.Key);
            Assert.Equal(string.Empty, attribute.Value);
        }

        [Fact]
        public void SetAttribute_Array_Sets_Empty_String_For_Emtpy_Array()
        {
            var tracer = tracerFactory.GetTracer(null);
            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            var array = new object[0];
            span.SetAttribute("array", array);

            Assert.Single(span.Attributes);

            var attribute = span.Attributes.Single(kvp => kvp.Key == "array");
            Assert.Equal("array", attribute.Key);
            Assert.Equal(string.Empty, attribute.Value);
        }

        [Fact]
        public void BadArguments_SetAttribute_NullOrEmptyValue()
        {
            var tracer = tracerFactory.GetTracer(null);
            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            // does not throw
            span.SetAttribute("null", null);
            span.SetAttribute("empty", string.Empty);

            Assert.Equal(2, span.Attributes.Count());
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == "null" && kvp.Value is string sv && sv == string.Empty));
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == "empty" && kvp.Value is string sv && sv == string.Empty));
        }

        [Fact]
        public void BadArguments_SetAttribute_BadTypeValue()
        {
            var tracer = tracerFactory.GetTracer(null);
            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            // does not throw
            span.SetAttribute("complex type", new { });
            span.SetAttribute("not supported type", DateTimeOffset.UtcNow);

            Assert.Equal(2, span.Attributes.Count());
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == "complex type" && kvp.Value is string sv && sv == string.Empty));
            Assert.Single(span.Attributes.Where(kvp => kvp.Key == "not supported type" && kvp.Value is string sv && sv == string.Empty));
        }

        [Fact]
        public void BadArguments_NullEvent()
        {
            var tracer = tracerFactory.GetTracer(null);
            var span = (SpanSdk)tracer.StartRootSpan(SpanName);

            span.AddEvent((string)null);
            span.AddEvent(new Event(null, null));
            Assert.Equal(2, span.Events.Count());

            var event0 = span.Events.ToArray()[0];
            var event1 = span.Events.ToArray()[1];

            Assert.Empty(event0.Name);
            Assert.Empty(event1.Name);

            Assert.Empty(event0.Attributes);
            Assert.Empty(event1.Attributes);

            span.AddEvent((Event)null);
            Assert.Equal(2, span.Events.Count());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EndSpanStopsActivity(bool recordEvents)
        {
            var parentActivity = new Activity("parent").Start();

            if (recordEvents)
            {
                parentActivity.ActivityTraceFlags = ActivityTraceFlags.Recorded;
            }

            var tracer = tracerFactory
                .GetTracer(null);
            var span = (SpanSdk)tracer.StartSpan(SpanName);
            span.End();

            // activity is stopped
            Assert.NotEqual(default, span.Activity.Duration);
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
            var span = (SpanSdk)tracer.StartSpanFromActivity(SpanName, activity);
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

            SpanSdk span;
            if (ownsActivity)
            {
                span = (SpanSdk)tracer.StartSpanFromActivity(SpanName, activity);
            }
            else
            {
                span = (SpanSdk)tracer.StartSpan(SpanName);
            }

            var anotherActivity = new Activity(SpanName).Start();
            span.End();

            Assert.Same(anotherActivity, Activity.Current);
        }

        [Fact]
        public void StartActiveSpan_ParentSpan()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentSpan = (SpanSdk)tracer.StartSpan(SpanName);
            using (var scope = tracer.StartActiveSpan(SpanName, parentSpan, out var ispan))
            {
                Assert.NotNull(scope);

                var span = tracer.CurrentSpan;
                Assert.Same(ispan, span);
                Assert.NotNull(span);
                Assert.True(span.Context.IsValid);
                Assert.Equal(parentSpan.Context.SpanId, ((SpanSdk)span).ParentSpanId);
            }

            Assert.False(tracer.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void StartActiveSpan_ParentSpan_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentSpan = (SpanSdk)tracer.StartSpan(SpanName);
            using (var scope = tracer.StartActiveSpan(SpanName, parentSpan, SpanKind.Producer, out var ispan))
            {
                Assert.NotNull(scope);

                var span = (SpanSdk)tracer.CurrentSpan;
                Assert.Same(ispan, span);
                Assert.True(span.Context.IsValid);
                Assert.NotNull(span);
                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.Equal(parentSpan.Context.SpanId, span.ParentSpanId);
            }

            Assert.False(tracer.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void StartActiveSpan_ParentSpan_Kind_Timestamp()
        {
            var tracer = tracerFactory.GetTracer(null);

            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            var parentSpan = (SpanSdk)tracer.StartSpan(SpanName);
            using (var scope = tracer.StartActiveSpan(SpanName, parentSpan, SpanKind.Producer, new SpanCreationOptions { StartTimestamp = startTimestamp },
                out var ispan))
            {
                Assert.NotNull(scope);

                var span = (SpanSdk)tracer.CurrentSpan;
                Assert.Same(ispan, span);
                Assert.NotNull(span);
                Assert.True(span.Context.IsValid);
                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Equal(parentSpan.Context.SpanId, span.ParentSpanId);
            }

            Assert.False(tracer.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void StartActiveSpan_ParentSpan_Kind_Timestamp_Links()
        {
            var tracer = tracerFactory.GetTracer(null);

            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            var parentSpan = (SpanSdk)tracer.StartSpan(SpanName);
            using (var scope = tracer.StartActiveSpan(SpanName, parentSpan, SpanKind.Producer,
                new SpanCreationOptions { StartTimestamp = startTimestamp, Links = new[] { new Link(linkContext) }, }, out var ispan))
            {
                Assert.NotNull(scope);

                var span = (SpanSdk)tracer.CurrentSpan;
                Assert.NotNull(span);
                Assert.Same(ispan, span);
                Assert.True(span.Context.IsValid);
                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Single(span.Links);
                Assert.Equal(parentSpan.Context.SpanId, span.ParentSpanId);
            }

            Assert.False(tracer.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void StartActiveSpan_ParentContext()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            using (var scope = tracer.StartActiveSpan(SpanName, parentContext, out var ispan))
            {
                Assert.NotNull(scope);

                var span = tracer.CurrentSpan;
                Assert.NotNull(span);
                Assert.Same(ispan, span);
                Assert.True(span.Context.IsValid);
                Assert.Equal(parentContext.SpanId, ((SpanSdk)span).ParentSpanId);
            }

            Assert.False(tracer.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void StartActiveSpan_ParentContext_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            using (var scope = tracer.StartActiveSpan(SpanName, parentContext, SpanKind.Producer, out var ispan))
            {
                Assert.NotNull(scope);

                var span = (SpanSdk)tracer.CurrentSpan;

                Assert.True(span.Context.IsValid);
                Assert.Same(ispan, span);
                Assert.NotNull(span);

                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.Equal(parentContext.SpanId, span.ParentSpanId);
            }

            Assert.False(tracer.CurrentSpan.Context.IsValid);
        }


        [Fact]
        public void StartActiveSpan_ParentContext_Kind_Timestamp()
        {
            var tracer = tracerFactory.GetTracer(null);

            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            using (var scope = tracer.StartActiveSpan(SpanName, parentContext, SpanKind.Producer,
                new SpanCreationOptions { StartTimestamp = startTimestamp }, out var ispan))
            {
                Assert.NotNull(scope);

                var span = (SpanSdk)tracer.CurrentSpan;
                Assert.NotNull(span);
                Assert.Same(ispan, span);
                Assert.True(span.Context.IsValid);
                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Equal(parentContext.SpanId, span.ParentSpanId);
            }

            Assert.False(tracer.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void StartActiveSpan_ParentContext_Kind_Timestamp_Links()
        {
            var tracer = tracerFactory.GetTracer(null);

            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            using (var scope = tracer.StartActiveSpan(SpanName, parentContext, SpanKind.Producer,
                new SpanCreationOptions { StartTimestamp = startTimestamp, Links = new[] { new Link(linkContext) }, }, out var ispan))
            {
                Assert.NotNull(scope);

                var span = (SpanSdk)tracer.CurrentSpan;
                Assert.NotNull(span);
                Assert.Same(ispan, span);
                Assert.True(span.Context.IsValid);
                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Single(span.Links);
                Assert.Equal(parentContext.SpanId, span.ParentSpanId);
            }

            Assert.False(tracer.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void StartActiveSpan_ImplicitParentSpan()
        {
            var tracer = tracerFactory.GetTracer(null);

            using (var parentScope = tracer.StartActiveSpan(SpanName, out var parentSpan))
            {
                using var scope = tracer.StartActiveSpan(SpanName, out var childSpan);
                Assert.NotNull(scope);

                var span = tracer.CurrentSpan;
                Assert.Same(childSpan, span);
                Assert.NotNull(span);

                Assert.True(span.Context.IsValid);
                Assert.NotEqual(default, ((SpanSdk)span).ParentSpanId);
            }

            Assert.False(tracer.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void StartActiveSpan_ImplicitParentSpan_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            using (var parentScope = tracer.StartActiveSpan(SpanName, out var parentSpan))
            {
                using var scope = tracer.StartActiveSpan(SpanName, SpanKind.Producer, out var childSpan);
                Assert.NotNull(scope);

                var span = (SpanSdk)tracer.CurrentSpan;

                Assert.True(span.Context.IsValid);
                Assert.NotNull(span);
                Assert.Same(childSpan, span);

                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.NotEqual(default, span.ParentSpanId);
            }

            Assert.False(tracer.CurrentSpan.Context.IsValid);
        }


        [Fact]
        public void StartActiveSpan_ImplicitParentSpan_Kind_Timestamp()
        {
            var tracer = tracerFactory.GetTracer(null);

            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            using (var parentScope = tracer.StartActiveSpan(SpanName, out var parentspan))
            {
                using var scope = tracer.StartActiveSpan(SpanName, SpanKind.Producer,
                    new SpanCreationOptions { StartTimestamp = startTimestamp }, out var childSpan);
                Assert.NotNull(scope);

                var span = (SpanSdk)tracer.CurrentSpan;
                Assert.NotNull(span);
                Assert.Same(childSpan, span);
                Assert.True(span.Context.IsValid);

                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.NotEqual(default, span.ParentSpanId);
            }

            Assert.False(tracer.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void StartActiveSpan_ImplicitParentSpan_Kind_Timestamp_Links()
        {
            var tracer = tracerFactory.GetTracer(null);

            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            using (var parentScope = tracer.StartActiveSpan(SpanName, out _))
            {
                using var scope = tracer.StartActiveSpan(SpanName, SpanKind.Producer,
                    new SpanCreationOptions { StartTimestamp = startTimestamp, Links = new[] { new Link(linkContext) }, }, out var ispan);
                Assert.NotNull(scope);

                var span = (SpanSdk)tracer.CurrentSpan;
                Assert.NotNull(span);
                Assert.Same(ispan, span);
                Assert.True(span.Context.IsValid);

                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Single(span.Links);
                Assert.NotEqual(default, span.ParentSpanId);
            }

            Assert.False(tracer.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void StartActiveSpan_FromActivity()
        {
            var tracer = tracerFactory.GetTracer(null);

            var activity = new Activity(SpanName).SetIdFormat(ActivityIdFormat.W3C).Start();
            using (var scope = tracer.StartActiveSpanFromActivity(SpanName, activity, out var ispan))
            {
                Assert.NotNull(scope);

                var span = tracer.CurrentSpan;
                Assert.NotNull(span);
                Assert.Same(ispan, span);

                Assert.True(span.Context.IsValid);
                Assert.Equal(activity.SpanId, span.Context.SpanId);
                Assert.Equal(SpanKind.Internal, ((SpanSdk)span).Kind);
                Assert.Null(((SpanSdk)span).Links);
            }

            Assert.False(tracer.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void StartActiveSpan_FromActivity_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            var activity = new Activity(SpanName).SetIdFormat(ActivityIdFormat.W3C).Start();
            using (var scope = tracer.StartActiveSpanFromActivity(SpanName, activity, SpanKind.Consumer, out var ispan))
            {
                Assert.NotNull(scope);

                var span = tracer.CurrentSpan;
                Assert.NotNull(span);
                Assert.Same(ispan, span);
                Assert.True(span.Context.IsValid);
                Assert.Equal(activity.SpanId, span.Context.SpanId);
                Assert.Equal(SpanKind.Consumer, ((SpanSdk)span).Kind);
                Assert.Null(((SpanSdk)span).Links);
            }

            Assert.False(tracer.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void StartActiveSpan_FromActivity_Kind_Links()
        {
            var tracer = tracerFactory.GetTracer(null);

            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var activity = new Activity(SpanName).SetIdFormat(ActivityIdFormat.W3C).Start();
            using (var scope = tracer.StartActiveSpanFromActivity(SpanName, activity, SpanKind.Consumer, new[] { new Link(linkContext), }, out var ispan))
            {
                Assert.NotNull(scope);

                var span = tracer.CurrentSpan;
                Assert.NotNull(span);
                Assert.Same(ispan, span);
                Assert.True(span.Context.IsValid);
                Assert.Equal(activity.SpanId, span.Context.SpanId);
                Assert.Equal(SpanKind.Consumer, ((SpanSdk)span).Kind);
                Assert.Single(((SpanSdk)span).Links);
            }

            Assert.False(tracer.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void StartSpan_WithAttributes_PassesAttributesToSampler_AndSetsOnSpan()
        {
            var samplerMock = new Mock<Sampler>();
            var tracer = TracerFactory.Create(b => b
                    .SetSampler(samplerMock.Object)
                    .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor)))
                .GetTracer(null);

            samplerMock.Setup(s => s.ShouldSample(
                in It.Ref<SpanContext>.IsAny,
                in It.Ref<ActivityTraceId>.IsAny,
                It.IsAny<string>(),
                It.IsAny<SpanKind>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IEnumerable<Link>>())).Returns(new SamplingResult(true));

            var span = (SpanSdk)tracer.StartSpan("test", SpanKind.Client, new SpanCreationOptions { Attributes = this.attributes, });
            span.Attributes.AssertAreSame(this.attributes);

            samplerMock.Verify(o => o.ShouldSample(
                in It.Ref<SpanContext>.IsAny,
                in It.Ref<ActivityTraceId>.IsAny,
                It.IsAny<string>(),
                It.IsAny<SpanKind>(),
                It.Is<IDictionary<string, object>>(a => a == this.attributes),
                It.IsAny<IEnumerable<Link>>()), Times.Once);
        }

        [Fact]
        public void StartNotSampledSpan_WithAttributes_PassesAttributesToSampler_DoesNotSetAttributesOnSpan()
        {
            var samplerMock = new Mock<Sampler>();
            var tracer = TracerFactory.Create(b => b
                    .SetSampler(samplerMock.Object)
                    .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor)))
                .GetTracer(null);

            samplerMock.Setup(s => s.ShouldSample(
                in It.Ref<SpanContext>.IsAny,
                in It.Ref<ActivityTraceId>.IsAny,
                It.IsAny<string>(),
                It.IsAny<SpanKind>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IEnumerable<Link>>())).Returns(new SamplingResult(false));

            var span = (SpanSdk)tracer.StartSpan("test", SpanKind.Client, new SpanCreationOptions { Attributes = this.attributes, });
            Assert.Null(span.Attributes);

            samplerMock.Verify(o => o.ShouldSample(
                in It.Ref<SpanContext>.IsAny,
                in It.Ref<ActivityTraceId>.IsAny,
                It.IsAny<string>(),
                It.IsAny<SpanKind>(),
                It.Is<IDictionary<string, object>>(a => a == this.attributes),
                It.IsAny<IEnumerable<Link>>()), Times.Once);
        }

        private static void AssertApproxSameTimestamp(DateTimeOffset one, DateTimeOffset two)
        {
            var timeShift = Math.Abs((one - two).TotalMilliseconds);
            Assert.InRange(timeShift, 0, 40);
        }

        public void Dispose()
        {
            Activity.Current = null;
        }
    }
}
