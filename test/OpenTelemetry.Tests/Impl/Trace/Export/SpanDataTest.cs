// <copyright file="SpanDataTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Export.Test
{
    using System.Collections.Generic;
    using OpenTelemetry.Common;
    using OpenTelemetry.Trace.Internal;
    using Xunit;

    public class SpanDataTest
    {
        private static readonly Timestamp startTimestamp = Timestamp.Create(123, 456);
        private static readonly Timestamp eventTimestamp1 = Timestamp.Create(123, 457);
        private static readonly Timestamp eventTimestamp2 = Timestamp.Create(123, 458);
        private static readonly Timestamp eventTimestamp3 = Timestamp.Create(123, 459);
        private static readonly Timestamp endTimestamp = Timestamp.Create(123, 460);
        private static readonly string SPAN_NAME = "MySpanName";
        private static readonly string EVENT_TEXT = "MyEventText";
        private static readonly IEvent spanEvent = Event.Create(EVENT_TEXT);
        private static readonly Status status = Status.DeadlineExceeded.WithDescription("TooSlow");
        private static readonly SpanKind kind = SpanKind.Client;
        private static readonly int CHILD_SPAN_COUNT = 13;
        private readonly IRandomGenerator random = new RandomGenerator(1234);
        private readonly SpanContext spanContext;
        private readonly SpanId parentSpanId; 
        private readonly IDictionary<string, IAttributeValue> attributesMap = new Dictionary<string, IAttributeValue>();
        private readonly List<ITimedEvent<IEvent>> eventList = new List<ITimedEvent<IEvent>>();
        private readonly List<ILink> linksList = new List<ILink>();

        private IAttributes attributes;
        private ITimedEvents<IEvent> events;
        // private TimedEvents<NetworkEvent> networkEvents;
        private LinkList links;

        public SpanDataTest()
        {
            spanContext = SpanContext.Create(TraceId.GenerateRandomId(random), SpanId.GenerateRandomId(random), TraceOptions.Default, Tracestate.Empty);
            parentSpanId = SpanId.GenerateRandomId(random);

            attributesMap.Add("MyAttributeKey1", AttributeValue.LongAttributeValue(10));
            attributesMap.Add("MyAttributeKey2", AttributeValue.BooleanAttributeValue(true));
            attributes = Attributes.Create(attributesMap, 1);

            eventList.Add(TimedEvent<IEvent>.Create(eventTimestamp1, spanEvent));
            eventList.Add(TimedEvent<IEvent>.Create(eventTimestamp3, spanEvent));
            events = TimedEvents<IEvent>.Create(eventList, 2);

            linksList.Add(Link.FromSpanContext(spanContext));
            links = LinkList.Create(linksList, 0);
        }

        [Fact]
        public void SpanData_AllValues()
        {
            ISpanData spanData =
                SpanData.Create(
                    spanContext,
                    parentSpanId,
                    SPAN_NAME,
                    startTimestamp,
                    attributes,
                    events,
                    links,
                    CHILD_SPAN_COUNT,
                    status,
                    kind,
                    endTimestamp);
            Assert.Equal(spanContext, spanData.Context);
            Assert.Equal(parentSpanId, spanData.ParentSpanId);
            Assert.Equal(SPAN_NAME, spanData.Name);
            Assert.Equal(startTimestamp, spanData.StartTimestamp);
            Assert.Equal(attributes, spanData.Attributes);
            Assert.Equal(events, spanData.Events);
            Assert.Equal(links, spanData.Links);
            Assert.Equal(CHILD_SPAN_COUNT, spanData.ChildSpanCount);
            Assert.Equal(status, spanData.Status);
            Assert.Equal(endTimestamp, spanData.EndTimestamp);
        }

        [Fact]
        public void SpanData_RootActiveSpan()
        {
            ISpanData spanData =
                SpanData.Create(
                    spanContext,
                    null,
                    SPAN_NAME,
                    startTimestamp,
                    attributes,
                    events,
                    links,
                    null,
                    null,
                    kind,
                    null);
            Assert.Equal(spanContext, spanData.Context);
            Assert.Null(spanData.ParentSpanId);
            Assert.Equal(SPAN_NAME, spanData.Name);
            Assert.Equal(startTimestamp, spanData.StartTimestamp);
            Assert.Equal(attributes, spanData.Attributes);
            Assert.Equal(events, spanData.Events);
            Assert.Equal(links, spanData.Links);
            Assert.Null(spanData.ChildSpanCount);
            Assert.Null(spanData.Status);
            Assert.Null(spanData.EndTimestamp);
        }

        [Fact]
        public void SpanData_AllDataEmpty()
        {
            ISpanData spanData =
                SpanData.Create(
                    spanContext,
                    parentSpanId,
                    SPAN_NAME,
                    startTimestamp,
                    Attributes.Create(new Dictionary<string, IAttributeValue>(), 0),
                    TimedEvents<IEvent>.Create(new List<ITimedEvent<IEvent>>(), 0),
                    LinkList.Create(new List<ILink>(), 0),
                    0,
                    status,
                    kind,
                    endTimestamp);

            Assert.Equal(spanContext, spanData.Context);
            Assert.Equal(parentSpanId, spanData.ParentSpanId);
            Assert.Equal(SPAN_NAME, spanData.Name);
            Assert.Equal(startTimestamp, spanData.StartTimestamp);
            Assert.Empty(spanData.Attributes.AttributeMap);
            Assert.Empty(spanData.Events.Events);
            Assert.Empty(spanData.Links.Links);
            Assert.Equal(0, spanData.ChildSpanCount);
            Assert.Equal(status, spanData.Status);
            Assert.Equal(endTimestamp, spanData.EndTimestamp);
        }

        [Fact]
        public void SpanDataEquals()
        {
            ISpanData allSpanData1 =
                SpanData.Create(
                    spanContext,
                    parentSpanId,
                    SPAN_NAME,
                    startTimestamp,
                    attributes,
                    events,
                    links,
                    CHILD_SPAN_COUNT,
                    status,
                    kind,
                    endTimestamp);
            ISpanData allSpanData2 =
                SpanData.Create(
                    spanContext,
                    parentSpanId,
                    SPAN_NAME,
                    startTimestamp,
                    attributes,
                    events,
                    links,
                    CHILD_SPAN_COUNT,
                    status,
                    kind,
                    endTimestamp);
            ISpanData emptySpanData =
                SpanData.Create(
                    spanContext,
                    parentSpanId,
                    SPAN_NAME,
                    startTimestamp,
                    Attributes.Create(new Dictionary<string, IAttributeValue>(), 0),
                    TimedEvents<IEvent>.Create(new List<ITimedEvent<IEvent>>(), 0),
                    LinkList.Create(new List<ILink>(), 0),
                    0,
                    status,
                    kind,
                    endTimestamp);

            Assert.Equal(allSpanData1, allSpanData2);
            Assert.NotEqual(emptySpanData, allSpanData1);
            Assert.NotEqual(emptySpanData, allSpanData2);

        }

        [Fact]
        public void SpanData_ToString()
        {
            string spanDataString =
                SpanData.Create(
                        spanContext,
                        parentSpanId,
                        SPAN_NAME,
                        startTimestamp,
                        attributes,
                        events,
                        links,
                        CHILD_SPAN_COUNT,
                        status,
                        kind,
                        endTimestamp)
                    .ToString();
            Assert.Contains(spanContext.ToString(), spanDataString);
            Assert.Contains(parentSpanId.ToString(), spanDataString);
            Assert.Contains(SPAN_NAME, spanDataString);
            Assert.Contains(startTimestamp.ToString(), spanDataString);
            Assert.Contains(attributes.ToString(), spanDataString);
            Assert.Contains(events.ToString(), spanDataString);
            Assert.Contains(links.ToString(), spanDataString);
            Assert.Contains(status.ToString(), spanDataString);
            Assert.Contains(endTimestamp.ToString(), spanDataString);
        }
    }
}
