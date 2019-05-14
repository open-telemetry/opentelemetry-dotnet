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
        private static readonly String ANNOTATION_DESCRIPTION = "MyAnnotation";
        private readonly RandomGenerator random = new RandomGenerator(1234);
        private readonly ISpanContext spanContext;
        private readonly ISpanId parentSpanId;
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
            ISpan span =
                Span.StartSpan(
                    spanContext,
                    noRecordSpanOptions,
                    SPAN_NAME,
                    parentSpanId,
                    false,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
            // Check that adding trace events after Span#End() does not throw any exception.
            span.SetAttributes(attributes);
            span.AddAnnotation(Annotation.FromDescription(ANNOTATION_DESCRIPTION));
            span.AddAnnotation(ANNOTATION_DESCRIPTION, attributes);
            span.AddMessageEvent(
                MessageEvent.Builder(MessageEventType.Received, 1).SetUncompressedMessageSize(3).Build());
            span.AddLink(Link.FromSpanContext(spanContext, LinkType.ChildLinkedSpan));
            span.End();
            // exception.expect(IllegalStateException);
            Assert.Throws<InvalidOperationException>(() => ((Span)span).ToSpanData());
        }

        [Fact]
        public void NoEventsRecordedAfterEnd()
        {
            ISpan span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
                    parentSpanId,
                    false,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
            span.End();
            // Check that adding trace events after Span#End() does not throw any exception and are not
            // recorded.
            span.SetAttributes(attributes);
            span.SetAttribute(
                "MySingleStringAttributeKey",
                AttributeValue.StringAttributeValue("MySingleStringAttributeValue"));
            span.AddAnnotation(Annotation.FromDescription(ANNOTATION_DESCRIPTION));
            span.AddAnnotation(ANNOTATION_DESCRIPTION, attributes);
            span.AddMessageEvent(
                MessageEvent.Builder(MessageEventType.Received, 1).SetUncompressedMessageSize(3).Build());
            span.AddLink(Link.FromSpanContext(spanContext, LinkType.ChildLinkedSpan));
            ISpanData spanData = ((Span)span).ToSpanData();
            Assert.Equal(timestamp, spanData.StartTimestamp);
            Assert.Empty(spanData.Attributes.AttributeMap);
            Assert.Empty(spanData.Annotations.Events);
            Assert.Empty(spanData.MessageEvents.Events);
            Assert.Empty(spanData.Links.Links);
            Assert.Equal(Status.Ok, spanData.Status);
            Assert.Equal(timestamp, spanData.EndTimestamp);
        }

        [Fact]
        public void ToSpanData_ActiveSpan()
        {
            ISpan span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
                    parentSpanId,
                    true,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
   
            span.SetAttribute(
                "MySingleStringAttributeKey",
                AttributeValue.StringAttributeValue("MySingleStringAttributeValue"));
            span.SetAttributes(attributes);
            interval = TimeSpan.FromMilliseconds(100);
            span.AddAnnotation(Annotation.FromDescription(ANNOTATION_DESCRIPTION));
            interval = TimeSpan.FromMilliseconds(200);
            span.AddAnnotation(ANNOTATION_DESCRIPTION, attributes);
            interval = TimeSpan.FromMilliseconds(300);
            IMessageEvent networkEvent =
                MessageEvent.Builder(MessageEventType.Received, 1).SetUncompressedMessageSize(3).Build();
            span.AddMessageEvent(networkEvent);
            interval = TimeSpan.FromMilliseconds(400);
            ILink link = Link.FromSpanContext(spanContext, LinkType.ChildLinkedSpan);
            span.AddLink(link);
            ISpanData spanData = ((Span)span).ToSpanData();
            Assert.Equal(spanContext, spanData.Context);
            Assert.Equal(SPAN_NAME, spanData.Name);
            Assert.Equal(parentSpanId, spanData.ParentSpanId);
            Assert.True(spanData.HasRemoteParent);
            Assert.Equal(0, spanData.Attributes.DroppedAttributesCount);
            Assert.Equal(expectedAttributes, spanData.Attributes.AttributeMap); 
            Assert.Equal(0, spanData.Annotations.DroppedEventsCount);
            Assert.Equal(2, spanData.Annotations.Events.Count());
            Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(100))), spanData.Annotations.Events.ToList()[0].Timestamp);
            Assert.Equal(Annotation.FromDescription(ANNOTATION_DESCRIPTION), spanData.Annotations.Events.ToList()[0].Event);
            Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(200))), spanData.Annotations.Events.ToList()[1].Timestamp);
            Assert.Equal(Annotation.FromDescriptionAndAttributes(ANNOTATION_DESCRIPTION, attributes), spanData.Annotations.Events.ToList()[1].Event);
            Assert.Equal(0, spanData.MessageEvents.DroppedEventsCount);
            Assert.Single(spanData.MessageEvents.Events);
            Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(300))), spanData.MessageEvents.Events.First().Timestamp);
            Assert.Equal(networkEvent, spanData.MessageEvents.Events.First().Event);
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
            ISpan span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
                    parentSpanId,
                    false,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
     
            span.SetAttribute(
                "MySingleStringAttributeKey",
                AttributeValue.StringAttributeValue("MySingleStringAttributeValue"));
            span.SetAttributes(attributes);
            interval = TimeSpan.FromMilliseconds(100);
            span.AddAnnotation(Annotation.FromDescription(ANNOTATION_DESCRIPTION));
            interval = TimeSpan.FromMilliseconds(200);
            span.AddAnnotation(ANNOTATION_DESCRIPTION, attributes);
            interval = TimeSpan.FromMilliseconds(300);
            IMessageEvent networkEvent =
                MessageEvent.Builder(MessageEventType.Received, 1).SetUncompressedMessageSize(3).Build();
            span.AddMessageEvent(networkEvent);
            ILink link = Link.FromSpanContext(spanContext, LinkType.ChildLinkedSpan);
            span.AddLink(link);
            interval = TimeSpan.FromMilliseconds(400);
            span.End(EndSpanOptions.Builder().SetStatus(Status.Cancelled).Build());
          
            ISpanData spanData = ((Span)span).ToSpanData();
            Assert.Equal(spanContext, spanData.Context);
            Assert.Equal(SPAN_NAME, spanData.Name);
            Assert.Equal(parentSpanId, spanData.ParentSpanId);
            Assert.False(spanData.HasRemoteParent);
            Assert.Equal(0, spanData.Attributes.DroppedAttributesCount);
            Assert.Equal(expectedAttributes, spanData.Attributes.AttributeMap);
            Assert.Equal(0, spanData.Annotations.DroppedEventsCount);
            Assert.Equal(2, spanData.Annotations.Events.Count());
            Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(100))), spanData.Annotations.Events.ToList()[0].Timestamp);
            Assert.Equal(Annotation.FromDescription(ANNOTATION_DESCRIPTION), spanData.Annotations.Events.ToList()[0].Event);
            Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(200))), spanData.Annotations.Events.ToList()[1].Timestamp);
            Assert.Equal(Annotation.FromDescriptionAndAttributes(ANNOTATION_DESCRIPTION, attributes), spanData.Annotations.Events.ToList()[1].Event);
            Assert.Equal(0, spanData.MessageEvents.DroppedEventsCount);
            Assert.Single(spanData.MessageEvents.Events);
            Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(300))), spanData.MessageEvents.Events.First().Timestamp);
            Assert.Equal(networkEvent, spanData.MessageEvents.Events.First().Event);
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
            ISpan span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
                    parentSpanId,
                    false,
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
            ISpan span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
                    parentSpanId,
                    false,
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
            int maxNumberOfAttributes = 8;
            TraceParams traceParams =
                TraceParams.Default.ToBuilder().SetMaxNumberOfAttributes(maxNumberOfAttributes).Build();
            ISpan span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
                    parentSpanId,
                    false,
                    traceParams,
                    startEndHandler,
                    timestampConverter);
            for (int i = 0; i < 2 * maxNumberOfAttributes; i++)
            {
                IDictionary<String, IAttributeValue> attributes = new Dictionary<String, IAttributeValue>();
                attributes.Add("MyStringAttributeKey" + i, AttributeValue.LongAttributeValue(i));
                span.SetAttributes(attributes);
            }
            ISpanData spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfAttributes, spanData.Attributes.DroppedAttributesCount);
            Assert.Equal(maxNumberOfAttributes, spanData.Attributes.AttributeMap.Count);
            for (int i = 0; i < maxNumberOfAttributes; i++)
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
            for (int i = 0; i < maxNumberOfAttributes; i++)
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
            int maxNumberOfAttributes = 8;
            TraceParams traceParams =
                TraceParams.Default.ToBuilder().SetMaxNumberOfAttributes(maxNumberOfAttributes).Build();
            ISpan span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
                    parentSpanId,
                    false,
                    traceParams,
                    startEndHandler,
                    timestampConverter);
            for (int i = 0; i < 2 * maxNumberOfAttributes; i++)
            {
                IDictionary<String, IAttributeValue> attributes = new Dictionary<String, IAttributeValue>();
                attributes.Add("MyStringAttributeKey" + i, AttributeValue.LongAttributeValue(i));
                span.SetAttributes(attributes);
            }
            ISpanData spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfAttributes, spanData.Attributes.DroppedAttributesCount);
            Assert.Equal(maxNumberOfAttributes, spanData.Attributes.AttributeMap.Count);
            for (int i = 0; i < maxNumberOfAttributes; i++)
            {
                Assert.Equal(
                    AttributeValue.LongAttributeValue(i + maxNumberOfAttributes),
                    spanData
                            .Attributes
                            .AttributeMap["MyStringAttributeKey" + (i + maxNumberOfAttributes)]);
            }
            for (int i = 0; i < maxNumberOfAttributes / 2; i++)
            {
                IDictionary<String, IAttributeValue> attributes = new Dictionary<String, IAttributeValue>();
                attributes.Add("MyStringAttributeKey" + i, AttributeValue.LongAttributeValue(i));
                span.SetAttributes(attributes);
            }
            spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfAttributes * 3 / 2, spanData.Attributes.DroppedAttributesCount);
            Assert.Equal(maxNumberOfAttributes, spanData.Attributes.AttributeMap.Count);
            // Test that we still have in the attributes map the latest maxNumberOfAttributes / 2 entries.
            for (int i = 0; i < maxNumberOfAttributes / 2; i++)
            {
                Assert.Equal(
                    AttributeValue.LongAttributeValue(i + maxNumberOfAttributes * 3 / 2),
                    spanData
                            .Attributes
                            .AttributeMap["MyStringAttributeKey" + (i + maxNumberOfAttributes * 3 / 2)]);
            }
            // Test that we have the newest re-added initial entries.
            for (int i = 0; i < maxNumberOfAttributes / 2; i++)
            {
                Assert.Equal(AttributeValue.LongAttributeValue(i), spanData.Attributes.AttributeMap["MyStringAttributeKey" + i]);
            }
        }

        [Fact]
        public void DroppingAnnotations()
        {
            int maxNumberOfAnnotations = 8;
            TraceParams traceParams =
                TraceParams.Default.ToBuilder().SetMaxNumberOfAnnotations(maxNumberOfAnnotations).Build();
            ISpan span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
                    parentSpanId,
                    false,
                    traceParams,
                    startEndHandler,
                    timestampConverter);
            IAnnotation annotation = Annotation.FromDescription(ANNOTATION_DESCRIPTION);
            int i = 0;
            for (i = 0; i < 2 * maxNumberOfAnnotations; i++)
            {
                span.AddAnnotation(annotation);
                interval += TimeSpan.FromMilliseconds(100);
            }
            ISpanData spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfAnnotations, spanData.Annotations.DroppedEventsCount);
            Assert.Equal(maxNumberOfAnnotations, spanData.Annotations.Events.Count());
            i = 0;
            foreach (var te in spanData.Annotations.Events)
            {
                Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(100 * (maxNumberOfAnnotations + i)))), te.Timestamp);
                Assert.Equal(annotation, te.Event);
                i++;
            }
            span.End();
            spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfAnnotations, spanData.Annotations.DroppedEventsCount);
            Assert.Equal(maxNumberOfAnnotations, spanData.Annotations.Events.Count());
            i = 0;
            foreach (var te in spanData.Annotations.Events)
            {
                Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(100 * (maxNumberOfAnnotations + i)))), te.Timestamp);
                Assert.Equal(annotation, te.Event);
                i++;
            }
        }

        [Fact]
        public void DroppingNetworkEvents()
        {
            int maxNumberOfNetworkEvents = 8;
            TraceParams traceParams =
                TraceParams.Default
                    .ToBuilder()
                    .SetMaxNumberOfMessageEvents(maxNumberOfNetworkEvents)
                    .Build();
            ISpan span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
                    parentSpanId,
                    false,
                    traceParams,
                    startEndHandler,
                    timestampConverter);
            IMessageEvent networkEvent =
                MessageEvent.Builder(MessageEventType.Received, 1).SetUncompressedMessageSize(3).Build();
            for (int i = 0; i < 2 * maxNumberOfNetworkEvents; i++)
            {
                span.AddMessageEvent(networkEvent);
                interval += TimeSpan.FromMilliseconds(100);
            }
            ISpanData spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfNetworkEvents, spanData.MessageEvents.DroppedEventsCount);
            Assert.Equal(maxNumberOfNetworkEvents, spanData.MessageEvents.Events.Count());
            var list = spanData.MessageEvents.Events.ToList();
            for (int i = 0; i < maxNumberOfNetworkEvents; i++)
            {
                Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(100 * (maxNumberOfNetworkEvents + i)))), list[i].Timestamp);
                Assert.Equal(networkEvent, list[i].Event);
            }
            span.End();
            spanData = ((Span)span).ToSpanData();
            Assert.Equal(maxNumberOfNetworkEvents, spanData.MessageEvents.DroppedEventsCount);
            Assert.Equal(maxNumberOfNetworkEvents, spanData.MessageEvents.Events.Count());
            list = spanData.MessageEvents.Events.ToList();
            for (int i = 0; i < maxNumberOfNetworkEvents; i++)
            {
                Assert.Equal(timestamp.AddDuration(Duration.Create(TimeSpan.FromMilliseconds(100 * (maxNumberOfNetworkEvents + i)))), list[i].Timestamp);
                Assert.Equal(networkEvent, list[i].Event);
            }
        }

        [Fact]
        public void DroppingLinks()
        {
            int maxNumberOfLinks = 8;
            TraceParams traceParams =
                TraceParams.Default.ToBuilder().SetMaxNumberOfLinks(maxNumberOfLinks).Build();
            ISpan span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
                    parentSpanId,
                    false,
                    traceParams,
                    startEndHandler,
                    timestampConverter);
            ILink link = Link.FromSpanContext(spanContext, LinkType.ChildLinkedSpan);
            for (int i = 0; i < 2 * maxNumberOfLinks; i++)
            {
                span.AddLink(link);
            }
            ISpanData spanData = ((Span)span).ToSpanData();
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
            ISpan span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
                    parentSpanId,
                    false,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);
            span.End(EndSpanOptions.Builder().SetSampleToLocalSpanStore(true).Build());

            Assert.True(((Span)span).IsSampleToLocalSpanStore);
            ISpan span2 =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
                    parentSpanId,
                    false,
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
            ISpan span =
                Span.StartSpan(
                    spanContext,
                    recordSpanOptions,
                    SPAN_NAME,
                    parentSpanId,
                    false,
                    TraceParams.Default,
                    startEndHandler,
                    timestampConverter);

            Assert.Throws<InvalidOperationException>(() => ((Span)span).IsSampleToLocalSpanStore);
        }
    }
}
