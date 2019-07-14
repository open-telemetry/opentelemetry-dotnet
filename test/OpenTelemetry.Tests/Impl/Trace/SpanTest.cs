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
    using Moq;
    using OpenTelemetry.Common;
    using OpenTelemetry.Internal;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Config;
    using Xunit;

    public class SpanTest : IDisposable
    {
        private const string SpanName = "MySpanName";
        private const string EventDescription = "MyEvent";
        
        private TimeSpan interval = TimeSpan.FromMilliseconds(0);
        private readonly DateTimeOffset startTime = DateTimeOffset.Now;
        private readonly Timestamp timestamp;
        private readonly Timer timestampConverter;
        private readonly SpanOptions noRecordSpanOptions = SpanOptions.None;
        private readonly SpanOptions recordSpanOptions = SpanOptions.RecordEvents;
        private readonly IDictionary<string, object> attributes = new Dictionary<String, object>();
        private readonly IDictionary<string, object> expectedAttributes;
        private readonly IStartEndHandler startEndHandler = Mock.Of<IStartEndHandler>();

        public SpanTest()
        {
            timestamp = Timestamp.FromDateTimeOffset(startTime);
            timestampConverter = Timer.StartNew(startTime, () => interval);

            attributes.Add("MyStringAttributeKey", "MyStringAttributeValue");
            attributes.Add("MyLongAttributeKey", 123L);
            attributes.Add("MyBooleanAttributeKey", false);
            expectedAttributes = new Dictionary<string, object>(attributes)
            {
                ["MySingleStringAttributeKey"] = "MySingleStringAttributeValue",
            };
        }

        [Fact]
        public void ToSpanData_NoRecordEvents()
        {
            var activityLink = new Activity(SpanName).Start();
            activityLink.Stop();

            var activity = new Activity(SpanName).Start();
            
            var span =
                Span.StartSpan(
                    activity,
                    Tracestate.Empty,
                    noRecordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
            // Check that adding trace events after Span#End() does not throw any exception.
            foreach (var attribute in attributes)
            {
                span.SetAttribute(attribute);
            }

            span.AddEvent(Event.Create(EventDescription));
            span.AddEvent(EventDescription, attributes);
            span.AddLink(Link.FromActivity(activityLink));
            span.End();
            // exception.expect(IllegalStateException);
            Assert.Throws<InvalidOperationException>(() => ((Span)span).ToSpanData());
        }

        [Fact]
        public void GetSpanContextFromActivity()
        {
            var tracestate = Tracestate.Builder.Set("k1", "v1").Build();
            var activity = new Activity(SpanName).Start();
            activity.TraceStateString = tracestate.ToString();

            var span =
                Span.StartSpan(
                    activity,
                    tracestate,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
            Assert.NotNull(span.Context);
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

            var span =
                Span.StartSpan(
                    activity,
                    tracestate,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
            Assert.NotNull(span.Context);
            Assert.Equal(activity.TraceId, span.Context.TraceId);
            Assert.Equal(activity.SpanId, span.Context.SpanId);
            Assert.Equal(activity.ParentSpanId, ((Span)span).ParentSpanId);
            Assert.Equal(activity.ActivityTraceFlags, span.Context.TraceOptions);
            Assert.Same(tracestate, span.Context.Tracestate);
        }

        [Fact]
        public void NoEventsRecordedAfterEnd()
        {
            var activityLink = new Activity(SpanName).Start();
            activityLink.Stop();

            var activity = new Activity(SpanName).Start();

            var span =
                Span.StartSpan(
                    activity,
                    Tracestate.Empty,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
            span.End();
            // Check that adding trace events after Span#End() does not throw any exception and are not
            // recorded.
            foreach (var attribute in attributes)
            {
                span.SetAttribute(attribute);
            }

            span.SetAttribute(
                "MySingleStringAttributeKey",
                "MySingleStringAttributeValue");
            span.AddEvent(Event.Create(EventDescription));
            span.AddEvent(EventDescription, attributes);
            span.AddLink(Link.FromActivity(activityLink));
            var spanData = ((Span)span).ToSpanData();
            Assert.Equal(timestamp, spanData.StartTimestamp);
            Assert.Empty(spanData.Attributes.AttributeMap);
            Assert.Empty(spanData.Events.Events);
            Assert.Empty(spanData.Links.Links);
            Assert.Equal(Status.Ok, spanData.Status);
            Assert.Equal(timestamp, spanData.EndTimestamp);
        }

        [Fact]
        public void ToSpanData_ActiveSpan()
        {

            var activityLink = new Activity(SpanName);
            activityLink.Stop();

            var activity = new Activity(SpanName)
                .SetParentId(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom())
                .Start();

            var span =
                Span.StartSpan(
                    activity,
                    Tracestate.Empty,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);

            span.SetAttribute(
                "MySingleStringAttributeKey",
                "MySingleStringAttributeValue");
            foreach (var attribute in attributes)
            {
                span.SetAttribute(attribute);
            }

            interval = TimeSpan.FromMilliseconds(100);
            span.AddEvent(Event.Create(EventDescription));
            interval = TimeSpan.FromMilliseconds(200);
            span.AddEvent(EventDescription, attributes);
            interval = TimeSpan.FromMilliseconds(300);
            interval = TimeSpan.FromMilliseconds(400);
            var link = Link.FromActivity(activityLink);
            span.AddLink(link);
            var spanData = ((Span)span).ToSpanData();
            Assert.Equal(activity.TraceId, spanData.Context.TraceId);
            Assert.Equal(activity.SpanId, spanData.Context.SpanId);
            Assert.Equal(activity.ParentSpanId, spanData.ParentSpanId);
            Assert.Equal(activity.ActivityTraceFlags, spanData.Context.TraceOptions);
            Assert.Same(Tracestate.Empty, spanData.Context.Tracestate);

            Assert.Equal(SpanName, spanData.Name);
            Assert.Equal(activity.ParentSpanId, spanData.ParentSpanId);
            Assert.Equal(0, spanData.Attributes.DroppedAttributesCount);
            Assert.Equal(expectedAttributes, spanData.Attributes.AttributeMap);
            Assert.Equal(0, spanData.Events.DroppedEventsCount);
            Assert.Equal(2, spanData.Events.Events.Count());
            Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(100))),
                spanData.Events.Events.ToList()[0].Timestamp);
            Assert.Equal(Event.Create(EventDescription), spanData.Events.Events.ToList()[0].Event);
            Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(200))),
                spanData.Events.Events.ToList()[1].Timestamp);
            Assert.Equal(Event.Create(EventDescription, attributes), spanData.Events.Events.ToList()[1].Event);
            Assert.Equal(0, spanData.Links.DroppedLinksCount);
            Assert.Single(spanData.Links.Links);
            Assert.Equal(link, spanData.Links.Links.First());
            Assert.Equal(timestamp, spanData.StartTimestamp);
            Assert.Null(spanData.Status);
            Assert.Null(spanData.EndTimestamp);

            var startEndMock = Mock.Get<IStartEndHandler>(startEndHandler);
            startEndMock.Verify(s => s.OnStart(span), Times.Once);
        }

        [Fact]
        public void GoSpanData_EndedSpan()
        {
            var activityLink = new Activity(SpanName).Start();
            activityLink.Stop();

            var activity = new Activity(SpanName)
                .SetParentId(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom())
                .Start();

            var span =
                (Span)Span.StartSpan(
                    activity,
                    Tracestate.Empty,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);

            span.SetAttribute(
                "MySingleStringAttributeKey",
                "MySingleStringAttributeValue");
            foreach (var attribute in attributes)
            {
                span.SetAttribute(attribute);
            }

            interval = TimeSpan.FromMilliseconds(100);
            span.AddEvent(Event.Create(EventDescription));
            interval = TimeSpan.FromMilliseconds(200);
            span.AddEvent(EventDescription, attributes);
            interval = TimeSpan.FromMilliseconds(300);
            var link = Link.FromActivity(activityLink);
            span.AddLink(link);
            interval = TimeSpan.FromMilliseconds(400);
            span.Status = Status.Cancelled;
            span.End();

            var spanData = ((Span)span).ToSpanData();
            Assert.Equal(activity.TraceId, spanData.Context.TraceId);
            Assert.Equal(activity.SpanId, spanData.Context.SpanId);
            Assert.Equal(activity.ParentSpanId, spanData.ParentSpanId);
            Assert.Equal(activity.ActivityTraceFlags, spanData.Context.TraceOptions);

            Assert.Equal(SpanName, spanData.Name);
            Assert.Equal(activity.ParentSpanId, spanData.ParentSpanId);
            Assert.Equal(0, spanData.Attributes.DroppedAttributesCount);
            Assert.Equal(expectedAttributes, spanData.Attributes.AttributeMap);
            Assert.Equal(0, spanData.Events.DroppedEventsCount);
            Assert.Equal(2, spanData.Events.Events.Count());
            Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(100))),
                spanData.Events.Events.ToList()[0].Timestamp);
            Assert.Equal(Event.Create(EventDescription), spanData.Events.Events.ToList()[0].Event);
            Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(200))),
                spanData.Events.Events.ToList()[1].Timestamp);
            Assert.Equal(Event.Create(EventDescription, attributes), spanData.Events.Events.ToList()[1].Event);
            Assert.Equal(0, spanData.Links.DroppedLinksCount);
            Assert.Single(spanData.Links.Links);
            Assert.Equal(link, spanData.Links.Links.First());
            Assert.Equal(timestamp, spanData.StartTimestamp);
            Assert.Equal(Status.Cancelled, spanData.Status);
            Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(400))), spanData.EndTimestamp);

            var startEndMock = Mock.Get<IStartEndHandler>(startEndHandler);
            startEndMock.Verify(s => s.OnStart(span), Times.Once);
            startEndMock.Verify(s => s.OnEnd(span), Times.Once);
        }

        [Fact]
        public void Status_ViaSetStatus()
        {
            var activity = new Activity(SpanName).Start();

            var span =
                (Span)Span.StartSpan(
                    activity,
                    Tracestate.Empty,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
            interval = TimeSpan.FromMilliseconds(100);
            Assert.Equal(Status.Ok, span.Status);
            ((Span)span).Status = Status.Cancelled;
            Assert.Equal(Status.Cancelled, span.Status);
            span.End();
            Assert.Equal(Status.Cancelled, span.Status);

            var startEndMock = Mock.Get<IStartEndHandler>(startEndHandler);
            startEndMock.Verify(s => s.OnStart(span), Times.Once);
        }

        [Fact]
        public void status_ViaEndSpanOptions()
        {
            var activity = new Activity(SpanName).Start();

            var span =
                (Span)Span.StartSpan(
                    activity,
                    Tracestate.Empty,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
            interval = TimeSpan.FromMilliseconds(100);
            Assert.Equal(Status.Ok, span.Status);
            ((Span)span).Status = Status.Cancelled;
            Assert.Equal(Status.Cancelled, span.Status);
            span.Status = Status.Aborted;
            span.End();
            Assert.Equal(Status.Aborted, span.Status);

            var startEndMock = Mock.Get<IStartEndHandler>(startEndHandler);
            startEndMock.Verify(s => s.OnStart(span), Times.Once);
        }

        [Fact]
        public void DroppingAttributes()
        {
            var activity = new Activity(SpanName).Start();

            var maxNumberOfAttributes = 8;
            var traceParams =
                TraceParams.Default.ToBuilder().SetMaxNumberOfAttributes(maxNumberOfAttributes).Build();
            var span =
                Span.StartSpan(
                    activity,
                    Tracestate.Empty,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    traceParams,
                    startEndHandler,
                    timestampConverter);
            for (var i = 0; i < 2 * maxNumberOfAttributes; i++)
            {
                IDictionary<String, object> attributes = new Dictionary<String, object>();
                attributes.Add("MyStringAttributeKey" + i, i);
                foreach (var attribute in attributes)
                {
                    span.SetAttribute(attribute);
                }

            }

            var spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfAttributes, spanData.Attributes.DroppedAttributesCount);
            Assert.Equal(maxNumberOfAttributes, spanData.Attributes.AttributeMap.Count);
            for (var i = 0; i < maxNumberOfAttributes; i++)
            {
                Assert.Equal(
                    i + maxNumberOfAttributes,
                    spanData
                        .Attributes
                        .AttributeMap["MyStringAttributeKey" + (i + maxNumberOfAttributes)]);
            }

            span.End();
            spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfAttributes, spanData.Attributes.DroppedAttributesCount);
            Assert.Equal(maxNumberOfAttributes, spanData.Attributes.AttributeMap.Count);
            for (var i = 0; i < maxNumberOfAttributes; i++)
            {
                Assert.Equal(
                    i + maxNumberOfAttributes,
                    spanData
                        .Attributes
                        .AttributeMap["MyStringAttributeKey" + (i + maxNumberOfAttributes)]);
            }
        }

        [Fact]
        public void DroppingAndAddingAttributes()
        {
            var activity = new Activity(SpanName).Start();

            var maxNumberOfAttributes = 8;
            var traceParams =
                TraceParams.Default.ToBuilder().SetMaxNumberOfAttributes(maxNumberOfAttributes).Build();
            var span =
                Span.StartSpan(
                    activity,
                    Tracestate.Empty,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    traceParams,
                    startEndHandler,
                    timestampConverter);
            for (var i = 0; i < 2 * maxNumberOfAttributes; i++)
            {
                IDictionary<String, object> attributes = new Dictionary<String, object>();
                attributes.Add("MyStringAttributeKey" + i, i);
                foreach (var attribute in attributes)
                {
                    span.SetAttribute(attribute);
                }

            }

            var spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfAttributes, spanData.Attributes.DroppedAttributesCount);
            Assert.Equal(maxNumberOfAttributes, spanData.Attributes.AttributeMap.Count);
            for (var i = 0; i < maxNumberOfAttributes; i++)
            {
                Assert.Equal(
                    i + maxNumberOfAttributes,
                    spanData
                        .Attributes
                        .AttributeMap["MyStringAttributeKey" + (i + maxNumberOfAttributes)]);
            }

            for (var i = 0; i < maxNumberOfAttributes / 2; i++)
            {
                IDictionary<String, object> attributes = new Dictionary<String, object>();
                attributes.Add("MyStringAttributeKey" + i, i);
                foreach (var attribute in attributes)
                {
                    span.SetAttribute(attribute);
                }

            }

            spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfAttributes * 3 / 2, spanData.Attributes.DroppedAttributesCount);
            Assert.Equal(maxNumberOfAttributes, spanData.Attributes.AttributeMap.Count);
            // Test that we still have in the attributes map the latest maxNumberOfAttributes / 2 entries.
            for (var i = 0; i < maxNumberOfAttributes / 2; i++)
            {
                Assert.Equal(
                    i + maxNumberOfAttributes * 3 / 2,
                    spanData
                        .Attributes
                        .AttributeMap["MyStringAttributeKey" + (i + maxNumberOfAttributes * 3 / 2)]);
            }

            // Test that we have the newest re-added initial entries.
            for (var i = 0; i < maxNumberOfAttributes / 2; i++)
            {
                Assert.Equal(i,
                    spanData.Attributes.AttributeMap["MyStringAttributeKey" + i]);
            }
        }

        [Fact]
        public void DroppingEvents()
        {
            var activity = new Activity(SpanName).Start();

            var maxNumberOfEvents = 8;
            var traceParams =
                TraceParams.Default.ToBuilder().SetMaxNumberOfEvents(maxNumberOfEvents).Build();
            var span =
                Span.StartSpan(
                    activity,
                    Tracestate.Empty,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    traceParams,
                    startEndHandler,
                    timestampConverter);
            var testEvent = Event.Create(EventDescription);
            var i = 0;
            for (i = 0; i < 2 * maxNumberOfEvents; i++)
            {
                span.AddEvent(testEvent);
                interval += TimeSpan.FromMilliseconds(100);
            }

            var spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfEvents, spanData.Events.DroppedEventsCount);
            Assert.Equal(maxNumberOfEvents, spanData.Events.Events.Count());
            i = 0;
            foreach (var te in spanData.Events.Events)
            {
                Assert.Equal(
                    timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(100 * (maxNumberOfEvents + i)))),
                    te.Timestamp);
                Assert.Equal(testEvent, te.Event);
                i++;
            }

            span.End();
            spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfEvents, spanData.Events.DroppedEventsCount);
            Assert.Equal(maxNumberOfEvents, spanData.Events.Events.Count());
            i = 0;
            foreach (var te in spanData.Events.Events)
            {
                Assert.Equal(
                    timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(100 * (maxNumberOfEvents + i)))),
                    te.Timestamp);
                Assert.Equal(testEvent, te.Event);
                i++;
            }
        }

        [Fact]
        public void DroppingLinks()
        {
            var activityLink = new Activity(SpanName).Start();
            activityLink.Stop();

            var activity = new Activity(SpanName).Start();

            var maxNumberOfLinks = 8;
            var traceParams =
                TraceParams.Default.ToBuilder().SetMaxNumberOfLinks(maxNumberOfLinks).Build();
            var span =
                Span.StartSpan(
                    activity,
                    Tracestate.Empty, 
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    traceParams,
                    startEndHandler,
                    timestampConverter);
            var link = Link.FromActivity(activityLink);
            for (var i = 0; i < 2 * maxNumberOfLinks; i++)
            {
                span.AddLink(link);
            }

            var spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfLinks, spanData.Links.DroppedLinksCount);
            Assert.Equal(maxNumberOfLinks, spanData.Links.Links.Count());
            foreach (var actualLink in spanData.Links.Links)
            {
                Assert.Equal(link, actualLink);
            }

            span.End();
            spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfLinks, spanData.Links.DroppedLinksCount);
            Assert.Equal(maxNumberOfLinks, spanData.Links.Links.Count());
            foreach (var actualLink in spanData.Links.Links)
            {
                Assert.Equal(link, actualLink);
            }
        }

        [Fact]
        public void SampleToLocalSpanStore()
        {
            var activity1 = new Activity(SpanName).Start();

            var span =
                (Span)Span.StartSpan(
                    activity1,
                    Tracestate.Empty,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
            span.IsSampleToLocalSpanStore = true;
            span.End();

            Assert.True(((Span)span).IsSampleToLocalSpanStore);

            var activity2 = new Activity(SpanName).Start();
            var span2 =
                Span.StartSpan(
                    activity2,
                    Tracestate.Empty,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
            span2.End();

            Assert.False(((Span)span2).IsSampleToLocalSpanStore);

            var startEndMock = Mock.Get<IStartEndHandler>(startEndHandler);

            startEndMock.Verify(s => s.OnEnd(span), Times.Exactly(1));
            startEndMock.Verify(s => s.OnEnd(span2), Times.Exactly(1));
        }

        [Fact]
        public void SampleToLocalSpanStore_RunningSpan()
        {
            var activity = new Activity(SpanName).Start();

            var span =
                Span.StartSpan(
                    activity,
                    Tracestate.Empty,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);

            Assert.Throws<InvalidOperationException>(() => ((Span)span).IsSampleToLocalSpanStore);
        }

        [Fact]
        public void BadArguments()
        {
            var activity = new Activity(SpanName).Start();

            var span =
                Span.StartSpan(
                    activity,
                    Tracestate.Empty,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);

            Assert.Throws<ArgumentNullException>(() => span.Status = null);
            Assert.Throws<ArgumentNullException>(() => span.UpdateName(null));
            Assert.Throws<ArgumentNullException>(() => span.SetAttribute(null, string.Empty));
            Assert.Throws<ArgumentNullException>(() => span.SetAttribute(string.Empty, null));
            Assert.Throws<ArgumentNullException>(() =>
                span.SetAttribute(null, "foo"));
            Assert.Throws<ArgumentNullException>(() => span.SetAttribute(null, 1L));
            Assert.Throws<ArgumentNullException>(() => span.SetAttribute(null, 0.1d));
            Assert.Throws<ArgumentNullException>(() => span.SetAttribute(null, true));
            Assert.Throws<ArgumentNullException>(() => span.AddEvent((string)null));
            Assert.Throws<ArgumentNullException>(() => span.AddEvent((IEvent)null));
            Assert.Throws<ArgumentNullException>(() => span.AddLink(null));
        }

        [Fact]
        public void SetSampleTo()
        {
            var activity = new Activity(SpanName).Start();

            var span = (Span)Span.StartSpan(
                activity,
                Tracestate.Empty,
                recordSpanOptions,
                SpanName,
                SpanKind.Internal,
                TraceParams.Default,
                startEndHandler,
                timestampConverter);

            span.IsSampleToLocalSpanStore = true;
            span.End();
            Assert.True(span.IsSampleToLocalSpanStore);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void EndSpanStopsActivity(bool recordEvents)
        {
            var parentActivity = new Activity(SpanName).Start();
            var activity = new Activity(SpanName).Start();

            var span =
                Span.StartSpan(
                    activity,
                    Tracestate.Empty, 
                    recordEvents ? recordSpanOptions : noRecordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter,
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

            var span =
                Span.StartSpan(
                    activity,
                    Tracestate.Empty,
                    recordEvents ? recordSpanOptions : noRecordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter,
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

            var span =
                Span.StartSpan(
                    activity,
                    Tracestate.Empty,
                    recordEvents ? recordSpanOptions : noRecordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter,
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
    }
}
