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
    using OpenTelemetry.Exporter.Jaeger.Implementation;
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
        public void JaegerSpanConverterTest_ConvertSpanToJaegerSpan_AllPropertiesSet()
        {
            var startTimestamp = DateTime.Now;
            var endTimestamp = startTimestamp.AddSeconds(60);
            var eventTimestamp = DateTime.Now;

            var traceId = ActivityTraceId.CreateRandom();
            var traceIdAsInt = new Int128(traceId);
            var spanId = ActivitySpanId.CreateRandom();
            var spanIdAsInt = new Int128(spanId);
            var parentSpanId = ActivitySpanId.CreateRandom();
            var attributes = Attributes.Create(new Dictionary<string, object>{
                { "stringKey", "value"},
                { "longKey", 1L},
                { "longKey2", 1 },
                { "doubleKey", 1D},
                { "doubleKey2", 1F},
                { "boolKey", true},
            }, 0);
            var events = TimedEvents<IEvent>.Create(new List<IEvent>
            {
                Event.Create(
                    "Event1",
                    eventTimestamp,
                    new Dictionary<string, object>
                    {
                        { "key", "value" },
                    }
                ),
                Event.Create(
                    "Event2",
                    eventTimestamp,
                    new Dictionary<string, object>
                    {
                        { "key", "value" },
                    }
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

            var links = LinkList.Create(new List<ILink> { link }, 0);

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
            Assert.Equal(2, jaegerSpan.Logs.Count());

            Assert.Equal(traceIdAsInt.High, jaegerSpan.TraceIdHigh);
            Assert.Equal(traceIdAsInt.Low, jaegerSpan.TraceIdLow);
            Assert.Equal(spanIdAsInt.Low, jaegerSpan.SpanId);
            Assert.Equal(new Int128(parentSpanId).Low, jaegerSpan.ParentSpanId);

            Assert.Equal(links.Links.Count(), jaegerSpan.References.Count());
            var references = jaegerSpan.References.ToArray();
            var jaegerRef = references[0];
            Assert.Equal(linkTraceIdAsInt.High, jaegerRef.TraceIdHigh);
            Assert.Equal(linkTraceIdAsInt.Low, jaegerRef.TraceIdLow);
            Assert.Equal(linkSpanIdAsInt.Low, jaegerRef.SpanId);

            Assert.Equal(0x1, jaegerSpan.Flags);

            Assert.Equal(startTimestamp.ToEpochMicroseconds(), jaegerSpan.StartTime);
            Assert.Equal(endTimestamp.ToEpochMicroseconds() - startTimestamp.ToEpochMicroseconds(), jaegerSpan.Duration);

            var tags = jaegerSpan.JaegerTags.ToArray();
            var tag = tags[0];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("stringKey", tag.Key);
            Assert.Equal("value", tag.VStr);
            tag = tags[1];
            Assert.Equal(JaegerTagType.LONG, tag.VType);
            Assert.Equal("longKey", tag.Key);
            Assert.Equal(1, tag.VLong);
            tag = tags[2];
            Assert.Equal(JaegerTagType.LONG, tag.VType);
            Assert.Equal("longKey2", tag.Key);
            Assert.Equal(1, tag.VLong);
            tag = tags[3];
            Assert.Equal(JaegerTagType.DOUBLE, tag.VType);
            Assert.Equal("doubleKey", tag.Key);
            Assert.Equal(1, tag.VDouble);
            tag = tags[4];
            Assert.Equal(JaegerTagType.DOUBLE, tag.VType);
            Assert.Equal("doubleKey2", tag.Key);
            Assert.Equal(1, tag.VDouble);
            tag = tags[5];
            Assert.Equal(JaegerTagType.BOOL, tag.VType);
            Assert.Equal("boolKey", tag.Key);
            Assert.Equal(true, tag.VBool);

            var logs = jaegerSpan.Logs.ToArray();
            var jaegerLog = logs[0];
            Assert.Equal(events.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
            Assert.Equal(jaegerLog.Fields.Count(), 2);
            var eventFields = jaegerLog.Fields.ToArray();
            var eventField = eventFields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = eventFields[1];
            Assert.Equal("description", eventField.Key);
            Assert.Equal("Event1", eventField.VStr);

            jaegerLog = logs[1];
            Assert.Equal(events.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
            Assert.Equal(jaegerLog.Fields.Count(), 2);
            eventFields = jaegerLog.Fields.ToArray();
            eventField = eventFields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = eventFields[1];
            Assert.Equal("description", eventField.Key);
            Assert.Equal("Event2", eventField.VStr);
        }

        [Fact]
        public void JaegerSpanConverterTest_ConvertSpanToJaegerSpan_NoAttributes()
        {
            var startTimestamp = DateTime.Now;
            var endTimestamp = startTimestamp.AddSeconds(60);
            var eventTimestamp = DateTime.Now;

            var traceId = ActivityTraceId.CreateRandom();
            var traceIdAsInt = new Int128(traceId);
            var spanId = ActivitySpanId.CreateRandom();
            var spanIdAsInt = new Int128(spanId);
            var parentSpanId = ActivitySpanId.CreateRandom();
            var events = TimedEvents<IEvent>.Create(new List<IEvent>
            {
                Event.Create(
                    "Event1",
                    eventTimestamp,
                    new Dictionary<string, object>
                    {
                        { "key", "value" },
                    }
                ),
                Event.Create(
                    "Event2",
                    eventTimestamp,
                    new Dictionary<string, object>
                    {
                        { "key", "value" },
                    }
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

            var links = LinkList.Create(new List<ILink> { link }, 0);

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
                null,
                events,
                links,
                null,
                Status.Ok,
                SpanKind.Client,
                endTimestamp
            );

            var jaegerSpan = spanData.ToJaegerSpan();

            Assert.Equal("Name", jaegerSpan.OperationName);
            Assert.Equal(2, jaegerSpan.Logs.Count());

            Assert.Equal(traceIdAsInt.High, jaegerSpan.TraceIdHigh);
            Assert.Equal(traceIdAsInt.Low, jaegerSpan.TraceIdLow);
            Assert.Equal(spanIdAsInt.Low, jaegerSpan.SpanId);
            Assert.Equal(new Int128(parentSpanId).Low, jaegerSpan.ParentSpanId);

            Assert.Equal(links.Links.Count(), jaegerSpan.References.Count());
            var references = jaegerSpan.References.ToArray();
            var jaegerRef = references[0];
            Assert.Equal(linkTraceIdAsInt.High, jaegerRef.TraceIdHigh);
            Assert.Equal(linkTraceIdAsInt.Low, jaegerRef.TraceIdLow);
            Assert.Equal(linkSpanIdAsInt.Low, jaegerRef.SpanId);

            Assert.Equal(0x1, jaegerSpan.Flags);

            Assert.Equal(startTimestamp.ToEpochMicroseconds(), jaegerSpan.StartTime);
            Assert.Equal(endTimestamp.ToEpochMicroseconds() - startTimestamp.ToEpochMicroseconds(), jaegerSpan.Duration);

            Assert.Empty(jaegerSpan.JaegerTags);

            var logs = jaegerSpan.Logs.ToArray();
            var jaegerLog = logs[0];
            Assert.Equal(events.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
            Assert.Equal(jaegerLog.Fields.Count(), 2);
            var eventFields = jaegerLog.Fields.ToArray();
            var eventField = eventFields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = eventFields[1];
            Assert.Equal("description", eventField.Key);
            Assert.Equal("Event1", eventField.VStr);

            jaegerLog = logs[1];
            Assert.Equal(events.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
            Assert.Equal(jaegerLog.Fields.Count(), 2);
            eventFields = jaegerLog.Fields.ToArray();
            eventField = eventFields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = eventFields[1];
            Assert.Equal("description", eventField.Key);
            Assert.Equal("Event2", eventField.VStr);
        }

        [Fact]
        public void JaegerSpanConverterTest_ConvertSpanToJaegerSpan_NoEvents()
        {
            var startTimestamp = DateTime.Now;
            var endTimestamp = startTimestamp.AddSeconds(60);
            var eventTimestamp = DateTime.Now;

            var traceId = ActivityTraceId.CreateRandom();
            var traceIdAsInt = new Int128(traceId);
            var spanId = ActivitySpanId.CreateRandom();
            var spanIdAsInt = new Int128(spanId);
            var parentSpanId = ActivitySpanId.CreateRandom();
            var attributes = Attributes.Create(new Dictionary<string, object>{
                { "stringKey", "value"},
                { "longKey", 1L},
                { "longKey2", 1 },
                { "doubleKey", 1D},
                { "doubleKey2", 1F},
                { "boolKey", true},
            }, 0);

            var linkedSpanId = ActivitySpanId.CreateRandom();

            var link = Link.FromSpanContext(SpanContext.Create(
                    traceId,
                    linkedSpanId,
                    ActivityTraceFlags.Recorded,
                    Tracestate.Empty));

            var linkTraceIdAsInt = new Int128(link.Context.TraceId);
            var linkSpanIdAsInt = new Int128(link.Context.SpanId);

            var links = LinkList.Create(new List<ILink> { link }, 0);

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
                null,
                links,
                null,
                Status.Ok,
                SpanKind.Client,
                endTimestamp
            );

            var jaegerSpan = spanData.ToJaegerSpan();

            Assert.Equal("Name", jaegerSpan.OperationName);
            Assert.Empty(jaegerSpan.Logs);

            Assert.Equal(traceIdAsInt.High, jaegerSpan.TraceIdHigh);
            Assert.Equal(traceIdAsInt.Low, jaegerSpan.TraceIdLow);
            Assert.Equal(spanIdAsInt.Low, jaegerSpan.SpanId);
            Assert.Equal(new Int128(parentSpanId).Low, jaegerSpan.ParentSpanId);

            Assert.Equal(links.Links.Count(), jaegerSpan.References.Count());
            var references = jaegerSpan.References.ToArray();
            var jaegerRef = references[0];
            Assert.Equal(linkTraceIdAsInt.High, jaegerRef.TraceIdHigh);
            Assert.Equal(linkTraceIdAsInt.Low, jaegerRef.TraceIdLow);
            Assert.Equal(linkSpanIdAsInt.Low, jaegerRef.SpanId);

            Assert.Equal(0x1, jaegerSpan.Flags);

            Assert.Equal(startTimestamp.ToEpochMicroseconds(), jaegerSpan.StartTime);
            Assert.Equal(endTimestamp.ToEpochMicroseconds() - startTimestamp.ToEpochMicroseconds(), jaegerSpan.Duration);

            var tags = jaegerSpan.JaegerTags.ToArray();
            var tag = tags[0];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("stringKey", tag.Key);
            Assert.Equal("value", tag.VStr);
            tag = tags[1];
            Assert.Equal(JaegerTagType.LONG, tag.VType);
            Assert.Equal("longKey", tag.Key);
            Assert.Equal(1, tag.VLong);
            tag = tags[2];
            Assert.Equal(JaegerTagType.LONG, tag.VType);
            Assert.Equal("longKey2", tag.Key);
            Assert.Equal(1, tag.VLong);
            tag = tags[3];
            Assert.Equal(JaegerTagType.DOUBLE, tag.VType);
            Assert.Equal("doubleKey", tag.Key);
            Assert.Equal(1, tag.VDouble);
            tag = tags[4];
            Assert.Equal(JaegerTagType.DOUBLE, tag.VType);
            Assert.Equal("doubleKey2", tag.Key);
            Assert.Equal(1, tag.VDouble);
            tag = tags[5];
            Assert.Equal(JaegerTagType.BOOL, tag.VType);
            Assert.Equal("boolKey", tag.Key);
            Assert.Equal(true, tag.VBool);
        }

        [Fact]
        public void JaegerSpanConverterTest_ConvertSpanToJaegerSpan_NoLinks()
        {
            var startTimestamp = DateTime.Now;
            var endTimestamp = startTimestamp.AddSeconds(60);
            var eventTimestamp = DateTime.Now;

            var traceId = ActivityTraceId.CreateRandom();
            var traceIdAsInt = new Int128(traceId);
            var spanId = ActivitySpanId.CreateRandom();
            var spanIdAsInt = new Int128(spanId);
            var parentSpanId = ActivitySpanId.CreateRandom();
            var attributes = Attributes.Create(new Dictionary<string, object>{
                { "stringKey", "value"},
                { "longKey", 1L},
                { "longKey2", 1 },
                { "doubleKey", 1D},
                { "doubleKey2", 1F},
                { "boolKey", true},
            }, 0);
            var events = TimedEvents<IEvent>.Create(new List<IEvent>
            {
                Event.Create(
                    "Event1",
                    eventTimestamp,
                    new Dictionary<string, object>
                    {
                        { "key", "value" },
                    }
                ),
                Event.Create(
                    "Event2",
                    eventTimestamp,
                    new Dictionary<string, object>
                    {
                        { "key", "value" },
                    }
                ),
            }, 0);

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
                null,
                null,
                Status.Ok,
                SpanKind.Client,
                endTimestamp
            );

            var jaegerSpan = spanData.ToJaegerSpan();

            Assert.Equal("Name", jaegerSpan.OperationName);
            Assert.Equal(2, jaegerSpan.Logs.Count());

            Assert.Equal(traceIdAsInt.High, jaegerSpan.TraceIdHigh);
            Assert.Equal(traceIdAsInt.Low, jaegerSpan.TraceIdLow);
            Assert.Equal(spanIdAsInt.Low, jaegerSpan.SpanId);
            Assert.Equal(new Int128(parentSpanId).Low, jaegerSpan.ParentSpanId);

            Assert.Empty(jaegerSpan.References);

            Assert.Equal(0x1, jaegerSpan.Flags);

            Assert.Equal(startTimestamp.ToEpochMicroseconds(), jaegerSpan.StartTime);
            Assert.Equal(endTimestamp.ToEpochMicroseconds() - startTimestamp.ToEpochMicroseconds(), jaegerSpan.Duration);

            var tags = jaegerSpan.JaegerTags.ToArray();
            var tag = tags[0];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("stringKey", tag.Key);
            Assert.Equal("value", tag.VStr);
            tag = tags[1];
            Assert.Equal(JaegerTagType.LONG, tag.VType);
            Assert.Equal("longKey", tag.Key);
            Assert.Equal(1, tag.VLong);
            tag = tags[2];
            Assert.Equal(JaegerTagType.LONG, tag.VType);
            Assert.Equal("longKey2", tag.Key);
            Assert.Equal(1, tag.VLong);
            tag = tags[3];
            Assert.Equal(JaegerTagType.DOUBLE, tag.VType);
            Assert.Equal("doubleKey", tag.Key);
            Assert.Equal(1, tag.VDouble);
            tag = tags[4];
            Assert.Equal(JaegerTagType.DOUBLE, tag.VType);
            Assert.Equal("doubleKey2", tag.Key);
            Assert.Equal(1, tag.VDouble);
            tag = tags[5];
            Assert.Equal(JaegerTagType.BOOL, tag.VType);
            Assert.Equal("boolKey", tag.Key);
            Assert.Equal(true, tag.VBool);

            var logs = jaegerSpan.Logs.ToArray();
            var jaegerLog = logs[0];
            Assert.Equal(events.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
            Assert.Equal(2, jaegerLog.Fields.Count());
            var eventFields = jaegerLog.Fields.ToArray();
            var eventField = eventFields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = eventFields[1];
            Assert.Equal("description", eventField.Key);
            Assert.Equal("Event1", eventField.VStr);

            jaegerLog = logs[1];
            Assert.Equal(events.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
            Assert.Equal(jaegerLog.Fields.Count(), 2);
            eventFields = jaegerLog.Fields.ToArray();
            eventField = eventFields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = eventFields[1];
            Assert.Equal("description", eventField.Key);
            Assert.Equal("Event2", eventField.VStr);
        }
    }
}
