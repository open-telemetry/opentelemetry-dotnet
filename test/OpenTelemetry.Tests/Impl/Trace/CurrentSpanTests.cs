// <copyright file="CurrentSpanTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;
using OpenTelemetry.Trace.Configuration;
using Xunit;

namespace OpenTelemetry.Trace.Test
{
    public class CurrentSpanTests: IDisposable
    {
        private readonly ITracer tracer;

        public CurrentSpanTests()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            tracer = TracerFactory.Create(b => { }).GetTracer(null);
        }

        [Fact]
        public void CurrentSpan_WhenNoContext()
        {
            Assert.Same(BlankSpan.Instance, this.tracer.CurrentSpan);
        }

        [Fact]
        public void CurrentSpan_WhenNoSpanOnActivity()
        {
            var a = new Activity("foo").Start();
            Assert.Same(BlankSpan.Instance, this.tracer.CurrentSpan);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithSpan_CloseDetaches(bool recordEvents)
        {
            var spanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), recordEvents ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None);
            var span = (Span)tracer.StartSpan("foo", spanContext);

            Assert.Same(BlankSpan.Instance, this.tracer.CurrentSpan);
            using (this.tracer.WithSpan(span))
            {
                Assert.Same(span.Activity, Activity.Current);
                Assert.Same(span, this.tracer.CurrentSpan);
            }

            Assert.Same(BlankSpan.Instance, this.tracer.CurrentSpan);
            Assert.Null(Activity.Current);

            // span not ended
            Assert.Equal(default, span.EndTimestamp);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithSpan_EndDoesNotDetach(bool recordEvents)
        {
            var spanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), recordEvents ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None);
            var span = (Span)tracer.StartSpan("foo", spanContext);

            Assert.Same(BlankSpan.Instance, this.tracer.CurrentSpan);
            using (this.tracer.WithSpan(span))
            {
                Assert.Same(span.Activity, Activity.Current);
                Assert.Same(span, this.tracer.CurrentSpan);

                span.End();

                // span ended
                Assert.NotEqual(default, span.EndTimestamp);

                Assert.Same(span.Activity, Activity.Current);
                Assert.Same(span, this.tracer.CurrentSpan);
            }

            Assert.Same(BlankSpan.Instance, this.tracer.CurrentSpan);
            Assert.Null(Activity.Current);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void StartActiveSpan_NotOwningActivity(bool recordEvents)
        {
            var activity = new Activity("foo");
            if (recordEvents)
            {
                activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            }

            activity.Start();

            Span span = null;
            using (var scope = tracer.StartSpanFromActivity("foo", activity))
            {
                Assert.IsType<Span>(scope);
                span = (Span)scope;
                Assert.Same(span, this.tracer.CurrentSpan);
                Assert.Same(span.Activity, Activity.Current);
            }

            Assert.Same(BlankSpan.Instance, this.tracer.CurrentSpan);
            Assert.Equal(activity, Activity.Current);

            // span ended
            Assert.NotEqual(default, span.EndTimestamp);
        }

        [Fact]
        public void StartActiveSpan_OwningActivity()
        {
            Span span = null;
            using (var scope = tracer.StartActiveSpan("foo"))
            {
                Assert.IsType<Span>(scope);
                span = (Span)scope;
                Assert.Same(span, this.tracer.CurrentSpan);
                Assert.Same(span.Activity, Activity.Current);
            }

            Assert.Same(BlankSpan.Instance, this.tracer.CurrentSpan);
            Assert.Null(Activity.Current);

            // span ended
            Assert.NotEqual(default, span.EndTimestamp);
        }

        [Fact]
        public void StartActiveSpan_WithSpan()
        {
            using (tracer.StartActiveSpan("foo"))
            {
                var span = this.tracer.CurrentSpan;

                Assert.Same(NoopDisposable.Instance, this.tracer.WithSpan(span));
            }

            Assert.Same(BlankSpan.Instance, this.tracer.CurrentSpan);
            Assert.Null(Activity.Current);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithSpan_NoopOnBrokenScope(bool recordEvents)
        {
            var spanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), recordEvents ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None);

            var parentSpan = tracer.StartSpan("parent", spanContext);
            var parentScope = this.tracer.WithSpan(parentSpan);

            var childSpan = (Span)tracer.StartSpan("child", parentSpan);
            var childActivity = childSpan.Activity;
            Assert.Same(parentSpan, this.tracer.CurrentSpan);

            var childScope = this.tracer.WithSpan(childSpan);

            parentScope.Dispose();

            Assert.Same(childSpan, this.tracer.CurrentSpan);
            Assert.Equal(childActivity, Activity.Current);


            // span not ended
            Assert.Equal(default, ((Span)parentSpan).EndTimestamp);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithSpan_RestoresParentScope(bool recordEvents)
        {
            var spanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), recordEvents ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None);

            var parentSpan = (Span)tracer.StartSpan("parent", spanContext);
            var parentActivity = parentSpan.Activity;
            var parentScope = this.tracer.WithSpan(parentSpan);

            var childSpan = (Span)tracer.StartSpan("child");
            Assert.Same(parentSpan, this.tracer.CurrentSpan);
            using (this.tracer.WithSpan(childSpan))
            {
            }

            Assert.Same(parentSpan, this.tracer.CurrentSpan);
            Assert.Equal(parentActivity, Activity.Current);

            // span not ended
            Assert.Equal(default, parentSpan.EndTimestamp);
        }

        [Fact]
        public void WithSpan_SameActivityCreateScopeTwice()
        {
            var span = (Span)tracer.StartRootSpan("foo");

            using(this.tracer.WithSpan(span))
            using(this.tracer.WithSpan(span))
            {
                Assert.Same(span.Activity, Activity.Current);
                Assert.Same(span, this.tracer.CurrentSpan);
            }

            Assert.Same(BlankSpan.Instance, this.tracer.CurrentSpan);
            Assert.Null(Activity.Current);

            // span not ended
            Assert.Equal(default, span.EndTimestamp);
        }

        [Fact]
        public void WithSpan_NullActivity()
        {
            var span = (Span)tracer.StartRootSpan("foo");

            span.Activity.Stop();

            using (this.tracer.WithSpan(span))
            {
                Assert.Null(Activity.Current);
                Assert.Same(BlankSpan.Instance, this.tracer.CurrentSpan);
            }

            Assert.Null(Activity.Current);
            Assert.Same(BlankSpan.Instance, this.tracer.CurrentSpan);

            // span not ended
            Assert.Equal(default, span.EndTimestamp);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void WithSpan_WrongActivity(bool recordEvents)
        {
            var spanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), recordEvents ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None);

            var span = (Span)tracer.StartSpan("foo", spanContext);
            Assert.Same(BlankSpan.Instance, this.tracer.CurrentSpan);
            using (this.tracer.WithSpan(span))
            {
                Assert.Same(span.Activity, Activity.Current);
                Assert.Same(span, this.tracer.CurrentSpan);

                var anotherActivity = new Activity("foo").Start();
            }

            Assert.Same(BlankSpan.Instance, this.tracer.CurrentSpan);
            Assert.NotSame(span.Activity, Activity.Current);
            Assert.NotNull(Activity.Current);

            // span not ended
            Assert.Equal(default, span.EndTimestamp);
        }

        public void Dispose()
        {
            Activity.Current = null;
        }
    }
}
