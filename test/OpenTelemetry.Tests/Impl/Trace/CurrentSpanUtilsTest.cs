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

namespace OpenTelemetry.Trace.Test
{
    using System;
    using System.Diagnostics;
    using Moq;
    using OpenTelemetry.Trace.Config;
    using Xunit;

    public class CurrentSpanUtilsTest: IDisposable
    {
        private readonly CurrentSpanUtils currentSpanUtils = new CurrentSpanUtils();
        private readonly IStartEndHandler startEndHandler = Mock.Of<IStartEndHandler>();

        public CurrentSpanUtilsTest()
        {
            // TODO: remove with next DiagnosticSource preview, switch to Activity setidformat
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
        }

        [Fact]
        public void CurrentSpan_WhenNoContext()
        {
            Assert.Same(BlankSpan.Instance, currentSpanUtils.CurrentSpan);
        }

        [Fact]
        public void CurrentSpan_WhenNoSpanOnActivity()
        {
            var a = new Activity("foo").Start();
            Assert.Same(BlankSpan.Instance, currentSpanUtils.CurrentSpan);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void WithSpan_CloseDetaches(bool stopSpan, bool recordEvents)
        {
            var activity = new Activity("foo").Start();
            var span = Span.StartSpan(
                activity,
                Tracestate.Empty,
                recordEvents ? SpanOptions.RecordEvents : SpanOptions.None,
                "foo",
                SpanKind.Internal,
                TraceParams.Default,
                startEndHandler,
                null);

            Assert.Same(BlankSpan.Instance, currentSpanUtils.CurrentSpan);
            using (currentSpanUtils.WithSpan(span, stopSpan))
            {
                Assert.Same(activity, Activity.Current);
                Assert.Same(span, currentSpanUtils.CurrentSpan);
            }

            Assert.Equal(stopSpan & recordEvents, span.HasEnded);
            Assert.Same(BlankSpan.Instance, currentSpanUtils.CurrentSpan);
            Assert.Null(Activity.Current);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void WithSpan_NotOwningActivity(bool stopSpan, bool recordEvents)
        {
            var activity = new Activity("foo").Start();
            var span = Span.StartSpan(
                activity,
                Tracestate.Empty,
                recordEvents ? SpanOptions.RecordEvents : SpanOptions.None,
                "foo",
                SpanKind.Internal,
                TraceParams.Default,
                startEndHandler,
                null,
                false);

            Assert.Same(BlankSpan.Instance, currentSpanUtils.CurrentSpan);
            using (currentSpanUtils.WithSpan(span, stopSpan))
            {
                Assert.Same(activity, Activity.Current);
                Assert.Same(span, currentSpanUtils.CurrentSpan);
            }

            Assert.Same(BlankSpan.Instance, currentSpanUtils.CurrentSpan);
            Assert.Equal(activity, Activity.Current);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void WithSpan_NoopOnBrokenScope(bool stopSpan, bool recordEvents)
        {
            var parentActivity = new Activity("parent").Start();
            var parentSpan = Span.StartSpan(
                parentActivity,
                Tracestate.Empty,
                recordEvents ? SpanOptions.RecordEvents : SpanOptions.None,
                "parent",
                SpanKind.Internal,
                TraceParams.Default,
                startEndHandler,
                null);
            var parentScope = currentSpanUtils.WithSpan(parentSpan, stopSpan);

            var childActivity = new Activity("child").Start();
            var childSpan = Span.StartSpan(
                childActivity,
                Tracestate.Empty,
                recordEvents ? SpanOptions.RecordEvents : SpanOptions.None,
                "child",
                SpanKind.Internal,
                TraceParams.Default,
                startEndHandler,
                null);

            Assert.Same(BlankSpan.Instance, currentSpanUtils.CurrentSpan);

            var childScope = currentSpanUtils.WithSpan(childSpan, stopSpan);

            parentScope.Dispose();

            Assert.Equal(stopSpan & recordEvents, parentSpan.HasEnded);
            Assert.False(childSpan.HasEnded);
            Assert.Same(childSpan, currentSpanUtils.CurrentSpan);
            Assert.Equal(childActivity, Activity.Current);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void WithSpan_RestoresParentScope(bool stopSpan, bool recordEvents)
        {
            var parentActivity = new Activity("parent").Start();
            var parentSpan = Span.StartSpan(
                parentActivity,
                Tracestate.Empty,
                recordEvents ? SpanOptions.RecordEvents : SpanOptions.None,
                "parent",
                SpanKind.Internal,
                TraceParams.Default,
                startEndHandler,
                null);
            var parentScope = currentSpanUtils.WithSpan(parentSpan, stopSpan);

            var childActivity = new Activity("child").Start();
            var childSpan = Span.StartSpan(
                childActivity,
                Tracestate.Empty,
                recordEvents ? SpanOptions.RecordEvents : SpanOptions.None,
                "child",
                SpanKind.Internal,
                TraceParams.Default,
                startEndHandler,
                null);

            Assert.Same(BlankSpan.Instance, currentSpanUtils.CurrentSpan);

            var childScope = currentSpanUtils.WithSpan(childSpan, stopSpan);

            childScope.Dispose();

            Assert.Equal(stopSpan & recordEvents, childSpan.HasEnded);
            Assert.False(parentSpan.HasEnded);

            Assert.Same(parentSpan, currentSpanUtils.CurrentSpan);
            Assert.Equal(parentActivity, Activity.Current);
        }

        [Fact]
        public void WithSpan_SameActivityCreateScopeTwice()
        {
            var activity = new Activity("foo").Start();
            var span = Span.StartSpan(
                activity,
                Tracestate.Empty,
                SpanOptions.RecordEvents,
                "foo",
                SpanKind.Internal,
                TraceParams.Default,
                startEndHandler,
                null);

            using (currentSpanUtils.WithSpan(span, true))
            using(currentSpanUtils.WithSpan(span, true))
            {
                Assert.Same(activity, Activity.Current);
                Assert.Same(span, currentSpanUtils.CurrentSpan);
            }

            Assert.Same(BlankSpan.Instance, currentSpanUtils.CurrentSpan);
            Assert.Null(Activity.Current);
        }

        [Fact]
        public void WithSpan_NullActivity()
        {
            var activity = new Activity("foo").Start();
            var span = Span.StartSpan(
                activity,
                Tracestate.Empty,
                SpanOptions.RecordEvents,
                "foo",
                SpanKind.Internal,
                TraceParams.Default,
                startEndHandler,
                null);

            activity.Stop();

            using (currentSpanUtils.WithSpan(span, true))
            {
                Assert.Null(Activity.Current);
                Assert.Same(BlankSpan.Instance, currentSpanUtils.CurrentSpan);
            }

            Assert.Null(Activity.Current);
            Assert.Same(BlankSpan.Instance, currentSpanUtils.CurrentSpan);
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void WithSpan_WrongActivity(bool stopSpan, bool recordEvents)
        {
            var activity = new Activity("foo").Start();
            var span = Span.StartSpan(
                activity,
                Tracestate.Empty,
                recordEvents ? SpanOptions.RecordEvents : SpanOptions.None,
                "foo",
                SpanKind.Internal,
                TraceParams.Default,
                startEndHandler,
                null);

            Assert.Same(BlankSpan.Instance, currentSpanUtils.CurrentSpan);
            using (currentSpanUtils.WithSpan(span, stopSpan))
            {
                Assert.Same(activity, Activity.Current);
                Assert.Same(span, currentSpanUtils.CurrentSpan);

                var anotherActivity = new Activity("foo").Start();
            }

            Assert.Equal(stopSpan & recordEvents, span.HasEnded);
            Assert.Same(BlankSpan.Instance, currentSpanUtils.CurrentSpan);
            Assert.NotSame(activity, Activity.Current);
            Assert.NotNull(Activity.Current);
        }

        public void Dispose()
        {
            Activity.Current = null;
        }
    }
}
