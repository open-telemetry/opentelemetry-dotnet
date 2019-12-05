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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using OpenTelemetry.Api.Utils;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace.Export;
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
            var grandParentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, false, traceState);
            var parentSpan = (Span)tracer.StartSpan(SpanName, grandParentContext);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartSpan(SpanName, parentSpan);

            Assert.True(span.Context.IsValid);
            Assert.Equal(parentSpan.Context.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(parentSpan.Context.SpanId, span.ParentSpanId);
            Assert.Equal(parentSpan.Context.TraceOptions, span.Context.TraceOptions);
            Assert.Same(traceState, span.Context.Tracestate);

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Internal, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentSpan_IgnoresCurrentParent()
        {
            var tracer = tracerFactory.GetTracer(null);

            using (tracer.StartActiveSpan("outer", out _))
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
        public void StartSpanFrom_Recorded_ParentSpan_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentSpan = tracer.StartRootSpan(SpanName);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartSpan(SpanName, parentSpan, SpanKind.Client);

            Assert.True(span.IsRecording);
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
            var span = (Span)tracer.StartSpan(SpanName, parentSpan, SpanKind.Server, new SpanCreationOptions { StartTimestamp = startTimestamp });

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentSpan_Kind_Links_Func()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentSpan = tracer.StartRootSpan(SpanName);
            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartSpan(SpanName, parentSpan, SpanKind.Server, new SpanCreationOptions { LinksFactory = () => new[] { new Link(linkContext) } });

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Server, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Single(span.Links);
            Assert.Same(linkContext, span.Links.Single().Context);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentSpan_Kind_Links_Enumerable()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentSpan = tracer.StartRootSpan(SpanName);
            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartSpan(SpanName, parentSpan, SpanKind.Server, new SpanCreationOptions { Links = new[] { new Link(linkContext) } });

            Assert.True(span.IsRecording);
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

            Assert.True(span.IsRecording);
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

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Consumer, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartRootSpanFrom_Kind_Timestamp()
        {
            var tracer = tracerFactory.GetTracer(null);

            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            var span = (Span)tracer.StartRootSpan(SpanName, SpanKind.Client, new SpanCreationOptions { StartTimestamp = startTimestamp });

            Assert.True(span.IsRecording);
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
            var span = (Span)tracer.StartRootSpan(SpanName, SpanKind.Server, new SpanCreationOptions
            {
                StartTimestamp = startTimestamp,
                Links = new[] { new Link(linkContext) },
            });

            Assert.True(span.IsRecording);
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
            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, false, traceState);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartSpan(SpanName, parentContext);

            Assert.True(span.Context.IsValid);
            Assert.Equal(parentContext.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(parentContext.SpanId, span.ParentSpanId);
            Assert.Equal(parentContext.TraceOptions, span.Context.TraceOptions);
            Assert.Same(traceState, span.Context.Tracestate);

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Internal, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartSpanFrom_InvalidContext()
        {
            var tracer = tracerFactory.GetTracer(null);
            var parentContext = SpanContext.BlankLocal;

            var span = (Span)tracer.StartSpan(SpanName, parentContext);

            Assert.True(span.Context.IsValid);
            Assert.NotEqual(default, span.Context.TraceId);
            Assert.NotEqual(default, span.Context.SpanId);
            Assert.Equal(default, span.ParentSpanId);

            // always sample sampler
            Assert.Equal(ActivityTraceFlags.Recorded, span.Context.TraceOptions);
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

                var span = (Span)tracer.StartSpan(SpanName, parentContext);

                Assert.True(span.Context.IsValid);
                Assert.Equal(parentContext.TraceId, span.Context.TraceId);
                Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
                Assert.Equal(parentContext.SpanId, span.ParentSpanId);
                Assert.Equal(parentContext.TraceOptions, span.Context.TraceOptions);
            }
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentContext_Kind_Timestamp()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            var span = (Span)tracer.StartSpan(SpanName, parentContext, SpanKind.Server, new SpanCreationOptions { StartTimestamp = startTimestamp } );

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentContext_Kind_Links_Func()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartSpan(SpanName, parentContext, SpanKind.Server, new SpanCreationOptions { LinksFactory = () => new[] { new Link(linkContext) }} );

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Server, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Single(span.Links);
            Assert.Same(linkContext, span.Links.Single().Context);
        }

        [Fact]
        public void StartSpanFrom_Recorded_ParentContext_Kind_Links_Enumerable()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var firstLinkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var secondLinkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span)tracer.StartSpan(SpanName, parentContext, SpanKind.Server, new SpanCreationOptions { Links = new[] { new Link(firstLinkContext), new Link(secondLinkContext) } });

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Server, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Equal(2, span.Links.Count());
            Assert.Same(firstLinkContext, span.Links.First().Context);
            Assert.Same(secondLinkContext, span.Links.Last().Context);
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

            Assert.True(span.IsRecording);
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
            Assert.True(span.IsRecording);
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

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Client, span.Kind);
            Assert.Equal(activity.StartTimeUtc, span.StartTimestamp.DateTime);
            Assert.Equal(activity, span.Activity);
            Assert.Empty(span.Links);
        }

        [Fact]
        public void StartSpan_Recorded_FromActivity_Kind_Links_Enumerable()
        {
            var tracer = tracerFactory.GetTracer(null);

            var activity = new Activity(SpanName).SetIdFormat(ActivityIdFormat.W3C).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var firstLinkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var secondLinkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);

            var span = (Span)tracer.StartSpanFromActivity(SpanName, activity, SpanKind.Server, new[] { new Link(firstLinkContext), new Link(secondLinkContext),  });

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(activity.StartTimeUtc, span.StartTimestamp.DateTime);
            Assert.Equal(activity, span.Activity);
            Assert.Equal(2, span.Links.Count());
            Assert.Same(firstLinkContext, span.Links.First().Context);
            Assert.Same(secondLinkContext, span.Links.Last().Context);
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
                var span = (Span)tracer.StartSpan(SpanName);

                Assert.True(span.Context.IsValid);
                Assert.Equal(parentSpan.Context.TraceId, span.Context.TraceId);
                Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
                Assert.Equal(parentSpan.Context.SpanId, span.ParentSpanId);
                Assert.Equal(parentSpan.Context.TraceOptions, span.Context.TraceOptions);
                Assert.Same(traceState, span.Context.Tracestate);

                Assert.True(span.IsRecording);
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

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Internal, span.Kind);
            AssertApproxSameTimestamp(startTimestamp, span.StartTimestamp);
            Assert.Empty(span.Links);
            
            parentActivity.Stop();
        }

        [Fact]
        public void StartSpanFrom_Recorded_ImplicitParentSpan_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            using (tracer.WithSpan(tracer.StartRootSpan(SpanName)))
            {
                var startTimestamp = PreciseTimestamp.GetUtcNow();
                var span = (Span)tracer.StartSpan(SpanName, SpanKind.Producer);

                Assert.True(span.IsRecording);
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
                var span = (Span)tracer.StartSpan(SpanName, SpanKind.Server, new SpanCreationOptions { StartTimestamp = startTimestamp });

                Assert.True(span.IsRecording);
                Assert.Equal(SpanKind.Server, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Empty(span.Links);
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
                var span = (Span)tracer.StartSpan(SpanName, SpanKind.Server, new SpanCreationOptions { StartTimestamp = startTimestamp, LinksFactory = () => new[] { new Link(linkContext) } });

                Assert.True(span.IsRecording);
                Assert.Equal(SpanKind.Server, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Single(span.Links);
                Assert.Same(linkContext, span.Links.Single().Context);
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
                var span = (Span)tracer.StartSpan(SpanName, SpanKind.Server, new SpanCreationOptions { StartTimestamp = startTimestamp, Links = new[] { new Link(linkContext) } });

                Assert.True(span.IsRecording);
                Assert.Equal(SpanKind.Server, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Single(span.Links);
                Assert.Same(linkContext, span.Links.Single().Context);
            }
        }

        [Fact]
        public void EndSpan_EventsNotRecorded()
        {
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
            var link = new Link(contextLink);

            var tracer = tracerFactory.GetTracer(null);
            var startTime = DateTimeOffset.UtcNow.AddSeconds(-1);
            var spanPassedToSpanProcessorHasSpanContext = false;

            spanProcessorMock
                .Setup(s => s.OnStart(It.IsAny<Span>()))
                .Callback<Span>(s =>
                {
                    spanPassedToSpanProcessorHasSpanContext = s.Context != null;
                });

            var span = (Span)tracer.StartSpan(SpanName, SpanKind.Client, new SpanCreationOptions { StartTimestamp = startTime, LinksFactory = () => new[] { link } });

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
            Assert.True(spanPassedToSpanProcessorHasSpanContext);
            startEndMock.Verify(s => s.OnEnd(span), Times.Never);
        }

        [Fact]
        public async Task EndSpan_Properties()
        {
            var contextLink = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None);

            var link = new Link(contextLink);
            var tracer = tracerFactory.GetTracer(null);
            var startTime = DateTimeOffset.UtcNow.AddSeconds(-1);
            var span = (Span)tracer.StartRootSpan(SpanName, SpanKind.Client, new SpanCreationOptions { StartTimestamp = startTime, LinksFactory = () => new[] { link }});
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
            var span = (Span)tracer.StartSpan(SpanName);
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

        [Fact]
        public void StartActiveSpan_ParentSpan()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentSpan = (Span)tracer.StartSpan(SpanName);
            using (var scope = tracer.StartActiveSpan(SpanName, parentSpan, out var ispan))
            {
                Assert.NotNull(scope);

                var span = tracer.CurrentSpan;
                Assert.Same(ispan, span);
                Assert.NotNull(span);
                Assert.NotSame(BlankSpan.Instance, tracer.CurrentSpan);
                Assert.True(span.Context.IsValid);
                Assert.Equal(parentSpan.Context.SpanId,((Span)span).ParentSpanId);
            }

            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
        }

        [Fact]
        public void StartActiveSpan_ParentSpan_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentSpan = (Span)tracer.StartSpan(SpanName);
            using (var scope = tracer.StartActiveSpan(SpanName, parentSpan, SpanKind.Producer, out var ispan))
            {
                Assert.NotNull(scope);

                var span = (Span)tracer.CurrentSpan;
                Assert.Same(ispan, span);
                Assert.True(span.Context.IsValid);
                Assert.NotNull(span);
                Assert.NotSame(BlankSpan.Instance, tracer.CurrentSpan);
                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.Equal(parentSpan.Context.SpanId, ((Span)span).ParentSpanId);
            }

            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
        }


        [Fact]
        public void StartActiveSpan_ParentSpan_Kind_Timestamp()
        {
            var tracer = tracerFactory.GetTracer(null);

            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            var parentSpan = (Span)tracer.StartSpan(SpanName);
            using (var scope = tracer.StartActiveSpan(SpanName, parentSpan, SpanKind.Producer, new SpanCreationOptions { StartTimestamp = startTimestamp }, 
                out var ispan))
            {
                Assert.NotNull(scope);

                var span = (Span)tracer.CurrentSpan;
                Assert.Same(ispan, span);
                Assert.NotNull(span);
                Assert.True(span.Context.IsValid);
                Assert.NotSame(BlankSpan.Instance, tracer.CurrentSpan);
                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Equal(parentSpan.Context.SpanId, ((Span)span).ParentSpanId);
            }

            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
        }

        [Fact]
        public void StartActiveSpan_ParentSpan_Kind_Timestamp_Links()
        {
            var tracer = tracerFactory.GetTracer(null);

            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            var parentSpan = (Span)tracer.StartSpan(SpanName);
            using (var scope = tracer.StartActiveSpan(SpanName, parentSpan, SpanKind.Producer,
                new SpanCreationOptions { StartTimestamp = startTimestamp, Links = new[] { new Link(linkContext) }, }, out var ispan))
            {
                Assert.NotNull(scope);

                var span = (Span)tracer.CurrentSpan;
                Assert.NotNull(span);
                Assert.Same(ispan, span);
                Assert.True(span.Context.IsValid);
                Assert.NotSame(BlankSpan.Instance, tracer.CurrentSpan);
                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Single(span.Links);
                Assert.Equal(parentSpan.Context.SpanId, ((Span)span).ParentSpanId);
            }

            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
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
                Assert.NotSame(BlankSpan.Instance, tracer.CurrentSpan);
                Assert.True(span.Context.IsValid);
                Assert.Equal(parentContext.SpanId, ((Span)span).ParentSpanId);
            }

            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
        }

        [Fact]
        public void StartActiveSpan_ParentContext_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            using (var scope = tracer.StartActiveSpan(SpanName, parentContext, SpanKind.Producer, out var ispan))
            {
                Assert.NotNull(scope);

                var span = (Span)tracer.CurrentSpan;

                Assert.True(span.Context.IsValid);
                Assert.Same(ispan, span);
                Assert.NotNull(span);
                Assert.NotSame(BlankSpan.Instance, tracer.CurrentSpan);
                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.Equal(parentContext.SpanId, ((Span)span).ParentSpanId);
            }

            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
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

                var span = (Span)tracer.CurrentSpan;
                Assert.NotNull(span);
                Assert.Same(ispan, span);
                Assert.True(span.Context.IsValid);
                Assert.NotSame(BlankSpan.Instance, tracer.CurrentSpan);
                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Equal(parentContext.SpanId, ((Span)span).ParentSpanId);
            }

            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
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

                var span = (Span)tracer.CurrentSpan;
                Assert.NotNull(span);
                Assert.Same(ispan, span);
                Assert.True(span.Context.IsValid);
                Assert.NotSame(BlankSpan.Instance, tracer.CurrentSpan);
                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Single(span.Links);
                Assert.Equal(parentContext.SpanId, ((Span)span).ParentSpanId);
            }

            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
        }

        [Fact]
        public void StartActiveSpan_ImplicitParentSpan()
        {
            var tracer = tracerFactory.GetTracer(null);

            using (var parentScope = tracer.StartActiveSpan(SpanName, out var parentSpan))
            using (var scope = tracer.StartActiveSpan(SpanName, out var childSpan))
            {
                Assert.NotNull(scope);

                var span = tracer.CurrentSpan;
                Assert.Same(childSpan, span);
                Assert.NotNull(span);
                Assert.NotSame(BlankSpan.Instance, tracer.CurrentSpan);
                Assert.True(span.Context.IsValid);
                Assert.NotEqual(default, ((Span)span).ParentSpanId);
            }

            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
        }

        [Fact]
        public void StartActiveSpan_ImplicitParentSpan_Kind()
        {
            var tracer = tracerFactory.GetTracer(null);

            using (var parentScope = tracer.StartActiveSpan(SpanName, out var parentSpan))
            using (var scope = tracer.StartActiveSpan(SpanName, SpanKind.Producer, out var childSpan))
            {
                Assert.NotNull(scope);

                var span = (Span)tracer.CurrentSpan;

                Assert.True(span.Context.IsValid);
                Assert.NotNull(span);
                Assert.Same(childSpan, span);
                Assert.NotSame(BlankSpan.Instance, tracer.CurrentSpan);
                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.NotEqual(default, ((Span)span).ParentSpanId);
            }

            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
        }


        [Fact]
        public void StartActiveSpan_ImplicitParentSpan_Kind_Timestamp()
        {
            var tracer = tracerFactory.GetTracer(null);

            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            using (var parentScope = tracer.StartActiveSpan(SpanName, out var parentspan))
            using (var scope = tracer.StartActiveSpan(SpanName, SpanKind.Producer, 
                new SpanCreationOptions { StartTimestamp = startTimestamp }, out var childSpan))
            {
                Assert.NotNull(scope);

                var span = (Span)tracer.CurrentSpan;
                Assert.NotNull(span);
                Assert.Same(childSpan, span);
                Assert.True(span.Context.IsValid);
                Assert.NotSame(BlankSpan.Instance, tracer.CurrentSpan);
                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.NotEqual(default, ((Span)span).ParentSpanId);
            }

            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
        }

        [Fact]
        public void StartActiveSpan_ImplicitParentSpan_Kind_Timestamp_Links()
        {
            var tracer = tracerFactory.GetTracer(null);

            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var startTimestamp = DateTimeOffset.Now.AddSeconds(-1);
            using (var parentScope = tracer.StartActiveSpan(SpanName, out _))
            using (var scope = tracer.StartActiveSpan(SpanName, SpanKind.Producer, 
                new SpanCreationOptions { StartTimestamp = startTimestamp, Links = new[] { new Link(linkContext) }, }, out var ispan))
            {
                Assert.NotNull(scope);

                var span = (Span)tracer.CurrentSpan;
                Assert.NotNull(span);
                Assert.Same(ispan, span);
                Assert.True(span.Context.IsValid);
                Assert.NotSame(BlankSpan.Instance, tracer.CurrentSpan);
                Assert.Equal(SpanKind.Producer, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Single(span.Links);
                Assert.NotEqual(default, ((Span)span).ParentSpanId);
            }

            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
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
                Assert.NotSame(BlankSpan.Instance, tracer.CurrentSpan);
                Assert.True(span.Context.IsValid);
                Assert.Equal(activity.SpanId, span.Context.SpanId);
                Assert.Equal(SpanKind.Internal, ((Span)span).Kind);
                Assert.Empty(((Span)span).Links);
            }

            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
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
                Assert.NotSame(BlankSpan.Instance, tracer.CurrentSpan);
                Assert.True(span.Context.IsValid);
                Assert.Equal(activity.SpanId, span.Context.SpanId);
                Assert.Equal(SpanKind.Consumer, ((Span)span).Kind);
                Assert.Empty(((Span)span).Links);
            }

            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
        }

        [Fact]
        public void StartActiveSpan_FromActivity_Kind_Links()
        {
            var tracer = tracerFactory.GetTracer(null);

            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var activity = new Activity(SpanName).SetIdFormat(ActivityIdFormat.W3C).Start();
            using (var scope = tracer.StartActiveSpanFromActivity(SpanName, activity, SpanKind.Consumer, new [] { new Link(linkContext),  }, out var ispan))
            {
                Assert.NotNull(scope);

                var span = tracer.CurrentSpan;
                Assert.NotNull(span);
                Assert.Same(ispan, span);
                Assert.NotSame(BlankSpan.Instance, tracer.CurrentSpan);
                Assert.True(span.Context.IsValid);
                Assert.Equal(activity.SpanId, span.Context.SpanId);
                Assert.Equal(SpanKind.Consumer, ((Span)span).Kind);
                Assert.Single(((Span)span).Links);
            }

            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
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
                It.IsAny<SpanContext>(),
                It.IsAny<ActivityTraceId>(),
                It.IsAny<ActivitySpanId>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IEnumerable<Link>>())).Returns(new Decision(true));

            var span = (Span)tracer.StartSpan("test", SpanKind.Client, new SpanCreationOptions { Attributes = this.attributes, });
            span.Attributes.AssertAreSame(this.attributes);

            samplerMock.Verify(o => o.ShouldSample(
                It.IsAny<SpanContext>(), 
                It.IsAny<ActivityTraceId>(), 
                It.IsAny<ActivitySpanId>(),
                It.IsAny<string>(),
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
                It.IsAny<SpanContext>(),
                It.IsAny<ActivityTraceId>(),
                It.IsAny<ActivitySpanId>(),
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IEnumerable<Link>>())).Returns(new Decision(false));

            var span = (Span)tracer.StartSpan("test", SpanKind.Client, new SpanCreationOptions { Attributes = this.attributes, });
            Assert.Empty(span.Attributes);

            samplerMock.Verify(o => o.ShouldSample(
                It.IsAny<SpanContext>(),
                It.IsAny<ActivityTraceId>(),
                It.IsAny<ActivitySpanId>(),
                It.IsAny<string>(),
                It.Is<IDictionary<string, object>>(a => a == this.attributes),
                It.IsAny<IEnumerable<Link>>()), Times.Once);
        }

        private void AssertApproxSameTimestamp(DateTimeOffset one, DateTimeOffset two)
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
