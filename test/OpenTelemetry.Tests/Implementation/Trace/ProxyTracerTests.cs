// <copyright file="ProxyTracerTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Utils;
using Xunit;

namespace OpenTelemetry.Tests.Impl.Trace
{
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
        public void ProxyTracer_CreateSpan_BadArgs_DoesNotThrow()
        {
            var proxyTracer = new ProxyTracer();

            proxyTracer.StartRootSpan(null);
            proxyTracer.StartRootSpan(null, SpanKind.Client);
            proxyTracer.StartRootSpan(null, SpanKind.Client, default);
            proxyTracer.StartRootSpan(null, SpanKind.Client, null);

            proxyTracer.StartSpan(null);
            proxyTracer.StartSpan(null, SpanKind.Client);
            proxyTracer.StartSpan(null, SpanKind.Client, default);
            proxyTracer.StartSpan(null, SpanKind.Client, null);

            proxyTracer.StartSpan(null, BlankSpan.Instance);
            proxyTracer.StartSpan(null, BlankSpan.Instance, SpanKind.Client);
            proxyTracer.StartSpan(null, BlankSpan.Instance, SpanKind.Client, default);
            proxyTracer.StartSpan(null, BlankSpan.Instance, SpanKind.Client, null);

            var defaultContext = default(SpanContext);
            proxyTracer.StartSpan(null, defaultContext);
            proxyTracer.StartSpan(null, defaultContext, SpanKind.Client);
            proxyTracer.StartSpan(null, defaultContext, SpanKind.Client, default);
            proxyTracer.StartSpan(null, defaultContext, SpanKind.Client, null);

            proxyTracer.StartSpanFromActivity(null, new Activity("foo").Start());
            proxyTracer.StartSpanFromActivity(null, null);
            proxyTracer.StartSpanFromActivity(null, new Activity("foo"));
            proxyTracer.StartSpanFromActivity(null, new Activity("foo").SetIdFormat(ActivityIdFormat.Hierarchical).Start());
        }

        [Fact]
        public void ProxyTracer_CreateSpan_ValidArgs()
        {
            var proxyTracer = new ProxyTracer();
            Assert.Same(BlankSpan.Instance, proxyTracer.StartRootSpan("foo"));
            Assert.Same(BlankSpan.Instance, proxyTracer.StartRootSpan("foo", SpanKind.Client));
            Assert.Same(BlankSpan.Instance, proxyTracer.StartRootSpan("foo", SpanKind.Client, null));

            Assert.Same(BlankSpan.Instance, proxyTracer.StartSpan("foo"));
            Assert.Same(BlankSpan.Instance, proxyTracer.StartSpan("foo", SpanKind.Client));
            Assert.Same(BlankSpan.Instance, proxyTracer.StartSpan("foo", SpanKind.Client, null));

            Assert.Same(BlankSpan.Instance, proxyTracer.StartSpan("foo", BlankSpan.Instance));
            Assert.Same(BlankSpan.Instance, proxyTracer.StartSpan("foo", BlankSpan.Instance, SpanKind.Client));
            Assert.Same(BlankSpan.Instance, proxyTracer.StartSpan("foo", BlankSpan.Instance, SpanKind.Client, null));

            var defaultContext = default(SpanContext);
            Assert.Same(BlankSpan.Instance, proxyTracer.StartSpan("foo", defaultContext));
            Assert.Same(BlankSpan.Instance, proxyTracer.StartSpan("foo", defaultContext, SpanKind.Client));
            Assert.Same(BlankSpan.Instance, proxyTracer.StartSpan("foo", defaultContext, SpanKind.Client, null));

            var validActivity = new Activity("foo").SetIdFormat(ActivityIdFormat.W3C).Start();
            Assert.Same(BlankSpan.Instance, proxyTracer.StartSpanFromActivity("foo", validActivity));
            Assert.Same(BlankSpan.Instance, proxyTracer.StartSpanFromActivity("foo", validActivity, SpanKind.Consumer));
            Assert.Same(BlankSpan.Instance, proxyTracer.StartSpanFromActivity("foo", validActivity, SpanKind.Client, null));
        }

        [Fact]
        public void ProxyTracer_UpdateTracer_StartRootSpanFrom_Kind_Timestamp_Links()
        {
            var proxyTracer = new ProxyTracer();
            var realTracer = TracerFactory.Create(b => { }).GetTracer(null);
            proxyTracer.UpdateTracer(realTracer);
            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var startTimestamp = DateTimeOffset.UtcNow.AddSeconds(-10);
            var span = (SpanSdk)proxyTracer.StartRootSpan("foo", SpanKind.Server, new SpanCreationOptions
            {
                StartTimestamp = startTimestamp,
                LinksFactory = () => new[] { new Link(linkContext) },
            });

            Assert.True(span.Context.IsValid);
            Assert.Equal(span.Activity.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(default, span.ParentSpanId);
            Assert.Equal(span.Activity.ActivityTraceFlags, span.Context.TraceFlags);
            Assert.Empty(span.Context.Tracestate);

            Assert.True(span.IsRecording);

            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(startTimestamp, span.StartTimestamp);
            Assert.Single(span.Links);
            Assert.Equal(linkContext, span.Links.Single().Context);
        }

        [Fact]
        public void ProxyTracer_UpdateTracer_StartSpanFrom_ParentSpan_Kind_Timestamp_Links()
        {
            var proxyTracer = new ProxyTracer();
            var realTracer = TracerFactory.Create(b => { }).GetTracer(null);
            proxyTracer.UpdateTracer(realTracer);

            var parentSpan = (SpanSdk)proxyTracer.StartSpan("parent");

            var startTimestamp = PreciseTimestamp.GetUtcNow();
            var linkContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            var span = (SpanSdk)proxyTracer.StartSpan("child", parentSpan, SpanKind.Server, new SpanCreationOptions
            {
                StartTimestamp = startTimestamp,
                Links = new[] { new Link(linkContext) },
            });

            Assert.True(span.Context.IsValid);
            Assert.Equal(parentSpan.Context.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(parentSpan.Context.SpanId, span.ParentSpanId);
            Assert.Equal(parentSpan.Context.TraceFlags, span.Context.TraceFlags);

            Assert.True(span.IsRecording);

            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(startTimestamp, span.StartTimestamp);
            Assert.Single(span.Links);
            Assert.Equal(linkContext, span.Links.Single().Context);
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
            var span = (SpanSdk)proxyTracer.StartSpan("child", parentSpanContext, SpanKind.Client, new SpanCreationOptions
            {
                StartTimestamp = startTimestamp,
                Links = new[] { new Link(linkContext) },
            });

            Assert.True(span.Context.IsValid);
            Assert.Equal(parentSpanContext.TraceId, span.Context.TraceId);
            Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
            Assert.Equal(parentSpanContext.SpanId, span.ParentSpanId);
            Assert.Equal(parentSpanContext.TraceFlags, span.Context.TraceFlags);

            Assert.True(span.IsRecording);

            Assert.Equal(SpanKind.Client, span.Kind);
            Assert.Equal(startTimestamp, span.StartTimestamp);
            Assert.Single(span.Links);
            Assert.Equal(linkContext, span.Links.Single().Context);
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

            var span = (SpanSdk)proxyTracer.StartSpanFromActivity("foo", activity, SpanKind.Server, new[] { new Link(linkContext) });

            Assert.Equal(activity.TraceId, span.Context.TraceId);
            Assert.Equal(activity.SpanId, span.Context.SpanId);
            Assert.Equal(activity.ParentSpanId, span.ParentSpanId);
            Assert.Equal(activity.ActivityTraceFlags, span.Context.TraceFlags);

            Assert.True(span.IsRecording);
            Assert.Equal(SpanKind.Server, span.Kind);
            Assert.Equal(activity.StartTimeUtc, span.StartTimestamp.DateTime);
            Assert.Equal(activity, span.Activity);
            Assert.Single(span.Links);
            Assert.Equal(linkContext, span.Links.Single().Context);
        }

        [Fact]
        public void ProxyTracer_UpdateTracer_StartSpanFrom_ImplicitParent_Kind_Timestamp_Links()
        {
            var proxyTracer = new ProxyTracer();
            var realTracer = TracerFactory.Create(b => { }).GetTracer(null);
            proxyTracer.UpdateTracer(realTracer);

            var parentSpan = (SpanSdk)proxyTracer.StartSpan("parent");
            using (proxyTracer.WithSpan(parentSpan))
            {
                var startTimestamp = PreciseTimestamp.GetUtcNow();
                var linkContext = new SpanContext(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.Recorded);
                var span = (SpanSdk)proxyTracer.StartSpan("child", SpanKind.Consumer, new SpanCreationOptions
                {
                    StartTimestamp = startTimestamp,
                    LinksFactory = () => new[] { new Link(linkContext) },
                });

                Assert.True(span.Context.IsValid);
                Assert.Equal(parentSpan.Context.TraceId, span.Context.TraceId);
                Assert.Equal(span.Activity.SpanId, span.Context.SpanId);
                Assert.Equal(parentSpan.Context.SpanId, span.ParentSpanId);
                Assert.Equal(parentSpan.Context.TraceFlags, span.Context.TraceFlags);

                Assert.True(span.IsRecording);

                Assert.Equal(SpanKind.Consumer, span.Kind);
                Assert.Equal(startTimestamp, span.StartTimestamp);
                Assert.Single(span.Links);
                Assert.Equal(linkContext, span.Links.Single().Context);
            }
        }
    }
}
