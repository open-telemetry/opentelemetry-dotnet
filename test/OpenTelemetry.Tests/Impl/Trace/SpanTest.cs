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
    using System.Linq;
    using Moq;
    using OpenTelemetry.Common;
    using OpenTelemetry.Internal;
    using OpenTelemetry.Trace.Config;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Trace.Internal;
    using Xunit;

    public class SpanTest
    {
        private static readonly String SPAN_NAME = "MySpanName";
        private static readonly String EVENT_DESCRIPTION = "MyEvent";
        private readonly RandomGenerator random = new RandomGenerator(1234);
        private readonly SpanContext spanContext;
        private readonly SpanId parentSpanId;
        private TimeSpan interval = TimeSpan.FromMilliseconds(0);
        private readonly DateTimeOffset startTime = DateTimeOffset.Now;
        private readonly Timestamp timestamp;
        private readonly Timer timestampConverter;
        private readonly SpanOptions noRecordSpanOptions = SpanOptions.None;
        private readonly SpanOptions recordSpanOptions = SpanOptions.RecordEvents;
        private readonly IDictionary<String, IAttributeValue> attributes = new Dictionary<String, IAttributeValue>();
        private readonly IDictionary<String, IAttributeValue> expectedAttributes;
        private IStartEndHandler startEndHandler = Mock.Of<IStartEndHandler>();

        public SpanTest()
        {
            timestamp = Timestamp.FromDateTimeOffset(startTime);
            timestampConverter = Timer.StartNew(startTime, () => interval);
            spanContext = SpanContext.Create(TraceId.GenerateRandomId(random), SpanId.GenerateRandomId(random), OpenTelemetry.Trace.TraceOptions.Default, Tracestate.Empty);
            parentSpanId = SpanId.GenerateRandomId(random);
            attributes.Add(
                "MyStringAttributeKey", AttributeValue.StringAttributeValue("MyStringAttributeValue"));
            attributes.Add("MyLongAttributeKey", AttributeValue.LongAttributeValue(123L));
            attributes.Add("MyBooleanAttributeKey", AttributeValue.BooleanAttributeValue(false));
            expectedAttributes = new Dictionary<String, IAttributeValue>(attributes);
            expectedAttributes.Add(
                "MySingleStringAttributeKey",
                AttributeValue.StringAttributeValue("MySingleStringAttributeValue"));
        }

        [Fact]
        public void ToSpanData_NoRecordEvents()
        {
            var span =
                Span.StartSpan(
                    spanContext,
                    noRecordSpanOptions,
                    SPAN_NAME,
                    SpanKind.Internal,
                    parentSpanId,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
            // Check that adding trace events after Span#End() does not throw any exception.
            foreach (var attribute in attributes)
            {
                span.SetAttribute(attribute.Key, attribute.Value);
            }
            span.AddEvent(Event.Create(EVENT_DESCRIPTION));
            span.AddEvent(EVENT_DESCRIPTION, attributes);
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
                    SPAN_NAME,
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
                span.SetAttribute(attribute.Key, attribute.Value);
            }
            span.SetAttribute(
                "MySingleStringAttributeKey",
                AttributeValue.StringAttributeValue("MySingleStringAttributeValue"));
            span.AddEvent(Event.Create(EVENT_DESCRIPTION));
            span.AddEvent(EVENT_DESCRIPTION, attributes);
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
                    SPAN_NAME,
                    SpanKind.Internal,
                    parentSpanId,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
   
            span.SetAttribute(
                "MySingleStringAttributeKey",
                AttributeValue.StringAttributeValue("MySingleStringAttributeValue"));
            foreach (var attribute in attributes)
            {
                span.SetAttribute(attribute.Key, attribute.Value);
            }

            interval = TimeSpan.FromMilliseconds(100);
            span.AddEvent(Event.Create(EVENT_DESCRIPTION));
            interval = TimeSpan.FromMilliseconds(200);
            span.AddEvent(EVENT_DESCRIPTION, attributes);
            interval = TimeSpan.FromMilliseconds(300);
            interval = TimeSpan.FromMilliseconds(400);
            var link = Link.FromSpanContext(spanContext);
            span.AddLink(link);
            var spanData = ((Span)span).ToSpanData();
            Assert.Equal(spanContext, spanData.Context);
            Assert.Equal(SPAN_NAME, spanData.Name);
            Assert.Equal(parentSpanId, spanData.ParentSpanId);
            Assert.Equal(0, spanData.Attributes.DroppedAttributesCount);
            Assert.Equal(expectedAttributes, spanData.Attributes.AttributeMap); 
            Assert.Equal(0, spanData.Events.DroppedEventsCount);
            Assert.Equal(2, spanData.Events.Events.Count());
            Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(100))), spanData.Events.Events.ToList()[0].Timestamp);
            Assert.Equal(Event.Create(EVENT_DESCRIPTION), spanData.Events.Events.ToList()[0].Event);
            Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(200))), spanData.Events.Events.ToList()[1].Timestamp);
            Assert.Equal(Event.Create(EVENT_DESCRIPTION, attributes), spanData.Events.Events.ToList()[1].Event);
            Assert.Equal(0, spanData.Links.DroppedLinksCount);
            Assert.Single(spanData.Links.Links);
            Assert.Equal(link, spanData.Links.Links.First());
            Assert.Equal(timestamp, spanData.StartTimestamp);
            Assert.Null(spanData.Status);
            Assert.Null(spanData.EndTimestamp);

            var startEndMock = Mock.Get<IStartEndHandler>(startEndHandler);
            var spanBase = span as SpanBase;
            startEndMock.Verify(s => s.OnStart(spanBase), Times.Once);  
        }

        [Fact]
        public void GoSpanData_EndedSpan()
        {
            var span =
                (Span)Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
                    SpanKind.Internal,
                    parentSpanId,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
     
            span.SetAttribute(
                "MySingleStringAttributeKey",
                AttributeValue.StringAttributeValue("MySingleStringAttributeValue"));
            foreach (var attribute in attributes)
            {
                span.SetAttribute(attribute.Key, attribute.Value);
            }

            interval = TimeSpan.FromMilliseconds(100);
            span.AddEvent(Event.Create(EVENT_DESCRIPTION));
            interval = TimeSpan.FromMilliseconds(200);
            span.AddEvent(EVENT_DESCRIPTION, attributes);
            interval = TimeSpan.FromMilliseconds(300);
            var link = Link.FromSpanContext(spanContext);
            span.AddLink(link);
            interval = TimeSpan.FromMilliseconds(400);
            span.End(EndSpanOptions.Builder().SetStatus(Status.Cancelled).Build());
          
            var spanData = ((Span)span).ToSpanData();
            Assert.Equal(spanContext, spanData.Context);
            Assert.Equal(SPAN_NAME, spanData.Name);
            Assert.Equal(parentSpanId, spanData.ParentSpanId);
            Assert.Equal(0, spanData.Attributes.DroppedAttributesCount);
            Assert.Equal(expectedAttributes, spanData.Attributes.AttributeMap);
            Assert.Equal(0, spanData.Events.DroppedEventsCount);
            Assert.Equal(2, spanData.Events.Events.Count());
            Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(100))), spanData.Events.Events.ToList()[0].Timestamp);
            Assert.Equal(Event.Create(EVENT_DESCRIPTION), spanData.Events.Events.ToList()[0].Event);
            Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(200))), spanData.Events.Events.ToList()[1].Timestamp);
            Assert.Equal(Event.Create(EVENT_DESCRIPTION, attributes), spanData.Events.Events.ToList()[1].Event);
            Assert.Equal(0, spanData.Links.DroppedLinksCount);
            Assert.Single(spanData.Links.Links);
            Assert.Equal(link, spanData.Links.Links.First());
            Assert.Equal(timestamp, spanData.StartTimestamp);
            Assert.Equal(Status.Cancelled, spanData.Status);
            Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(400))), spanData.EndTimestamp);

            var startEndMock = Mock.Get<IStartEndHandler>(startEndHandler);
            var spanBase = span as SpanBase;
            startEndMock.Verify(s => s.OnStart(spanBase), Times.Once);
            startEndMock.Verify(s => s.OnEnd(spanBase), Times.Once);
        }

        [Fact]
        public void Status_ViaSetStatus()
        {
            var span =
                (Span)Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
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
            var spanBase = span as SpanBase;
            startEndMock.Verify(s => s.OnStart(spanBase), Times.Once);
        }

        [Fact]
        public void status_ViaEndSpanOptions()
        {
            var span =
                (Span)Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
                    SpanKind.Internal,
                    parentSpanId,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
            interval = TimeSpan.FromMilliseconds(100);
            Assert.Equal(Status.Ok, span.Status);
            ((Span)span).Status = Status.Cancelled;
            Assert.Equal(Status.Cancelled, span.Status);
            span.End(EndSpanOptions.Builder().SetStatus(Status.Aborted).Build());
            Assert.Equal(Status.Aborted, span.Status);

            var startEndMock = Mock.Get<IStartEndHandler>(startEndHandler);
            var spanBase = span as SpanBase;
            startEndMock.Verify(s => s.OnStart(spanBase), Times.Once);
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
                    SPAN_NAME,
                    SpanKind.Internal,
                    parentSpanId,
                    traceParams,
                    startEndHandler,
                    timestampConverter);
            for (var i = 0; i < 2 * maxNumberOfAttributes; i++)
            {
                IDictionary<String, IAttributeValue> attributes = new Dictionary<String, IAttributeValue>();
                attributes.Add("MyStringAttributeKey" + i, AttributeValue.LongAttributeValue(i));
                foreach (var attribute in attributes)
                {
                    span.SetAttribute(attribute.Key, attribute.Value);
                }

            }
            var spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfAttributes, spanData.Attributes.DroppedAttributesCount);
            Assert.Equal(maxNumberOfAttributes, spanData.Attributes.AttributeMap.Count);
            for (var i = 0; i < maxNumberOfAttributes; i++)
            {
                Assert.Equal(
                    AttributeValue.LongAttributeValue(i + maxNumberOfAttributes),
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
                    AttributeValue.LongAttributeValue(i + maxNumberOfAttributes),
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
                    SPAN_NAME,
                    SpanKind.Internal,
                    parentSpanId,
                    traceParams,
                    startEndHandler,
                    timestampConverter);
            for (var i = 0; i < 2 * maxNumberOfAttributes; i++)
            {
                IDictionary<String, IAttributeValue> attributes = new Dictionary<String, IAttributeValue>();
                attributes.Add("MyStringAttributeKey" + i, AttributeValue.LongAttributeValue(i));
                foreach (var attribute in attributes)
                {
                    span.SetAttribute(attribute.Key, attribute.Value);
                }

            }
            var spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfAttributes, spanData.Attributes.DroppedAttributesCount);
            Assert.Equal(maxNumberOfAttributes, spanData.Attributes.AttributeMap.Count);
            for (var i = 0; i < maxNumberOfAttributes; i++)
            {
                Assert.Equal(
                    AttributeValue.LongAttributeValue(i + maxNumberOfAttributes),
                    spanData
                            .Attributes
                            .AttributeMap["MyStringAttributeKey" + (i + maxNumberOfAttributes)]);
            }
            for (var i = 0; i < maxNumberOfAttributes / 2; i++)
            {
                IDictionary<String, IAttributeValue> attributes = new Dictionary<String, IAttributeValue>();
                attributes.Add("MyStringAttributeKey" + i, AttributeValue.LongAttributeValue(i));
                foreach (var attribute in attributes)
                {
                    span.SetAttribute(attribute.Key, attribute.Value);
                }

            }
            spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfAttributes * 3 / 2, spanData.Attributes.DroppedAttributesCount);
            Assert.Equal(maxNumberOfAttributes, spanData.Attributes.AttributeMap.Count);
            // Test that we still have in the attributes map the latest maxNumberOfAttributes / 2 entries.
            for (var i = 0; i < maxNumberOfAttributes / 2; i++)
            {
                Assert.Equal(
                    AttributeValue.LongAttributeValue(i + maxNumberOfAttributes * 3 / 2),
                    spanData
                            .Attributes
                            .AttributeMap["MyStringAttributeKey" + (i + maxNumberOfAttributes * 3 / 2)]);
            }
            // Test that we have the newest re-added initial entries.
            for (var i = 0; i < maxNumberOfAttributes / 2; i++)
            {
                Assert.Equal(AttributeValue.LongAttributeValue(i), spanData.Attributes.AttributeMap["MyStringAttributeKey" + i]);
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
                    SPAN_NAME,
                    SpanKind.Internal,
                    parentSpanId,
                    traceParams,
                    startEndHandler,
                    timestampConverter);
            var testEvent = Event.Create(EVENT_DESCRIPTION);
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
                Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(100 * (maxNumberOfEvents + i)))), te.Timestamp);
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
                Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(100 * (maxNumberOfEvents + i)))), te.Timestamp);
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
                    SPAN_NAME,
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
                    SPAN_NAME,
                    SpanKind.Internal,
                    parentSpanId,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
            span.End(EndSpanOptions.Builder().SetSampleToLocalSpanStore(true).Build());

            Assert.True(((Span)span).IsSampleToLocalSpanStore);
            var span2 =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
                    SpanKind.Internal,
                    parentSpanId,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
            span2.End();

            Assert.False(((Span)span2).IsSampleToLocalSpanStore);

            var startEndMock = Mock.Get<IStartEndHandler>(startEndHandler);
            var spanBase = span as SpanBase;
            startEndMock.Verify(s => s.OnEnd(spanBase), Times.Exactly(1));
            var spanBase2 = span2 as SpanBase;
            startEndMock.Verify(s => s.OnEnd(spanBase2), Times.Exactly(1));
        }

        [Fact]
        public void SampleToLocalSpanStore_RunningSpan()
        {
            var span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
                    SpanKind.Internal,
                    parentSpanId,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);

            Assert.Throws<InvalidOperationException>(() => ((Span)span).IsSampleToLocalSpanStore);
        }
    }
}
