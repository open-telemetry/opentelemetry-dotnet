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
        private readonly Tracer tracer;

        public CurrentSpanTests()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            tracer = TracerFactory.Create(b => { }).GetTracer(null);
        }

        [Fact]
        public void CurrentSpan_WhenNoContext()
        {
            Assert.False(this.tracer.CurrentSpan.Context.IsValid);
        }

        [Fact]
        public void CurrentSpan_WhenNoSpanOnActivity()
        {
            var a = new Activity("foo").Start();
            Assert.False(this.tracer.CurrentSpan.Context.IsValid);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void WithSpan_CloseDetaches(bool endSpan, bool recordEvents)
        {
            var spanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), recordEvents ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None);
            var span = (Span)tracer.StartSpan("foo", spanContext);

            Assert.False(this.tracer.CurrentSpan.Context.IsValid);
            using (this.tracer.WithSpan(span, endSpan))
            {
                Assert.Same(span.Activity, Activity.Current);
                Assert.Same(span, this.tracer.CurrentSpan);
            }

            Assert.False(this.tracer.CurrentSpan.Context.IsValid);
            Assert.Null(Activity.Current);

            if (endSpan)
            {
                Assert.NotEqual(default, span.EndTimestamp);
            }
            else
            {
                Assert.Equal(default, span.EndTimestamp);
            }
        }

        [Fact]
        public void WithSpan_NoFlag_DoesNotEndSpan()
        {
            var span = (Span)tracer.StartSpan("foo");

            Assert.False(this.tracer.CurrentSpan.Context.IsValid);
            using (this.tracer.WithSpan(span))
            {
                Assert.Same(span.Activity, Activity.Current);
                Assert.Same(span, this.tracer.CurrentSpan);
            }

            Assert.False(this.tracer.CurrentSpan.Context.IsValid);
            Assert.Null(Activity.Current);

            // span not ended
            Assert.Equal(default, span.EndTimestamp);
        }

        [Fact]
        public void WithSpan_AttachAndDetach()
        {
            var span = (Span)tracer.StartSpan("foo");

            Assert.False(this.tracer.CurrentSpan.Context.IsValid);
            using (this.tracer.WithSpan(span))
            {
                Assert.Same(span.Activity, Activity.Current);
                Assert.Same(span, this.tracer.CurrentSpan);
            }

            Assert.False(this.tracer.CurrentSpan.Context.IsValid);
            Assert.Null(Activity.Current);

            using (this.tracer.WithSpan(span))
            {
                Assert.Same(span.Activity, Activity.Current);
                Assert.Same(span, this.tracer.CurrentSpan);
            }

            Assert.False(this.tracer.CurrentSpan.Context.IsValid);
            Assert.Null(Activity.Current);

            // span not ended
            Assert.Equal(default, span.EndTimestamp);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void WithSpan_EndDoesNotDetach(bool endSpan, bool recordEvents)
        {
            var spanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), recordEvents ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None);
            var span = (Span)tracer.StartSpan("foo", spanContext);

            Assert.False(this.tracer.CurrentSpan.Context.IsValid);
            using (this.tracer.WithSpan(span, endSpan))
            {
                Assert.Same(span.Activity, Activity.Current);
                Assert.Same(span, this.tracer.CurrentSpan);

                span.End();

                // span ended
                Assert.NotEqual(default, span.EndTimestamp);

                if (endSpan)
                {
                    Assert.Null(Activity.Current);
                    Assert.False(this.tracer.CurrentSpan.Context.IsValid);
                }
                else
                {
                    Assert.Same(span.Activity, Activity.Current);
                    Assert.Same(span, this.tracer.CurrentSpan);
                }
            }

            Assert.False(this.tracer.CurrentSpan.Context.IsValid);
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

            ISpan span;
            using (var scope = tracer.StartActiveSpanFromActivity("foo", activity, out span))
            {
                Assert.IsType<Span>(scope);
                Assert.Same(scope, span);
                Assert.Same(span, this.tracer.CurrentSpan);
                Assert.Same(((Span)span).Activity, Activity.Current);
            }

            Assert.False(this.tracer.CurrentSpan.Context.IsValid);
            Assert.Equal(activity, Activity.Current);

            // span ended
            Assert.NotEqual(default, ((Span)span).EndTimestamp);
        }

        [Fact]
        public void StartActiveSpan_OwningActivity()
        {
            Span span = null;
            using (var scope = tracer.StartActiveSpan("foo", out var ispan))
            {
                Assert.IsType<Span>(scope);
                span = (Span)scope;
                Assert.Same(ispan, span);
                Assert.Same(span, this.tracer.CurrentSpan);
                Assert.Same(span.Activity, Activity.Current);
            }

            Assert.False(this.tracer.CurrentSpan.Context.IsValid);
            Assert.Null(Activity.Current);

            // span ended
            Assert.NotEqual(default, span.EndTimestamp);
        }

        [Fact]
        public void StartActiveSpan_WithSpan()
        {
            using (var scope = tracer.StartActiveSpan("foo", out var span))
            {
                Assert.Same(scope, this.tracer.WithSpan(span));
            }

            Assert.False(this.tracer.CurrentSpan.Context.IsValid);
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
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void WithSpan_RestoresParentScope(bool endSpan, bool recordEvents)
        {
            var spanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), recordEvents ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None);

            var parentSpan = (Span)tracer.StartSpan("parent", spanContext);
            var parentActivity = parentSpan.Activity;
            var parentScope = this.tracer.WithSpan(parentSpan);

            var childSpan = (Span)tracer.StartSpan("child");
            Assert.Same(parentSpan, this.tracer.CurrentSpan);
            using (this.tracer.WithSpan(childSpan, endSpan))
            {
            }

            Assert.Same(parentSpan, this.tracer.CurrentSpan);
            Assert.Equal(parentActivity, Activity.Current);

            if (endSpan)
            {
                Assert.NotEqual(default, childSpan.EndTimestamp);
            }
            else
            {
                Assert.Equal(default, childSpan.EndTimestamp);
            }
        }

        [Fact]
        public void WithSpan_SameActivityCreateScopeTwice()
        {
            var span = (Span)tracer.StartRootSpan("foo");

            using(var scope1 = this.tracer.WithSpan(span))
            using(var scope2 = this.tracer.WithSpan(span))
            {
                Assert.IsNotType<NoopDisposable>(scope1);
                Assert.IsType<NoopDisposable>(scope2);
                Assert.Same(span.Activity, Activity.Current);
                Assert.Same(span, this.tracer.CurrentSpan);
            }

            Assert.False(this.tracer.CurrentSpan.Context.IsValid);
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
                Assert.False(this.tracer.CurrentSpan.Context.IsValid);
            }

            Assert.Null(Activity.Current);
            Assert.False(this.tracer.CurrentSpan.Context.IsValid);

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
            Assert.False(this.tracer.CurrentSpan.Context.IsValid);
            using (this.tracer.WithSpan(span))
            {
                Assert.Same(span.Activity, Activity.Current);
                Assert.Same(span, this.tracer.CurrentSpan);

                var anotherActivity = new Activity("foo").Start();
            }

            Assert.False(this.tracer.CurrentSpan.Context.IsValid);
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
