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
    using OpenTelemetry.Trace.Internal;
    using Xunit;

    public class SpanTest
    {
        private const string SpanName = "MySpanName";
        private const string EventDescription = "MyEvent";
        private readonly SpanContext spanContext;
        private readonly ActivitySpanId parentSpanId;
        private TimeSpan interval = TimeSpan.FromMilliseconds(0);
        private readonly DateTimeOffset startTime = DateTimeOffset.Now;
        private readonly Timestamp timestamp;
        private readonly Timer timestampConverter;
        private readonly SpanOptions noRecordSpanOptions = SpanOptions.None;
        private readonly SpanOptions recordSpanOptions = SpanOptions.RecordEvents;
        private readonly IDictionary<String, object> attributes = new Dictionary<String, object>();
        private readonly IDictionary<String, object> expectedAttributes;
        private readonly IStartEndHandler startEndHandler = Mock.Of<IStartEndHandler>();

        public SpanTest()
        {
            timestamp = Timestamp.FromDateTimeOffset(startTime);
            timestampConverter = Timer.StartNew(startTime, () => interval);
            spanContext = SpanContext.Create(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None, Tracestate.Empty);
            parentSpanId = ActivitySpanId.CreateRandom();
            attributes.Add(
                "MyStringAttributeKey", AttributeValue.StringAttributeValue("MyStringAttributeValue"));
            attributes.Add("MyLongAttributeKey", AttributeValue.LongAttributeValue(123L));
            attributes.Add("MyBooleanAttributeKey", AttributeValue.BooleanAttributeValue(false));
            expectedAttributes = new Dictionary<String, object>(attributes);
            expectedAttributes.Add(
                "MySingleStringAttributeKey",
                "MySingleStringAttributeValue");
        }

        [Fact]
        public void ToSpanData_NoRecordEvents()
        {
            var span =
                Span.StartSpan(
                    spanContext,
                    noRecordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    parentSpanId,
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
            span.AddLink(Link.FromSpanContext(spanContext));
            span.End();
            // exception.expect(IllegalStateException);
            Assert.Throws<InvalidOperationException>(() => ((Span)span).ToSpanData());
        }

        [Fact]
        public void NoEventsRecordedAfterEnd()
        {
            var span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    parentSpanId,
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
            span.AddLink(Link.FromSpanContext(spanContext));
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
            var span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    parentSpanId,
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
            var link = Link.FromSpanContext(spanContext);
            span.AddLink(link);
            var spanData = ((Span)span).ToSpanData();
            Assert.Equal(spanContext, spanData.Context);
            Assert.Equal(SpanName, spanData.Name);
            Assert.Equal(parentSpanId, spanData.ParentSpanId);
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
            var span =
                (Span)Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    parentSpanId,
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
            var link = Link.FromSpanContext(spanContext);
            span.AddLink(link);
            interval = TimeSpan.FromMilliseconds(400);
            span.Status = Status.Cancelled;
            span.End();

            var spanData = ((Span)span).ToSpanData();
            Assert.Equal(spanContext, spanData.Context);
            Assert.Equal(SpanName, spanData.Name);
            Assert.Equal(parentSpanId, spanData.ParentSpanId);
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
            var span =
                (Span)Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    parentSpanId,
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
            var span =
                (Span)Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    parentSpanId,
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
            var maxNumberOfAttributes = 8;
            var traceParams =
                TraceParams.Default.ToBuilder().SetMaxNumberOfAttributes(maxNumberOfAttributes).Build();
            var span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    parentSpanId,
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
            var maxNumberOfAttributes = 8;
            var traceParams =
                TraceParams.Default.ToBuilder().SetMaxNumberOfAttributes(maxNumberOfAttributes).Build();
            var span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    parentSpanId,
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
            var maxNumberOfEvents = 8;
            var traceParams =
                TraceParams.Default.ToBuilder().SetMaxNumberOfEvents(maxNumberOfEvents).Build();
            var span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    parentSpanId,
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
            var maxNumberOfLinks = 8;
            var traceParams =
                TraceParams.Default.ToBuilder().SetMaxNumberOfLinks(maxNumberOfLinks).Build();
            var span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    parentSpanId,
                    traceParams,
                    startEndHandler,
                    timestampConverter);
            var link = Link.FromSpanContext(spanContext);
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
            var span =
                (Span)Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    parentSpanId,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
            span.IsSampleToLocalSpanStore = true;
            span.End();

            Assert.True(((Span)span).IsSampleToLocalSpanStore);
            var span2 =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    parentSpanId,
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
            var span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    parentSpanId,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);

            Assert.Throws<InvalidOperationException>(() => ((Span)span).IsSampleToLocalSpanStore);
        }

        [Fact]
        public void BadArguments()
        {
            var span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SpanName,
                    SpanKind.Internal,
                    parentSpanId,
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
            var span = (Span)Span.StartSpan(
                spanContext,
                recordSpanOptions,
                SpanName,
                SpanKind.Internal,
                parentSpanId,
                TraceParams.Default,
                startEndHandler,
                timestampConverter);

            span.IsSampleToLocalSpanStore = true;
            span.End();
            Assert.True(span.IsSampleToLocalSpanStore);
        }
    }
}
