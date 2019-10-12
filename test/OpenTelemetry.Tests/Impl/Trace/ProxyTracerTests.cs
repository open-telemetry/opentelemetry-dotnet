// <copyright file="ProxyTracerTests.cs" company="OpenTelemetry Authors">
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

using System;
using System.Diagnostics;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Utils;

namespace OpenTelemetry.Tests.Impl.Trace
{
    using System.Linq;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Trace;
    using Xunit;

    public class ProxyTracerTests
    {
        [Fact]
        public void ProxyTracer_CurrentSpan()
        {
            Assert.Same(BlankSpan.Instance, new ProxyTracer().CurrentSpan);
        }

        [Fact]
        public void ProxyTracer_WithSpan()
        {
            var noopScope = new ProxyTracer().WithSpan(BlankSpan.Instance);
            Assert.NotNull(noopScope);
            // does not throw
            noopScope.Dispose();
        }

        [Fact]
        public void ProxyTracer_CreateSpan_BadArgs()
        {
            var proxyTracer = new ProxyTracer();
            
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartRootSpan(null));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartRootSpan(null, SpanKind.Client));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartRootSpan(null, SpanKind.Client, default));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartRootSpan(null, SpanKind.Client, default, null));

            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, SpanKind.Client));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, SpanKind.Client, default));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, SpanKind.Client, default, null));

            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, BlankSpan.Instance));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, BlankSpan.Instance, SpanKind.Client));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, BlankSpan.Instance, SpanKind.Client, default));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, BlankSpan.Instance, SpanKind.Client, default, null));

            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, SpanContext.Blank));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, SpanContext.Blank, SpanKind.Client));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, SpanContext.Blank, SpanKind.Client, default));
            Assert.Throws<ArgumentNullException>(() => proxyTracer.StartSpan(null, SpanContext.Blank, SpanKind.Client, default, null));

            Assert.Throws<ArgumentNullException>(() =>
                proxyTracer.StartSpanFromActivity(null, new Activity("foo").Start()));

            Assert.Throws<ArgumentNullException>(() =>
                proxyTracer.StartSpanFromActivity("foo", null));

            Assert.Throws<ArgumentException>(() =>
                proxyTracer.StartSpanFromActivity("foo", new Activity("foo")));

            Assert.Throws<ArgumentException>(() => proxyTracer.StartSpanFromActivity(
                    "foo", 
                    new Activity("foo").SetIdFormat(ActivityIdFormat.Hierarchical).Start()));
        }

        [Fact]
        public void ProxyTracer_CreateSpan_ValidArgs()
        {
            var proxyTracer = new ProxyTracer();
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartRootSpan("foo"));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartRootSpan("foo", SpanKind.Client));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartRootSpan("foo", SpanKind.Client, default));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartRootSpan("foo", SpanKind.Client, default, null));

            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo"));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", SpanKind.Client));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", SpanKind.Client, default));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", SpanKind.Client, default, null));

            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", BlankSpan.Instance));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", BlankSpan.Instance, SpanKind.Client));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", BlankSpan.Instance, SpanKind.Client, default));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", BlankSpan.Instance, SpanKind.Client, default, null));

            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", SpanContext.Blank));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", SpanContext.Blank, SpanKind.Client));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", SpanContext.Blank, SpanKind.Client, default));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpan("foo", SpanContext.Blank, SpanKind.Client, default, null));

            var validActivity = new Activity("foo").SetIdFormat(ActivityIdFormat.W3C).Start();
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpanFromActivity("foo", validActivity));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpanFromActivity("foo", validActivity, SpanKind.Consumer));
            Assert.Equal(BlankSpan.Instance, proxyTracer.StartSpanFromActivity("foo", validActivity, SpanKind.Client, null));
        }

        [Fact]
        public void ProxyTracer_UpdateTracer_StartRootSpanFrom_Kind_Timestamp_Links()
        {
            var proxyTracer = new ProxyTracer();
            var realTracer = TracerFactory.Create(b => { }).GetTracer(null);
            proxyTracer.UpdateTracer(realTracer);
            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var startTimestamp = DateTimeOffset.UtcNow.AddSeconds(-10);
            var span = (Span)proxyTracer.StartRootSpan("foo", SpanKind.Server, startTimestamp, new[] { new Link(linkContext) });

            Assert.True(span.Context.IsValid);
            Assert.Equal(span.Activity.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(default, span.ParentSpanId);
            Assert.Equal(span.Activity.ActivityTraceFlags, span.Context.TraceOptions);
            Assert.Empty(span.Context.Tracestate);

            Assert.True(span.IsRecordingEvents);

            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(startTimestamp, span.StartTimestamp);
            Assert.Single(span.Links);
            Assert.Same(linkContext, span.Links.Single().Context);
        }

        [Fact]
        public void ProxyTracer_UpdateTracer_StartSpanFrom_ParentSpan_Kind_Timestamp_Links()
        {
            var proxyTracer = new ProxyTracer();
            var realTracer = TracerFactory.Create(b => { }).GetTracer(null);
            proxyTracer.UpdateTracer(realTracer);

            var parentSpan = (Span)proxyTracer.StartSpan("parent");

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var span = (Span)proxyTracer.StartSpan("child", parentSpan, SpanKind.Server, startTimestamp, new[] { new Link(linkContext) });

            Assert.True(span.Context.IsValid);
            Assert.Equal(parentSpan.Context.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(parentSpan.Context.SpanId, span.ParentSpanId);
            Assert.Equal(parentSpan.Context.TraceOptions, span.Context.TraceOptions);

            Assert.True(span.IsRecordingEvents);

            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(startTimestamp, span.StartTimestamp);
            Assert.Single(span.Links);
            Assert.Same(linkContext, span.Links.Single().Context);
        }

        [Fact]
        public void ProxyTracer_UpdateTracer_StartSpanFrom_ParentContext_Kind_Timestamp_Links()
        {
            var proxyTracer = new ProxyTracer();
            var realTracer = TracerFactory.Create(b => { }).GetTracer(null);
            proxyTracer.UpdateTracer(realTracer);

            var parentSpanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var span = (Span)proxyTracer.StartSpan("child", parentSpanContext, SpanKind.Client, startTimestamp, new[] { new Link(linkContext) });

            Assert.True(span.Context.IsValid);
            Assert.Equal(parentSpanContext.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(parentSpanContext.SpanId, span.ParentSpanId);
            Assert.Equal(parentSpanContext.TraceOptions, span.Context.TraceOptions);

            Assert.True(span.IsRecordingEvents);

            Assert.Equal(SpanKind.Client, span.Kind);
            Assert.Equal(startTimestamp, span.StartTimestamp);
            Assert.Single(span.Links);
            Assert.Same(linkContext, span.Links.Single().Context);
        }


        [Fact]
        public void ProxyTracer_UpdateTracer_StartSpanFrom_FromActivity_Kind_Links()
        {
            var proxyTracer = new ProxyTracer();
            var realTracer = TracerFactory.Create(b => { }).GetTracer(null);
            proxyTracer.UpdateTracer(realTracer);

            var activity = new Activity("foo").SetIdFormat(ActivityIdFormat.W3C).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var span = (Span)proxyTracer.StartSpanFromActivity("foo", activity, SpanKind.Server, new[] { new Link(linkContext) });

            Assert.Equal(activity.TraceId, span.Context.TraceId);
            Assert.Equal(activity.SpanId, span.Context.SpanId);
            Assert.Equal(activity.ParentSpanId, span.ParentSpanId);
            Assert.Equal(activity.ActivityTraceFlags, span.Context.TraceOptions);

            Assert.True(span.IsRecordingEvents);
            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(activity.StartTimeUtc, span.StartTimestamp.DateTime);
            Assert.Equal(activity, span.Activity);
            Assert.Single(span.Links);
            Assert.Same(linkContext, span.Links.Single().Context);
        }

        [Fact]
        public void ProxyTracer_UpdateTracer_StartSpanFrom_ImplicitParent_Kind_Timestamp_Links()
        {
            var proxyTracer = new ProxyTracer();
            var realTracer = TracerFactory.Create(b => { }).GetTracer(null);
            proxyTracer.UpdateTracer(realTracer);

            var parentSpan = (Span)proxyTracer.StartSpan("parent");
            using (proxyTracer.WithSpan(parentSpan))
            {
                var startTimestamp = PreciseTimestamp.GetUtcNow();
                var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.Recorded);
                var span = (Span)proxyTracer.StartSpan("child", SpanKind.Consumer, startTimestamp,
                    new[] {new Link(linkContext)});

                Assert.True(span.Context.IsValid);
                Assert.Equal(parentSpan.Context.TraceId, span.Context.TraceId);
                Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
                Assert.Equal(parentSpan.Context.SpanId, span.ParentSpanId);
                Assert.Equal(parentSpan.Context.TraceOptions, span.Context.TraceOptions);

                Assert.True(span.IsRecordingEvents);

                Assert.Equal(SpanKind.Consumer, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Single(span.Links);
                Assert.Same(linkContext, span.Links.Single().Context);
            }
        }

        [Fact]
        public void ProxyTracer_Formats()
        {
            Assert.NotNull(new ProxyTracer().TextFormat);
            Assert.NotNull(new ProxyTracer().BinaryFormat);
            Assert.IsAssignableFrom<ITextFormat>(new ProxyTracer().TextFormat);
            Assert.IsAssignableFrom<IBinaryFormat>(new ProxyTracer().BinaryFormat);
        }
    }
}

