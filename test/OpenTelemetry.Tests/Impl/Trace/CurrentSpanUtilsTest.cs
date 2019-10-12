// <copyright file="CurrentSpanUtilsTest.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Trace.Internal;
using System;
using System.Diagnostics;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Trace.Test
{
    public class CurrentSpanUtilsTest: IDisposable
    {
        private readonly SpanProcessor spanProcessor = new SimpleSpanProcessor(new NoopSpanExporter());
        private readonly ITracer tracer;

        public CurrentSpanUtilsTest()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            tracer = TracerFactory.Create(b => b
                    .SetProcessor(_ => spanProcessor)
                    .SetTracerOptions(new TracerConfiguration()))
                .GetTracer(null);
        }

        [Fact]
        public void CurrentSpan_WhenNoContext()
        {
            Assert.Same(BlankSpan.Instance, CurrentSpanUtils.CurrentSpan);
        }

        [Fact]
        public void CurrentSpan_WhenNoSpanOnActivity()
        {
            var a = new Activity("foo").Start();
            Assert.Same(BlankSpan.Instance, CurrentSpanUtils.CurrentSpan);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void WithSpan_CloseDetaches(bool stopSpan, bool recordEvents)
        {
            var spanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), recordEvents ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None);
            var span = (Span)tracer.StartSpan("foo", spanContext);

            Assert.Same(BlankSpan.Instance, CurrentSpanUtils.CurrentSpan);
            using (CurrentSpanUtils.WithSpan(span, stopSpan))
            {
                Assert.Same(span.Activity, Activity.Current);
                Assert.Same(span, CurrentSpanUtils.CurrentSpan);
            }

            Assert.Same(BlankSpan.Instance, CurrentSpanUtils.CurrentSpan);
            Assert.Null(Activity.Current);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void WithSpan_NotOwningActivity(bool stopSpan, bool recordEvents)
        {
            var activity = new Activity("foo");
            if (recordEvents)
            {
                activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            }

            activity.Start();
            var span = (Span)tracer.StartSpanFromActivity("foo", activity);

            Assert.Same(BlankSpan.Instance, CurrentSpanUtils.CurrentSpan);
            using (CurrentSpanUtils.WithSpan(span, stopSpan))
            {
                Assert.Same(span.Activity, Activity.Current);
                Assert.Same(span, CurrentSpanUtils.CurrentSpan);
            }

            Assert.Same(BlankSpan.Instance, CurrentSpanUtils.CurrentSpan);
            Assert.Equal(activity, Activity.Current);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void WithSpan_NoopOnBrokenScope(bool stopSpan, bool recordEvents)
        {
            var spanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), recordEvents ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None);

            var parentSpan = tracer.StartSpan("parent", spanContext);
            var parentScope = CurrentSpanUtils.WithSpan(parentSpan, stopSpan);

            var childSpan = (Span)tracer.StartSpan("child", parentSpan);
            var childActivity = childSpan.Activity;
            Assert.Same(parentSpan, CurrentSpanUtils.CurrentSpan);

            var childScope = CurrentSpanUtils.WithSpan(childSpan, stopSpan);

            parentScope.Dispose();

            Assert.Same(childSpan, CurrentSpanUtils.CurrentSpan);
            Assert.Equal(childActivity, Activity.Current);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void WithSpan_RestoresParentScope(bool stopSpan, bool recordEvents)
        {
            var spanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), recordEvents ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None);

            var parentSpan = (Span)tracer.StartSpan("parent", spanContext);
            var parentActivity = parentSpan.Activity;
            var parentScope = CurrentSpanUtils.WithSpan(parentSpan, stopSpan);

            var childSpan = (Span)tracer.StartSpan("child");
            Assert.Same(parentSpan, CurrentSpanUtils.CurrentSpan);
            var childScope = CurrentSpanUtils.WithSpan(childSpan, stopSpan);

            childScope.Dispose();

            Assert.Same(parentSpan, CurrentSpanUtils.CurrentSpan);
            Assert.Equal(parentActivity, Activity.Current);
        }

        [Fact]
        public void WithSpan_SameActivityCreateScopeTwice()
        {
            var span = (Span)tracer.StartRootSpan("foo");

            using(CurrentSpanUtils.WithSpan(span, true))
            using(CurrentSpanUtils.WithSpan(span, true))
            {
                Assert.Same(span.Activity, Activity.Current);
                Assert.Same(span, CurrentSpanUtils.CurrentSpan);
            }

            Assert.Same(BlankSpan.Instance, CurrentSpanUtils.CurrentSpan);
            Assert.Null(Activity.Current);
        }

        [Fact]
        public void WithSpan_NullActivity()
        {
            var span = (Span)tracer.StartRootSpan("foo");

            span.Activity.Stop();

            using (CurrentSpanUtils.WithSpan(span, true))
            {
                Assert.Null(Activity.Current);
                Assert.Same(BlankSpan.Instance, CurrentSpanUtils.CurrentSpan);
            }

            Assert.Null(Activity.Current);
            Assert.Same(BlankSpan.Instance, CurrentSpanUtils.CurrentSpan);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void WithSpan_WrongActivity(bool stopSpan, bool recordEvents)
        {
            var spanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), recordEvents ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None);

            var span = (Span)tracer.StartSpan("foo", spanContext);
            Assert.Same(BlankSpan.Instance, CurrentSpanUtils.CurrentSpan);
            using (CurrentSpanUtils.WithSpan(span, stopSpan))
            {
                Assert.Same(span.Activity, Activity.Current);
                Assert.Same(span, CurrentSpanUtils.CurrentSpan);

                var anotherActivity = new Activity("foo").Start();
            }

            Assert.Same(BlankSpan.Instance, CurrentSpanUtils.CurrentSpan);
            Assert.NotSame(span.Activity, Activity.Current);
            Assert.NotNull(Activity.Current);
        }

        public void Dispose()
        {
            Activity.Current = null;
        }
    }
}
