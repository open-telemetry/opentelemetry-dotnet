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


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Exporter.Jaeger.Implementation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Exporter.Jaeger.Tests.Implementation
{
    public class JaegerSpanConverterTest
    {
        private const long MillisPerSecond = 1000L;
        private const long NanosPerMillisecond = 1000 * 1000;
        private const long NanosPerSecond = NanosPerMillisecond * MillisPerSecond;
        private readonly Tracer tracer;

        public JaegerSpanConverterTest()
        {
            tracer = TracerFactory.Create(b => { }).GetTracer(null);
        }

        [Fact]
        public void JaegerSpanConverterTest_ConvertSpanToJaegerSpan_AllPropertiesSet()
        {
            var span = CreateTestSpan();
            var traceIdAsInt = new Int128(span.Context.TraceId);
            var spanIdAsInt = new Int128(span.Context.SpanId);
            var linkTraceIdAsInt = new Int128(span.Links.Single().Context.TraceId);
            var linkSpanIdAsInt = new Int128(span.Links.Single().Context.SpanId);

            var jaegerSpan = span.ToJaegerSpan();

            Assert.Equal("Name", jaegerSpan.OperationName);
            Assert.Equal(2, jaegerSpan.Logs.Count());

            Assert.Equal(traceIdAsInt.High, jaegerSpan.TraceIdHigh);
            Assert.Equal(traceIdAsInt.Low, jaegerSpan.TraceIdLow);
            Assert.Equal(spanIdAsInt.Low, jaegerSpan.SpanId);
            Assert.Equal(new Int128(span.ParentSpanId).Low, jaegerSpan.ParentSpanId);

            Assert.Equal(span.Links.Count(), jaegerSpan.References.Count());
            var references = jaegerSpan.References.ToArray();
            var jaegerRef = references[0];
            Assert.Equal(linkTraceIdAsInt.High, jaegerRef.TraceIdHigh);
            Assert.Equal(linkTraceIdAsInt.Low, jaegerRef.TraceIdLow);
            Assert.Equal(linkSpanIdAsInt.Low, jaegerRef.SpanId);

            Assert.Equal(0x1, jaegerSpan.Flags);

            Assert.Equal(span.StartTimestamp.ToEpochMicroseconds(), jaegerSpan.StartTime);
            Assert.Equal((long)((span.EndTimestamp - span.StartTimestamp).TotalMilliseconds * 1000), jaegerSpan.Duration);

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
            Assert.Equal(span.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
            Assert.Equal(2, jaegerLog.Fields.Count());
            var eventFields = jaegerLog.Fields.ToArray();
            var eventField = eventFields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = eventFields[1];
            Assert.Equal("message", eventField.Key);
            Assert.Equal("Event1", eventField.VStr);

            Assert.Equal(span.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);

            jaegerLog = logs[1];
            Assert.Equal(2, jaegerLog.Fields.Count());
            eventFields = jaegerLog.Fields.ToArray();
            eventField = eventFields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = eventFields[1];
            Assert.Equal("message", eventField.Key);
            Assert.Equal("Event2", eventField.VStr);
        }

        [Fact]
        public void JaegerSpanConverterTest_ConvertSpanToJaegerSpan_NoAttributes()
        {
            var span = CreateTestSpan(setAttributes: false);
            var traceIdAsInt = new Int128(span.Context.TraceId);
            var spanIdAsInt = new Int128(span.Context.SpanId);
            var linkTraceIdAsInt = new Int128(span.Links.Single().Context.TraceId);
            var linkSpanIdAsInt = new Int128(span.Links.Single().Context.SpanId);

            var jaegerSpan = span.ToJaegerSpan();

            Assert.Equal("Name", jaegerSpan.OperationName);
            Assert.Equal(2, jaegerSpan.Logs.Count());

            Assert.Equal(traceIdAsInt.High, jaegerSpan.TraceIdHigh);
            Assert.Equal(traceIdAsInt.Low, jaegerSpan.TraceIdLow);
            Assert.Equal(spanIdAsInt.Low, jaegerSpan.SpanId);
            Assert.Equal(new Int128(span.ParentSpanId).Low, jaegerSpan.ParentSpanId);

            Assert.Equal(span.Links.Count(), jaegerSpan.References.Count());
            var references = jaegerSpan.References.ToArray();
            var jaegerRef = references[0];
            Assert.Equal(linkTraceIdAsInt.High, jaegerRef.TraceIdHigh);
            Assert.Equal(linkTraceIdAsInt.Low, jaegerRef.TraceIdLow);
            Assert.Equal(linkSpanIdAsInt.Low, jaegerRef.SpanId);

            Assert.Equal(0x1, jaegerSpan.Flags);

            Assert.Equal(span.StartTimestamp.ToEpochMicroseconds(), jaegerSpan.StartTime);
            Assert.Equal((long)((span.EndTimestamp - span.StartTimestamp).TotalMilliseconds * 1000), jaegerSpan.Duration);

            // 2 tags: span.kind & ot.status_code.
            Assert.Equal(2, jaegerSpan.JaegerTags.Count());

            var logs = jaegerSpan.Logs.ToArray();
            var jaegerLog = logs[0];
            Assert.Equal(span.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
            Assert.Equal(2, jaegerLog.Fields.Count());
            var eventFields = jaegerLog.Fields.ToArray();
            var eventField = eventFields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = eventFields[1];
            Assert.Equal("message", eventField.Key);
            Assert.Equal("Event1", eventField.VStr);

            Assert.Equal(span.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);

            jaegerLog = logs[1];
            Assert.Equal(2, jaegerLog.Fields.Count());
            eventFields = jaegerLog.Fields.ToArray();
            eventField = eventFields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = eventFields[1];
            Assert.Equal("message", eventField.Key);
            Assert.Equal("Event2", eventField.VStr);
        }

        [Fact]
        public void JaegerSpanConverterTest_ConvertSpanToJaegerSpan_NoEvents()
        {
            var span = CreateTestSpan(addEvents: false);
            var traceIdAsInt = new Int128(span.Context.TraceId);
            var spanIdAsInt = new Int128(span.Context.SpanId);
            var linkTraceIdAsInt = new Int128(span.Links.Single().Context.TraceId);
            var linkSpanIdAsInt = new Int128(span.Links.Single().Context.SpanId);

            var jaegerSpan = span.ToJaegerSpan();

            Assert.Equal("Name", jaegerSpan.OperationName);
            Assert.Empty(jaegerSpan.Logs);

            Assert.Equal(traceIdAsInt.High, jaegerSpan.TraceIdHigh);
            Assert.Equal(traceIdAsInt.Low, jaegerSpan.TraceIdLow);
            Assert.Equal(spanIdAsInt.Low, jaegerSpan.SpanId);
            Assert.Equal(new Int128(span.ParentSpanId).Low, jaegerSpan.ParentSpanId);

            Assert.Equal(span.Links.Count(), jaegerSpan.References.Count());
            var references = jaegerSpan.References.ToArray();
            var jaegerRef = references[0];
            Assert.Equal(linkTraceIdAsInt.High, jaegerRef.TraceIdHigh);
            Assert.Equal(linkTraceIdAsInt.Low, jaegerRef.TraceIdLow);
            Assert.Equal(linkSpanIdAsInt.Low, jaegerRef.SpanId);

            Assert.Equal(0x1, jaegerSpan.Flags);

            Assert.Equal(span.StartTimestamp.ToEpochMicroseconds(), jaegerSpan.StartTime);
            Assert.Equal(span.EndTimestamp.ToEpochMicroseconds()
                         - span.StartTimestamp.ToEpochMicroseconds(), jaegerSpan.Duration);

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
            var span = CreateTestSpan(addLinks: false);
            var traceIdAsInt = new Int128(span.Context.TraceId);
            var spanIdAsInt = new Int128(span.Context.SpanId);

            var jaegerSpan = span.ToJaegerSpan();

            Assert.Equal("Name", jaegerSpan.OperationName);
            Assert.Equal(2, jaegerSpan.Logs.Count());

            Assert.Equal(traceIdAsInt.High, jaegerSpan.TraceIdHigh);
            Assert.Equal(traceIdAsInt.Low, jaegerSpan.TraceIdLow);
            Assert.Equal(spanIdAsInt.Low, jaegerSpan.SpanId);
            Assert.Equal(new Int128(span.ParentSpanId).Low, jaegerSpan.ParentSpanId);

            Assert.Empty(jaegerSpan.References);

            Assert.Equal(0x1, jaegerSpan.Flags);

            Assert.Equal(span.StartTimestamp.ToEpochMicroseconds(), jaegerSpan.StartTime);
            Assert.Equal(span.EndTimestamp.ToEpochMicroseconds()
                         - span.StartTimestamp.ToEpochMicroseconds(), jaegerSpan.Duration);

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

            // The second to last tag should be span.kind in this case
            tag = tags[tags.Length - 2];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("span.kind", tag.Key);
            Assert.Equal("client", tag.VStr);

            // The last tag should be span.kind in this case
            tag = tags[tags.Length - 1];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("ot.status_code", tag.Key);
            Assert.Equal("Ok", tag.VStr);

            var logs = jaegerSpan.Logs.ToArray();
            var jaegerLog = logs[0];
            Assert.Equal(span.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
            Assert.Equal(2, jaegerLog.Fields.Count());
            var eventFields = jaegerLog.Fields.ToArray();
            var eventField = eventFields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = eventFields[1];
            Assert.Equal("message", eventField.Key);
            Assert.Equal("Event1", eventField.VStr);
            Assert.Equal(span.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);

            jaegerLog = logs[1];
            Assert.Equal(2, jaegerLog.Fields.Count());
            eventFields = jaegerLog.Fields.ToArray();
            eventField = eventFields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = eventFields[1];
            Assert.Equal("message", eventField.Key);
            Assert.Equal("Event2", eventField.VStr);
        }

        [Fact]
        public void JaegerSpanConverterTest_ConvertSpanToJaegerSpan_LibraryResources()
        {
            var span = CreateTestSpan(resource: new Resource(new Dictionary<string, object>
            {
                [Resource.LibraryNameKey] = "libname",
                [Resource.LibraryVersionKey] = "libversion",
                [Resource.ServiceNameKey] = "MyService",
            }));

            var jaegerSpan = span.ToJaegerSpan();

            Assert.Contains(jaegerSpan.JaegerTags, t => t.Key == Resource.LibraryNameKey && t.VStr == "libname");
            Assert.Contains(jaegerSpan.JaegerTags, t => t.Key == Resource.LibraryVersionKey && t.VStr == "libversion");
            Assert.DoesNotContain(jaegerSpan.JaegerTags, t => t.Key == Resource.ServiceNameKey && t.VStr == "MyService");
        }

        internal SpanData CreateTestSpan(
            bool setAttributes = true,
            bool addEvents = true,
            bool addLinks = true,
            Resource resource = null)
        {
            var startTimestamp = DateTime.UtcNow;
            var endTimestamp = startTimestamp.AddSeconds(60);
            var eventTimestamp = DateTime.UtcNow;
            var traceId = ActivityTraceId.CreateFromString("e8ea7e9ac72de94e91fabc613f9686b2".AsSpan());

            var spanId = ActivitySpanId.CreateRandom();
            var parentSpanId = ActivitySpanId.CreateFromBytes(new byte[] { 12, 23, 34, 45, 56, 67, 78, 89 });
            var attributes = new Dictionary<string, object>
            {
                { "stringKey", "value"},
                { "longKey", 1L},
                { "longKey2", 1 },
                { "doubleKey", 1D},
                { "doubleKey2", 1F},
                { "boolKey", true},
            };
            var events = new List<Event>
            {
                new Event(
                    "Event1",
                    eventTimestamp,
                    new Dictionary<string, object>
                    {
                        { "key", "value" },
                    }
                ),
                new Event(
                    "Event2",
                    eventTimestamp,
                    new Dictionary<string, object>
                    {
                        { "key", "value" },
                    }
                ),
            };

            var linkedSpanId = ActivitySpanId.CreateFromString("888915b6286b9c41".AsSpan());

            return new SpanData(
                "Name",
                new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded),
                parentSpanId,
                SpanKind.Client,
                startTimestamp,
                setAttributes ? attributes : null,
                addEvents ? events : null,
                addLinks ? new[] { new Link(new SpanContext(
                        traceId,
                        linkedSpanId,
                        ActivityTraceFlags.Recorded)), } : null,
                resource,
                Status.Ok,
                endTimestamp);
        }
    }
}
