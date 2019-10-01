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
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Trace.Sampler;
    using Xunit;

    public class SpanTest : IDisposable
    {
        private const string SpanName = "MySpanName";
        private const string EventDescription = "MyEvent";

        private readonly IDictionary<string, object> attributes = new Dictionary<String, object>();
        private readonly List<KeyValuePair<string, object>> expectedAttributes;
        private readonly Mock<SpanProcessor> spanProcessorMock = new Mock<SpanProcessor>(new NoopSpanExporter());
        private readonly SpanProcessor spanProcessor;

        public SpanTest()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            spanProcessor = spanProcessorMock.Object;
            attributes.Add("MyStringAttributeKey", "MyStringAttributeValue");
            attributes.Add("MyLongAttributeKey", 123L);
            attributes.Add("MyBooleanAttributeKey", false);
            expectedAttributes = new List<KeyValuePair<string, object>>(attributes)
                {
                    new KeyValuePair<string, object>("MySingleStringAttributeKey", "MySingleStringAttributeValue"),
                };
        }

        [Fact]
        public void GetSpanContextFromActivity()
        {
            var tracestate = Tracestate.Builder.Set("k1", "v1").Build();
            var activity = new Activity(SpanName).Start();
            activity.TraceStateString = tracestate.ToString();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var span = new Span(
                    activity,
                    tracestate,
                    SpanKind.Internal,
                    TraceConfig.Default,
                    spanProcessor,
                    default,
                    false);
            Assert.True(span.Context.IsValid);
            Assert.Equal(activity.TraceId, span.Context.TraceId);
            Assert.Equal(activity.SpanId, span.Context.SpanId);
            Assert.Equal(activity.ParentSpanId, ((Span)span).ParentSpanId);
            Assert.Equal(activity.ActivityTraceFlags, span.Context.TraceOptions);
            Assert.Same(tracestate, span.Context.Tracestate);
        }

        [Fact]
        public void GetSpanContextFromActivityRecordedWithParent()
        {
            var tracestate = Tracestate.Builder.Set("k1", "v1").Build();
            var parent = new Activity(SpanName).Start();
            var activity = new Activity(SpanName).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var span = new Span(
                    activity,
                    tracestate,
                    SpanKind.Internal,
                    TraceConfig.Default,
                    spanProcessor,
                    default,
                    false);
            Assert.True(span.Context.IsValid);
            Assert.Equal(activity.TraceId, span.Context.TraceId);
            Assert.Equal(activity.SpanId, span.Context.SpanId);
            Assert.Equal(activity.ParentSpanId, ((Span)span).ParentSpanId);
            Assert.Equal(activity.ActivityTraceFlags, span.Context.TraceOptions);
            Assert.Same(tracestate, span.Context.Tracestate);
        }

        [Fact]
        public void NoEventsRecordedAfterEnd()
        {
            var link = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None, Tracestate.Empty);

            var activity = new Activity(SpanName).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var spanStartTime = PreciseTimestamp.GetUtcNow();
            var span = new Span(
                    activity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    TraceConfig.Default,
                    spanProcessor,
                    spanStartTime,
                    false);
            var spanEndTime = PreciseTimestamp.GetUtcNow();
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

            Assert.Equal(spanStartTime, span.StartTimestamp);
            Assert.Empty(span.Attributes);
            Assert.Empty(span.Events);
            Assert.Empty(span.Links);
            Assert.Equal(Status.Ok, span.Status);
            Assert.Equal(spanEndTime, span.EndTimestamp);
        }

        [Fact]
        public void ImplicitTimestamps()
        {
            var activity = new Activity(SpanName).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var spanStartTime = PreciseTimestamp.GetUtcNow();
            var span = new Span(
                    activity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    TraceConfig.Default,
                    spanProcessor,
                    PreciseTimestamp.GetUtcNow(),
                    false);
            var spanEndTime = PreciseTimestamp.GetUtcNow();
            span.End();

            AssertApproxSameTimestamp(spanStartTime, span.StartTimestamp);
            Assert.Empty(span.Attributes);
            Assert.Empty(span.Events);
            Assert.Empty(span.Links);
            Assert.Equal(Status.Ok, span.Status);
            AssertApproxSameTimestamp(spanEndTime, span.EndTimestamp);
        }

        [Fact]
        public async Task ActiveSpan_Properties()
        {
            var contextLink = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None, Tracestate.Empty);

            var activity = new Activity(SpanName)
                .SetParentId(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom())
                .Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var spanStartTime = PreciseTimestamp.GetUtcNow();
            var span = new Span(
                    activity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    TraceConfig.Default,
                    spanProcessor,
                    spanStartTime,
                    false);

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

            Assert.Equal(activity.TraceId, span.Context.TraceId);
            Assert.Equal(activity.SpanId, span.Context.SpanId);
            Assert.Equal(activity.ParentSpanId, span.ParentSpanId);
            Assert.Equal(activity.ActivityTraceFlags, span.Context.TraceOptions);
            Assert.Same(Tracestate.Empty, span.Context.Tracestate);

            Assert.Equal(SpanName, span.Name);
            Assert.Equal(activity.ParentSpanId, span.ParentSpanId);

            span.Attributes.AssertAreSame(expectedAttributes);

            Assert.Equal(2, span.Events.Count());
            Assert.Equal(firstEventTime, span.Events.ToList()[0].Timestamp);
            AssertApproxSameTimestamp(span.Events.ToList()[1].Timestamp, secondEventTime);

            Assert.Equal(new Event(EventDescription, firstEventTime), span.Events.ToList()[0]);

            Assert.Equal(EventDescription, span.Events.ToList()[1].Name);
            Assert.Equal(attributes, span.Events.ToList()[1].Attributes);

            Assert.Single(span.Links);
            Assert.Equal(link, span.Links.First());

            Assert.Equal(spanStartTime, span.StartTimestamp);

            Assert.True(span.Status.IsValid);
            Assert.Equal(default, span.EndTimestamp);

            var startEndMock = Mock.Get<SpanProcessor>(spanProcessor);

            spanProcessorMock.Verify(s => s.OnStart(span), Times.Once);
            startEndMock.Verify(s => s.OnEnd(span), Times.Never);
        }

        [Fact]
        public async Task EndedSpan_Properties()
        {
            var contextLink = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None, Tracestate.Empty);

            var activity = new Activity(SpanName)
                .SetParentId(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom())
                .Start();

            var spanStartTime = PreciseTimestamp.GetUtcNow();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            var span = new Span(
                    activity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    TraceConfig.Default,
                    spanProcessor,
                    spanStartTime,
                    false);

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

            Assert.Equal(activity.TraceId, span.Context.TraceId);
            Assert.Equal(activity.SpanId, span.Context.SpanId);
            Assert.Equal(activity.ParentSpanId, span.ParentSpanId);
            Assert.Equal(activity.ActivityTraceFlags, span.Context.TraceOptions);

            Assert.Equal(SpanName, span.Name);
            Assert.Equal(activity.ParentSpanId, span.ParentSpanId);

            span.Attributes.AssertAreSame(expectedAttributes);
            Assert.Equal(2, span.Events.Count());

            Assert.Equal(firstEventTime, span.Events.ToList()[0].Timestamp);
            AssertApproxSameTimestamp(span.Events.ToList()[1].Timestamp, secondEventTime);

            Assert.Equal(new Event(EventDescription, firstEventTime), span.Events.ToList()[0]);
            Assert.Single(span.Links);
            Assert.Equal(link, span.Links.First());
            Assert.Equal(spanStartTime, span.StartTimestamp);
            Assert.Equal(Status.Cancelled, span.Status);
            AssertApproxSameTimestamp(spanEndTime, span.EndTimestamp);

            spanProcessorMock.Verify(s => s.OnStart(span), Times.Once);
            spanProcessorMock.Verify(s => s.OnEnd(span), Times.Once);
        }

        [Fact]
        public void Status_ViaSetStatus()
        {
            var activity = new Activity(SpanName).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var span =
                new Span(
                    activity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    TraceConfig.Default,
                    spanProcessor,
                    PreciseTimestamp.GetUtcNow(),
                    false);

            Assert.Equal(Status.Ok, span.Status);
            ((Span)span).Status = Status.Cancelled;
            Assert.Equal(Status.Cancelled, span.Status);
            span.End();
            Assert.Equal(Status.Cancelled, span.Status);

            spanProcessorMock.Verify(s => s.OnStart(span), Times.Once);
            spanProcessorMock.Verify(s => s.OnEnd(span), Times.Once);
        }

        [Fact]
        public void status_ViaEndSpanOptions()
        {
            var activity = new Activity(SpanName).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var span =
                new Span(
                    activity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    TraceConfig.Default,
                    spanProcessor,
                    PreciseTimestamp.GetUtcNow(),
                    false);

            Assert.Equal(Status.Ok, span.Status);
            ((Span)span).Status = Status.Cancelled;
            Assert.Equal(Status.Cancelled, span.Status);
            span.Status = Status.Aborted;
            span.End();
            Assert.Equal(Status.Aborted, span.Status);

            spanProcessorMock.Verify(s => s.OnStart(span), Times.Once);
            spanProcessorMock.Verify(s => s.OnEnd(span), Times.Once);
        }

        [Fact]
        public void DroppingAttributes()
        {
            var activity = new Activity(SpanName).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var maxNumberOfAttributes = 8;
            var traceConfig = new TraceConfig(Samplers.AlwaysSample, 8 , 128, 32);
            var span = new Span(
                    activity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    traceConfig,
                    spanProcessor,
                    PreciseTimestamp.GetUtcNow(),
                    false);
            for (var i = 0; i < 2 * maxNumberOfAttributes; i++)
            {
                IDictionary<string, object> attributes = new Dictionary<string, object>();
                attributes.Add("MyStringAttributeKey" + i, i);
                foreach (var attribute in attributes)
                {
                    span.SetAttribute(attribute);
                }

            }

            Assert.Equal(maxNumberOfAttributes, span.Attributes.Count());
            for (var i = 0; i < maxNumberOfAttributes; i++)
            {
                Assert.Equal(
                    i + maxNumberOfAttributes,
                    span
                        .Attributes
                        .GetValue("MyStringAttributeKey" + (i + maxNumberOfAttributes)));
            }

            span.End();

            Assert.Equal(maxNumberOfAttributes, span.Attributes.Count());
            for (var i = 0; i < maxNumberOfAttributes; i++)
            {
                Assert.Equal(
                    i + maxNumberOfAttributes,
                    span
                        .Attributes
                        .GetValue("MyStringAttributeKey" + (i + maxNumberOfAttributes)));
            }
        }

        [Fact]
        public void DroppingAndAddingAttributes()
        {
            var activity = new Activity(SpanName).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var maxNumberOfAttributes = 8;
            var traceConfig = new TraceConfig(Samplers.AlwaysSample, maxNumberOfAttributes, 128, 32);
            var span = new Span(
                    activity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    traceConfig,
                    spanProcessor,
                    PreciseTimestamp.GetUtcNow(),
                    false);
            for (var i = 0; i < 2 * maxNumberOfAttributes; i++)
            {
                IDictionary<String, object> attributes = new Dictionary<String, object>();
                attributes.Add("MyStringAttributeKey" + i, i);
                foreach (var attribute in attributes)
                {
                    span.SetAttribute(attribute);
                }

            }

            Assert.Equal(maxNumberOfAttributes, span.Attributes.Count());
            for (var i = 0; i < maxNumberOfAttributes; i++)
            {
                Assert.Equal(
                    i + maxNumberOfAttributes,
                    span
                        .Attributes
                        .GetValue("MyStringAttributeKey" + (i + maxNumberOfAttributes)));
            }

            for (var i = 0; i < maxNumberOfAttributes / 2; i++)
            {
                IDictionary<string, object> attributes = new Dictionary<string, object>();
                attributes.Add("MyStringAttributeKey" + i, i);
                foreach (var attribute in attributes)
                {
                    span.SetAttribute(attribute);
                }

            }

            Assert.Equal(maxNumberOfAttributes, span.Attributes.Count());
            // Test that we still have in the attributes map the latest maxNumberOfAttributes / 2 entries.
            for (var i = 0; i < maxNumberOfAttributes / 2; i++)
            {
                Assert.Equal(
                    i + maxNumberOfAttributes * 3 / 2,
                    span
                        .Attributes
                        .GetValue("MyStringAttributeKey" + (i + maxNumberOfAttributes * 3 / 2)));
            }

            // Test that we have the newest re-added initial entries.
            for (var i = 0; i < maxNumberOfAttributes / 2; i++)
            {
                Assert.Equal(i,
                    span.Attributes.GetValue("MyStringAttributeKey" + i));
            }
        }

        [Fact]
        public async Task DroppingEvents()
        {
            var activity = new Activity(SpanName).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var maxNumberOfEvents = 8;
            var traceConfig = new TraceConfig(Samplers.AlwaysSample, 32, maxNumberOfEvents, 32);
            var span = new Span(
                    activity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    traceConfig,
                    spanProcessor,
                    PreciseTimestamp.GetUtcNow(),
                    false);

            var eventTimestamps = new DateTimeOffset[2 * maxNumberOfEvents];
            
            for (int i = 0; i < 2 * maxNumberOfEvents; i++)
            {
                eventTimestamps[i] = PreciseTimestamp.GetUtcNow();
                span.AddEvent(new Event(EventDescription, eventTimestamps[i]));
                await Task.Delay(10);
            }

            Assert.Equal(maxNumberOfEvents, span.Events.Count());

            var events = span.Events.ToArray();

            for (int i = 0; i < maxNumberOfEvents; i++)
            {
                Assert.Equal(eventTimestamps[i + maxNumberOfEvents], events[i].Timestamp);
            }

            span.End();
            
            Assert.Equal(maxNumberOfEvents, span.Events.Count());
        }

        [Fact]
        public void DroppingLinks()
        {
            var contextLink = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None, Tracestate.Empty);

            var activity = new Activity(SpanName).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var maxNumberOfLinks = 8;
            var traceConfig = new TraceConfig(Samplers.AlwaysSample, 32, 128, maxNumberOfLinks);
            var span = new Span(
                    activity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    traceConfig,
                    spanProcessor,
                    PreciseTimestamp.GetUtcNow(),
                    false);
            var link = new Link(contextLink);
            for (var i = 0; i < 2 * maxNumberOfLinks; i++)
            {
                span.AddLink(link);
            }

            Assert.Equal(maxNumberOfLinks, span.Links.Count());
            foreach (var actualLink in span.Links)
            {
                Assert.Equal(link, actualLink);
            }

            span.End();

            Assert.Equal(maxNumberOfLinks, span.Links.Count());
            foreach (var actualLink in span.Links)
            {
                Assert.Equal(link, actualLink);
            }
        }

        [Fact]
        public void BadArguments()
        {
            var activity = new Activity(SpanName).Start();
            activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;

            var span =
                new Span(
                    activity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    TraceConfig.Default,
                    spanProcessor,
                    PreciseTimestamp.GetUtcNow(),
                    false);

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
            var parentActivity = new Activity(SpanName).Start();
            var activity = new Activity(SpanName).Start();
            if (recordEvents)
            {
                activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            }

            var span =
                new Span(
                    activity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    TraceConfig.Default,
                    spanProcessor,
                    PreciseTimestamp.GetUtcNow(),
                    ownsActivity: true);
            
            span.End();
            Assert.Same(parentActivity, Activity.Current);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EndSpanDoesNotStopActivityWhenDoesNotOwnIt(bool recordEvents)
        {
            var activity = new Activity(SpanName).Start();
            if (recordEvents)
            {
                activity.ActivityTraceFlags |= ActivityTraceFlags.Recorded;
            }

            var span =
                new Span(
                    activity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    TraceConfig.Default,
                    spanProcessor,
                    PreciseTimestamp.GetUtcNow(),
                    ownsActivity: false);

            span.End();
            Assert.Equal(recordEvents, span.HasEnded);
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

            var span =
                new Span(
                    activity,
                    Tracestate.Empty,
                    SpanKind.Internal,
                    TraceConfig.Default,
                    spanProcessor,
                    PreciseTimestamp.GetUtcNow(),
                    ownsActivity: ownsActivity);

            var anotherActivity = new Activity(SpanName).Start();
            span.End();
            Assert.Equal(recordEvents, span.HasEnded);
            Assert.Same(anotherActivity, Activity.Current);
        }

        public void Dispose()
        {
            Activity.Current = null;
        }

        private void AssertApproxSameTimestamp(DateTimeOffset one, DateTimeOffset two)
        {
            var timeShift = Math.Abs((one - two).TotalMilliseconds);
            Assert.InRange(timeShift, 0, 20);
        }
    }
}
