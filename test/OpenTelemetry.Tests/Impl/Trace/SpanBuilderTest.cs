// <copyright file="SpanBuilderTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Test
{
    using System;
    using System.Diagnostics;
    using System.Collections.Generic;
    using System.Linq;
    using Moq;
    using OpenTelemetry.Abstractions.Utils;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Trace.Sampler;
    using Xunit;

    public class SpanBuilderTest : IDisposable
    {
        private static readonly string SpanName = "MySpanName";

        private readonly TraceConfig alwaysSampleTraceConfig = new TraceConfig(Samplers.AlwaysSample);

        private readonly SpanProcessor spanProcessor = new SimpleSpanProcessor(new NoopSpanExporter());

        private readonly ITracer tracer;
        public SpanBuilderTest()
        {
            tracer = new Tracer(spanProcessor, alwaysSampleTraceConfig);
        }

        [Fact]
        public void StartSpanNullParent()
        {
            var span = (Span) new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetNoParent()
                .StartSpan();
            Assert.True(span.Context.IsValid);
            Assert.True(span.IsRecordingEvents);
            Assert.True((span.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            var spanData = ((Span)span);
            Assert.True(spanData.ParentSpanId == default);
            Assert.InRange(spanData.StartTimestamp, PreciseTimestamp.GetUtcNow().AddSeconds(-1), PreciseTimestamp.GetUtcNow().AddSeconds(1));
            Assert.Equal(SpanName, spanData.Name);

            var activity = ((Span)span).Activity;
            Assert.Null(Activity.Current);
            Assert.Equal(activity.TraceId, span.Context.TraceId);
            Assert.Equal(activity.SpanId, span.Context.SpanId);
        }

        [Fact]
        public void StartSpanLastParentWins1()
        {
            var spanContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.None, Tracestate.Empty);

            var span = (Span)new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetNoParent()
                .SetParent(spanContext)
                .StartSpan();

            Assert.True(span.Context.IsValid);
            Assert.Equal(spanContext.TraceId, span.Context.TraceId);
            Assert.Equal(spanContext.SpanId, span.ParentSpanId);
        }

        [Fact]
        public void StartSpanLastParentWins2()
        {
            var spanContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.None, Tracestate.Empty);

            var span = (Span)new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetParent(spanContext)
                .SetNoParent()
                .StartSpan();

            Assert.True(span.Context.IsValid);
            Assert.NotEqual(spanContext.TraceId, span.Context.TraceId);
            Assert.True(span.ParentSpanId == default);
        }

        [Fact]
        public void StartSpanLastParentWins3()
        {
            var spanContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.None, Tracestate.Empty);
            var rootSpan = (Span)new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .StartSpan();

            var childSpan = (Span)new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetParent(spanContext)
                .SetParent(rootSpan)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(rootSpan.Context.TraceId, childSpan.Context.TraceId);
            Assert.Equal(rootSpan.Context.SpanId, childSpan.ParentSpanId);
        }

        [Fact]
        public void StartSpanLastParentWins4()
        {
            var spanContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.None, Tracestate.Empty);
            var rootSpan = (Span)new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .StartSpan();

            var childSpan = (Span)new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetParent(rootSpan)
                .SetParent(spanContext)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(spanContext.TraceId, childSpan.Context.TraceId);
            Assert.Equal(spanContext.SpanId, childSpan.ParentSpanId);
        }

        [Fact]
        public void StartSpanLastParentWins5()
        {
            var spanContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.None, Tracestate.Empty);
            var activity = new Activity("foo")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();

            var childSpan = (Span)new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetParent(spanContext)
                .SetParent(activity)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(activity.TraceId, childSpan.Context.TraceId);
            Assert.Equal(activity.SpanId, childSpan.ParentSpanId);
        }

        [Fact]
        public void StartSpanLastParentWins6()
        {
            var spanContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.None, Tracestate.Empty);
            var activity = new Activity("foo")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();

            var childSpan = (Span)new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetParent(spanContext)
                .SetCreateChild(false)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(activity.TraceId, childSpan.Context.TraceId);
            Assert.Equal(activity.SpanId, childSpan.Context.SpanId);
        }

        [Fact]
        public void StartSpanLastParentWins7()
        {
            var spanContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.None, Tracestate.Empty);
            var activity = new Activity("foo")
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();

            var childSpan = (Span)new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetCreateChild(false)
                .SetParent(spanContext)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(spanContext.TraceId, childSpan.Context.TraceId);
            Assert.Equal(spanContext.SpanId, childSpan.ParentSpanId);
        }

        [Fact]
        public void StartSpanNullParentWithRecordEvents()
        {
            var span = (Span) new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.NeverSample)
                .SetRecordEvents(true)
                .SetNoParent()
                .StartSpan();
            Assert.True(span.Context.IsValid);
            Assert.True(span.IsRecordingEvents);
            Assert.True((span.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            Assert.True(span.ParentSpanId == default);
        }

        [Fact]
        public void StartSpanWithStartTimestamp()
        {
            var timestamp = DateTime.UtcNow.AddSeconds(-100);
            var span = (Span) new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.AlwaysSample)
                .SetStartTimestamp(timestamp)
                .StartSpan();

            Assert.Equal(timestamp, span.StartTimestamp);
        }

        [Fact]
        public void StartSpanWithImplicitTimestamp()
        {
            var timestamp = PreciseTimestamp.GetUtcNow();
            var span = (Span) new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.AlwaysSample)
                .StartSpan();

            Assert.InRange(Math.Abs((timestamp - span.StartTimestamp).TotalMilliseconds), 0, 20);
        }

        [Fact]
        public void StartSpanNullParentNoRecordOptions()
        {
            var span = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.NeverSample)
                .SetNoParent()
                .StartSpan();
            Assert.True(span.Context.IsValid);
            Assert.False(span.IsRecordingEvents);
            Assert.True((span.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
        }

        [Fact]
        public void StartChildSpan()
        {
            var rootSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetNoParent()
                .StartSpan();

            Assert.True(rootSpan.Context.IsValid);
            Assert.True(rootSpan.IsRecordingEvents);
            Assert.True((rootSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);

            var childSpan = (Span) new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetParent(rootSpan)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(rootSpan.Context.TraceId, childSpan.Context.TraceId);
            Assert.Equal(rootSpan.Context.SpanId, childSpan.ParentSpanId);
        }

        [Fact]
        public void StartSpanInScopeOfCurrentActivity()
        {
            var parentActivity = new Activity(SpanName)
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            parentActivity.TraceStateString = "k1=v1,k2=v2";

            var childSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(parentActivity.TraceId, childSpan.Context.TraceId);
            Assert.Equal(parentActivity.SpanId, ((Span)childSpan).ParentSpanId);

            var activity = ((Span)childSpan).Activity;
            Assert.Equal(parentActivity, Activity.Current);
            Assert.Equal(activity.Parent, parentActivity);

            Assert.Equal("k1=v1,k2=v2", childSpan.Context.Tracestate.ToString());
        }

        [Fact]
        public void StartSpanInScopeOfCurrentActivityRecorded()
        {
            var parentActivity = new Activity(SpanName)
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            parentActivity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var childSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(parentActivity.TraceId, childSpan.Context.TraceId);
            Assert.Equal(parentActivity.SpanId, ((Span)childSpan).ParentSpanId);
            Assert.True((childSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            parentActivity.Stop();
        }

        [Fact]
        public void StartSpanInScopeOfCurrentActivityNoParent()
        {
            var parentActivity = new Activity(SpanName).Start();
            parentActivity.TraceStateString = "k1=v1,k2=v2";

            var childSpan = (Span) new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetNoParent()
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.NotEqual(parentActivity.TraceId, childSpan.Context.TraceId);
            Assert.True(childSpan.ParentSpanId == default);

            var activity = ((Span)childSpan).Activity;
            Assert.Equal(parentActivity, Activity.Current);
            Assert.Null(activity.Parent);
            Assert.Equal(activity.TraceId, childSpan.Context.TraceId);
            Assert.Equal(activity.SpanId, childSpan.Context.SpanId);
            Assert.Empty(childSpan.Context.Tracestate.Entries);
        }

        [Fact]
        public void StartSpanFromExplicitActivity()
        {
            var parentActivity = new Activity(SpanName)
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            parentActivity.TraceStateString = "k1=v1,k2=v2";
            parentActivity.Stop();

            var childSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetParent(parentActivity)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(parentActivity.TraceId, childSpan.Context.TraceId);
            Assert.Equal(parentActivity.SpanId, ((Span)childSpan).ParentSpanId);

            var activity = ((Span)childSpan).Activity;
            Assert.NotNull(activity);
            Assert.Null(Activity.Current);
            Assert.Equal(activity.TraceId, parentActivity.TraceId);
            Assert.Equal(activity.ParentSpanId, parentActivity.SpanId);
            Assert.Equal(activity.SpanId, childSpan.Context.SpanId);
            Assert.Equal("k1=v1,k2=v2", childSpan.Context.Tracestate.ToString());
        }

        [Fact]
        public void StartSpanFromExplicitRecordedActivity()
        {
            var parentActivity = new Activity(SpanName)
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            parentActivity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            parentActivity.Stop();

            var childSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetParent(parentActivity)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(parentActivity.TraceId, childSpan.Context.TraceId);
            Assert.Equal(parentActivity.SpanId, ((Span)childSpan).ParentSpanId);
            Assert.True((childSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
        }

        [Fact]
        public void StartSpanFromCurrentActivity()
        {
            var activity = new Activity(SpanName)
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            activity.TraceStateString = "k1=v1,k2=v2";

            var span = (Span) new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetCreateChild(false)
                .StartSpan();

            Assert.True(span.Context.IsValid);
            Assert.Equal(activity.TraceId, span.Context.TraceId);
            Assert.Equal(activity.SpanId, span.Context.SpanId);
            Assert.True(((Span)span).ParentSpanId == default);

            Assert.NotNull(Activity.Current);
            Assert.Equal(Activity.Current, activity);
            Assert.Equal("k1=v1,k2=v2", span.Context.Tracestate.ToString());

            Assert.Equal(activity.StartTimeUtc, span.StartTimestamp);
        }

        [Fact]
        public void StartSpanFromCurrentRecordedActivity()
        {
            var activity = new Activity(SpanName)
                .SetIdFormat(ActivityIdFormat.W3C)
                .Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var span = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetCreateChild(false)
                .StartSpan();

            Assert.True(span.Context.IsValid);
            Assert.Equal(activity.TraceId, span.Context.TraceId);
            Assert.Equal(activity.SpanId, span.Context.SpanId);
            Assert.True(((Span)span).ParentSpanId == default);
            Assert.True((span.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            activity.Stop();
        }

        [Fact]
        public void StartSpan_ExplicitNoParent()
        {
            var span = (Span) new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetNoParent()
                .StartSpan();

            Assert.True(span.Context.IsValid);
            Assert.True(span.IsRecordingEvents);
            Assert.True((span.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);

            Assert.True(span.ParentSpanId == default);

            var activity = ((Span)span).Activity;
            Assert.Null(Activity.Current);
            Assert.Equal(activity.TraceId, span.Context.TraceId);
            Assert.Equal(activity.SpanId, span.Context.SpanId);
            Assert.Empty(span.Context.Tracestate.Entries);
        }

        [Fact]
        public void StartSpan_NoParent()
        {
            var span = (Span) new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .StartSpan();

            Assert.True(span.Context.IsValid);
            Assert.True(span.IsRecordingEvents);
            Assert.True((span.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            Assert.True(span.ParentSpanId == default);
        }

        [Fact]
        public void StartSpan_BlankSpanParent()
        {
            var span = (Span) new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetParent(BlankSpan.Instance)
                .StartSpan();

            Assert.True(span.Context.IsValid);
            Assert.True(span.IsRecordingEvents);
            Assert.True((span.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            Assert.True(span.ParentSpanId == default);
        }

        [Fact]
        public void StartSpan_BlankSpanContextParent()
        {
            var span = (Span) new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetParent(SpanContext.Blank)
                .StartSpan();

            Assert.True(span.Context.IsValid);
            Assert.True(span.IsRecordingEvents);
            Assert.True((span.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);

            Assert.True(span.ParentSpanId == default);
        }


        [Fact]
        public void StartSpan_CurrentSpanParent()
        {
            var rootSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetParent(
                    SpanContext.Create(
                        ActivityTraceId.CreateRandom(), 
                        ActivitySpanId.CreateRandom(),
                        ActivityTraceFlags.None, 
                        Tracestate.Builder.Set("k1", "v1").Build()))
                .StartSpan();
            using (tracer.WithSpan(rootSpan))
            {
                var childSpan = (Span)new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                    .StartSpan();

                Assert.True(childSpan.Context.IsValid);
                Assert.Equal(rootSpan.Context.TraceId, childSpan.Context.TraceId);
                Assert.Equal(rootSpan.Context.SpanId, childSpan.ParentSpanId);
                Assert.Equal("k1=v1", childSpan.Context.Tracestate.ToString());
            }
        }

        [Fact]
        public void StartSpan_NoParentInScopeOfCurrentSpan()
        {
            var rootSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .StartSpan();
            using (tracer.WithSpan(rootSpan))
            {
                var childSpan = (Span)new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                    .SetNoParent()
                    .StartSpan();

                Assert.True(childSpan.Context.IsValid);
                Assert.NotEqual(rootSpan.Context.TraceId, childSpan.Context.TraceId);
                Assert.True(childSpan.ParentSpanId == default);
            }
        }

        [Fact]
        public void StartSpanInvalidParent()
        {
            var span = (Span) new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetParent(SpanContext.Blank)
                .StartSpan();
            Assert.True(span.Context.IsValid);
            Assert.True(span.IsRecordingEvents);
            Assert.True((span.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);

            Assert.True(span.ParentSpanId == default);
        }

        [Fact]
        public void StartRemoteSpan()
        {
            var spanContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.None,
                    Tracestate.Builder.Set("k1", "v1").Build());

            var span = (Span) new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetParent(spanContext)
                .SetRecordEvents(true)
                .StartSpan();

            Assert.True(span.Context.IsValid);
            Assert.Equal(spanContext.TraceId, span.Context.TraceId);
            Assert.True((span.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            
            Assert.Equal(spanContext.SpanId, span.ParentSpanId);
            Assert.Equal("k1=v1", span.Context.Tracestate.ToString());
        }

        [Fact]
        public void StartSpan_WithLink()
        {
            var link = Link.FromSpanContext(
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.None, Tracestate.Empty));

            var span = (Span) new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .AddLink(link)
                .StartSpan();

            var links = span.Links.ToArray();

            Assert.Single(links);

            Assert.Equal(link.Context.TraceId, links[0].Context.TraceId);
            Assert.Equal(link.Context.SpanId, links[0].Context.SpanId);
            Assert.Equal(link.Context.TraceOptions, links[0].Context.TraceOptions);
            Assert.Equal(link.Context.Tracestate, links[0].Context.Tracestate);
            Assert.Equal(0, links[0].Attributes.Count);
        }

        [Fact]
        public void StartSpan_WithLinkFromActivity()
        {
            var contextLink = SpanContext.Create(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None, Tracestate.Empty);

            var span = (Span) new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .AddLink(contextLink)
                .StartSpan();

            var links = span.Links.ToArray();

            Assert.Single(links);

            Assert.NotEqual(default, contextLink.TraceId);
            Assert.NotEqual(default, contextLink.SpanId);
            Assert.Equal(contextLink.TraceId, links[0].Context.TraceId);
            Assert.Equal(contextLink.SpanId, links[0].Context.SpanId);
            Assert.Equal(contextLink.TraceOptions, links[0].Context.TraceOptions);
            Assert.Empty(links[0].Context.Tracestate.Entries);
            Assert.Equal(0, links[0].Attributes.Count);
        }

        [Fact]
        public void StartSpan_WithLinkFromSpanContextAndAttributes()
        {
            var linkContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.None, Tracestate.Empty);

            var span = (Span) new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .AddLink(linkContext, new Dictionary<string, object> { ["k"] = "v", })
                .StartSpan();

            var links = span.Links.ToArray();

            Assert.Single(links);

            Assert.Equal(linkContext.TraceId, links[0].Context.TraceId);
            Assert.Equal(linkContext.SpanId, links[0].Context.SpanId);
            Assert.Equal(linkContext.TraceOptions, links[0].Context.TraceOptions);
            Assert.Equal(linkContext.Tracestate, links[0].Context.Tracestate);
            Assert.Equal(1, links[0].Attributes.Count);
            Assert.True(links[0].Attributes.ContainsKey("k"));
            Assert.Equal("v", links[0].Attributes["k"].ToString());
        }

        [Fact]
        public void StartSpan_WithLinkFromSpanContext()
        {
            var linkContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.None, Tracestate.Empty);

            var span = (Span) new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .AddLink(linkContext)
                .StartSpan();

            var links = span.Links.ToArray();

            Assert.Single(links);

            Assert.Equal(linkContext.TraceId, links[0].Context.TraceId);
            Assert.Equal(linkContext.SpanId, links[0].Context.SpanId);
            Assert.Equal(linkContext.TraceOptions, links[0].Context.TraceOptions);
            Assert.Equal(linkContext.Tracestate, links[0].Context.Tracestate);
        }

        [Fact]
        public void StartRootSpan_WithSpecifiedSampler()
        {
            // Apply given sampler before default sampler for root spans.
            var rootSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetNoParent()
                .SetSampler(Samplers.NeverSample)
                .StartSpan();

            Assert.True(rootSpan.Context.IsValid);
            Assert.True((rootSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
        }

        [Fact]
        public void StartRootSpan_WithoutSpecifiedSampler()
        {
            // Apply default sampler (always true in the tests) for root spans.
            var rootSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetNoParent()
                .StartSpan();

            Assert.True(rootSpan.Context.IsValid);
            Assert.True((rootSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
        }

        [Fact]
        public void StartRemoteChildSpan_WithSpecifiedSampler()
        {
            var rootSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.AlwaysSample)
                .SetNoParent()
                .StartSpan();

            Assert.True(rootSpan.Context.IsValid);
            Assert.True((rootSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            // Apply given sampler before default sampler for spans with remote parent.
            var childSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.NeverSample)
                .SetParent(rootSpan.Context)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(rootSpan.Context.TraceId, childSpan.Context.TraceId);
            Assert.True((childSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
        }

        [Fact]
        public void StartRemoteChildSpan_WithoutSpecifiedSampler()
        {
            var rootSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.NeverSample)
                .SetNoParent()
                .StartSpan();

            Assert.True(rootSpan.Context.IsValid);
            Assert.True((rootSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
            // Apply default sampler (always true in the tests) for spans with remote parent.
            var childSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetParent(rootSpan.Context)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(rootSpan.Context.TraceId, childSpan.Context.TraceId);
            Assert.True((childSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
        }

        [Fact]
        public void StartChildSpan_WithSpecifiedSampler()
        {
            var rootSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.AlwaysSample)
                .SetNoParent()
                .StartSpan();

            Assert.True(rootSpan.Context.IsValid);
            Assert.True((rootSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            // Apply the given sampler for child spans.

            var childSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.NeverSample)
                .SetParent(rootSpan)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(rootSpan.Context.TraceId, childSpan.Context.TraceId);
            Assert.True((childSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
        }

        [Fact]
        public void StartChildSpan_WithoutSpecifiedSampler()
        {
            var rootSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.NeverSample)
                .SetNoParent()
                .StartSpan();

            Assert.True(rootSpan.Context.IsValid);
            Assert.True((rootSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);

            // Don't apply the default sampler (always true) for child spans.
            var childSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetParent(rootSpan)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(rootSpan.Context.TraceId, childSpan.Context.TraceId);
            Assert.True((childSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
        }

        [Fact]
        public void StartChildSpan_SampledLinkedParent()
        {
            var rootSpanUnsampled = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .SetSampler(Samplers.NeverSample)
                .SetNoParent()
                .StartSpan();

            Assert.True((rootSpanUnsampled.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
            var rootSpanSampled =
                new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                    .SetSpanKind(SpanKind.Internal)
                    .SetSampler(Samplers.AlwaysSample)
                    .SetNoParent()
                    .StartSpan();

            Assert.True((rootSpanSampled.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            // Sampled because the linked parent is sampled.
            var childSpan = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                .SetSpanKind(SpanKind.Internal)
                .AddLink(Link.FromSpanContext(rootSpanSampled.Context))
                .SetParent(rootSpanUnsampled)
                .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(rootSpanUnsampled.Context.TraceId, childSpan.Context.TraceId);
            Assert.True((childSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
        }

        [Fact]
        public void StartRemoteChildSpan_WithProbabilitySamplerDefaultSampler()
        {
            // This traceId will not be sampled by the ProbabilitySampler because the first 8 bytes as long
            // is not less than probability * Long.MAX_VALUE;
            var traceId =
                ActivityTraceId.CreateFromBytes(
                    new byte[] {0x8F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0,});

            // If parent is sampled then the remote child must be sampled.
            var childSpan =
                new SpanBuilder(SpanName, spanProcessor, new TraceConfig(ProbabilitySampler.Create(0.1)))
                    .SetSpanKind(SpanKind.Internal)
                    .SetParent(SpanContext.Create(
                        traceId,
                        ActivitySpanId.CreateRandom(),
                        ActivityTraceFlags.Recorded,
                        Tracestate.Empty))
                    .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(traceId, childSpan.Context.TraceId);
            Assert.True((childSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) != 0);
            childSpan.End();

            // If parent is not sampled then the remote child must be not sampled.
            childSpan =
                new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig)
                    .SetSpanKind(SpanKind.Internal)
                    .SetParent(SpanContext.Create(
                        traceId,
                        ActivitySpanId.CreateRandom(),
                        ActivityTraceFlags.None,
                        Tracestate.Empty))
                    .StartSpan();

            Assert.True(childSpan.Context.IsValid);
            Assert.Equal(traceId, childSpan.Context.TraceId);
            Assert.True((childSpan.Context.TraceOptions & ActivityTraceFlags.Recorded) == 0);
            childSpan.End();
        }

        [Fact]
        public void SpanBuilder_BadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new SpanBuilder(null, spanProcessor, alwaysSampleTraceConfig));
            Assert.Throws<ArgumentNullException>(() => new SpanBuilder(SpanName, null, alwaysSampleTraceConfig));
            Assert.Throws<ArgumentNullException>(() => new SpanBuilder(SpanName, spanProcessor, null));

            var spanBuilder = new SpanBuilder(SpanName, spanProcessor, alwaysSampleTraceConfig);
            Assert.Throws<ArgumentNullException>(() => spanBuilder.SetParent((ISpan)null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.SetParent((SpanContext)null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.SetParent((Activity)null));

            // no Activity.Current
            Assert.Throws<ArgumentException>(() => spanBuilder.SetCreateChild(false));

            // Activity.Current wrong format
            var a = new Activity("foo")
                .SetIdFormat(ActivityIdFormat.Hierarchical)
                .Start(); 
            Assert.Throws<ArgumentException>(() => spanBuilder.SetCreateChild(false));
            a.Stop();

            Assert.Throws<ArgumentNullException>(() => spanBuilder.SetSampler(null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.AddLink((ILink)null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.AddLink((SpanContext)null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.AddLink(null, null));
            Assert.Throws<ArgumentNullException>(() => spanBuilder.AddLink(SpanContext.Blank, null));
        }

        public void Dispose()
        {
            Activity.Current = null;
        }
    }
}
