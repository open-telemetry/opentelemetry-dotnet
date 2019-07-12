// <copyright file="JaegerSpanConverterTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Jaeger.Tests.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using OpenTelemetry.Common;
    using OpenTelemetry.Exporter.Jaeger.Implimentation;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;
    using Xunit;

    public class JaegerSpanConverterTest
    {
        private const long MillisPerSecond = 1000L;
        private const long NanosPerMillisecond = 1000 * 1000;
        private const long NanosPerSecond = NanosPerMillisecond * MillisPerSecond;

        public JaegerSpanConverterTest()
        {
        }

        [Fact]
        public void TestConvertSpan()
        {
            var startTimestamp = Timestamp.Create(100, 100);
            var endTimestamp = Timestamp.Create(200, 100);
            var eventTimestamp = Timestamp.Create(100, 100);

            var traceId = ActivityTraceId.CreateRandom();
            var traceIdAsInt = new Int128(traceId);
            var spanId = ActivitySpanId.CreateRandom();
            var spanIdAsInt = new Int128(spanId);
            var parentSpanId = ActivitySpanId.CreateRandom();
            var attributes = Attributes.Create(new Dictionary<string, IAttributeValue>{
                { "stringKey", AttributeValue.StringAttributeValue("value")},
                { "longKey", AttributeValue.LongAttributeValue(1)},
                { "doubleKey", AttributeValue.DoubleAttributeValue(1)},
                { "boolKey", AttributeValue.BooleanAttributeValue(true)},
            }, 0);
            var events = TimedEvents<IEvent>.Create(new List<ITimedEvent<IEvent>>
            {
                TimedEvent<IEvent>.Create(
                    eventTimestamp,
                    Event.Create(
                        "Event1",
                        new Dictionary<string, IAttributeValue>
                        {
                            {"key", AttributeValue.StringAttributeValue("value") }
                        }
                    )
                ),
                TimedEvent<IEvent>.Create(
                    eventTimestamp,
                    Event.Create(
                        "Event2",
                        new Dictionary<string, IAttributeValue>
                        {
                            {"key", AttributeValue.StringAttributeValue("value") },
                        }
                    )
                ),
            }, 0);

            var linkedSpanId = ActivitySpanId.CreateRandom();

            var link = Link.FromSpanContext(SpanContext.Create(
                    traceId,
                    linkedSpanId,
                    ActivityTraceFlags.Recorded,
                    Tracestate.Empty));

            var linkTraceIdAsInt = new Int128(link.Context.TraceId);
            var linkSpanIdAsInt = new Int128(link.Context.SpanId);

            var links = LinkList.Create(new List<ILink>{ link }, 0);

            var spanData = SpanData.Create(
                SpanContext.Create(
                    traceId,
                    spanId,
                    ActivityTraceFlags.Recorded,
                    Tracestate.Empty
                ),
                parentSpanId,
                Resource.Empty,
                "Name",
                startTimestamp,
                attributes,
                events,
                links,
                null,
                Status.Ok,
                SpanKind.Client,
                endTimestamp
            );

            var jaegerSpan = spanData.ToJaegerSpan();

            Assert.Equal("Name", jaegerSpan.OperationName);
            Assert.Equal(2, jaegerSpan.Logs.Count);

            Assert.Equal(traceIdAsInt.High, jaegerSpan.TraceIdHigh);
            Assert.Equal(traceIdAsInt.Low, jaegerSpan.TraceIdLow);
            Assert.Equal(spanIdAsInt.Low, jaegerSpan.SpanId);
            Assert.Equal(new Int128(parentSpanId).Low, jaegerSpan.ParentSpanId);

            Assert.Equal(links.Links.Count(), jaegerSpan.References.Count);
            var jaegerRef = jaegerSpan.References[0];
            Assert.Equal(linkTraceIdAsInt.High, jaegerRef.TraceIdHigh);
            Assert.Equal(linkTraceIdAsInt.Low, jaegerRef.TraceIdLow);
            Assert.Equal(linkSpanIdAsInt.Low, jaegerRef.SpanId);

            Assert.Equal(0x1, jaegerSpan.Flags);

            Assert.Equal(startTimestamp.ToEpochMicroseconds(), jaegerSpan.StartTime);
            Assert.Equal(endTimestamp.ToEpochMicroseconds() - startTimestamp.ToEpochMicroseconds(), jaegerSpan.Duration);

            var tag = jaegerSpan.JaegerTags[0];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("stringKey", tag.Key);
            Assert.Equal("value", tag.VStr);
            tag = jaegerSpan.JaegerTags[1];
            Assert.Equal(JaegerTagType.LONG, tag.VType);
            Assert.Equal("longKey", tag.Key);
            Assert.Equal(1, tag.VLong);
            tag = jaegerSpan.JaegerTags[2];
            Assert.Equal(JaegerTagType.DOUBLE, tag.VType);
            Assert.Equal("doubleKey", tag.Key);
            Assert.Equal(1, tag.VDouble);
            tag = jaegerSpan.JaegerTags[3];
            Assert.Equal(JaegerTagType.BOOL, tag.VType);
            Assert.Equal("boolKey", tag.Key);
            Assert.Equal(true, tag.VBool);
            
            var jaegerLog = jaegerSpan.Logs[0];
            Assert.Equal(events.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
            Assert.Equal(jaegerLog.Fields.Count, 2);
            var eventField = jaegerLog.Fields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = jaegerLog.Fields[1];
            Assert.Equal("description", eventField.Key);
            Assert.Equal("Event1", eventField.VStr);

            jaegerLog = jaegerSpan.Logs[1];
            Assert.Equal(events.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
            Assert.Equal(jaegerLog.Fields.Count, 2);
            eventField = jaegerLog.Fields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = jaegerLog.Fields[1];
            Assert.Equal("description", eventField.Key);
            Assert.Equal("Event2", eventField.VStr);
        }

    }
}
