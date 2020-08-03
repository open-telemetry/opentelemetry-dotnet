// <copyright file="JaegerActivityConversionTest.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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

using Xunit;

namespace OpenTelemetry.Exporter.Jaeger.Tests.Implementation
{
    public class JaegerActivityConversionTest
    {
        static JaegerActivityConversionTest()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                GetRequestedDataUsingParentId = (ref ActivityCreationOptions<string> options) => ActivityDataRequest.AllData,
                GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => ActivityDataRequest.AllData,
            };

            ActivitySource.AddActivityListener(listener);
        }

        [Fact]
        public void JaegerActivityConverterTest_ConvertActivityToJaegerSpan_AllPropertiesSet()
        {
            var activity = CreateTestActivity();
            var traceIdAsInt = new Int128(activity.Context.TraceId);
            var spanIdAsInt = new Int128(activity.Context.SpanId);
            var linkTraceIdAsInt = new Int128(activity.Links.Single().Context.TraceId);
            var linkSpanIdAsInt = new Int128(activity.Links.Single().Context.SpanId);

            var jaegerSpan = activity.ToJaegerSpan();

            Assert.Equal("Name", jaegerSpan.OperationName);
            Assert.Equal(2, jaegerSpan.Logs.Count);

            Assert.Equal(traceIdAsInt.High, jaegerSpan.TraceIdHigh);
            Assert.Equal(traceIdAsInt.Low, jaegerSpan.TraceIdLow);
            Assert.Equal(spanIdAsInt.Low, jaegerSpan.SpanId);
            Assert.Equal(new Int128(activity.ParentSpanId).Low, jaegerSpan.ParentSpanId);

            Assert.Equal(activity.Links.Count(), jaegerSpan.References.Count);
            var references = jaegerSpan.References.ToArray();
            var jaegerRef = references[0];
            Assert.Equal(linkTraceIdAsInt.High, jaegerRef.TraceIdHigh);
            Assert.Equal(linkTraceIdAsInt.Low, jaegerRef.TraceIdLow);
            Assert.Equal(linkSpanIdAsInt.Low, jaegerRef.SpanId);

            Assert.Equal(0x1, jaegerSpan.Flags);

            Assert.Equal(activity.StartTimeUtc.ToEpochMicroseconds(), jaegerSpan.StartTime);
            Assert.Equal((long)(activity.Duration.TotalMilliseconds * 1000), jaegerSpan.Duration);

            var tags = jaegerSpan.Tags.ToArray();
            var tag = tags[0];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("stringKey", tag.Key);
            Assert.Equal("value", tag.VStr);
            tag = tags[1];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("longKey", tag.Key);
            Assert.Equal("1", tag.VStr);
            tag = tags[2];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("longKey2", tag.Key);
            Assert.Equal("1", tag.VStr);
            tag = tags[3];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("doubleKey", tag.Key);
            Assert.Equal("1", tag.VStr);
            tag = tags[4];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("doubleKey2", tag.Key);
            Assert.Equal("1", tag.VStr);
            tag = tags[5];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("boolKey", tag.Key);
            Assert.Equal("True", tag.VStr);

            var logs = jaegerSpan.Logs.ToArray();
            var jaegerLog = logs[0];
            Assert.Equal(activity.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
            Assert.Equal(2, jaegerLog.Fields.Count());
            var eventFields = jaegerLog.Fields.ToArray();
            var eventField = eventFields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = eventFields[1];
            Assert.Equal("message", eventField.Key);
            Assert.Equal("Event1", eventField.VStr);

            Assert.Equal(activity.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);

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
        public void JaegerActivityConverterTest_ConvertActivityToJaegerSpan_NoAttributes()
        {
            var activity = CreateTestActivity(setAttributes: false);
            var traceIdAsInt = new Int128(activity.Context.TraceId);
            var spanIdAsInt = new Int128(activity.Context.SpanId);
            var linkTraceIdAsInt = new Int128(activity.Links.Single().Context.TraceId);
            var linkSpanIdAsInt = new Int128(activity.Links.Single().Context.SpanId);

            var jaegerSpan = activity.ToJaegerSpan();

            Assert.Equal("Name", jaegerSpan.OperationName);
            Assert.Equal(2, jaegerSpan.Logs.Count);

            Assert.Equal(traceIdAsInt.High, jaegerSpan.TraceIdHigh);
            Assert.Equal(traceIdAsInt.Low, jaegerSpan.TraceIdLow);
            Assert.Equal(spanIdAsInt.Low, jaegerSpan.SpanId);
            Assert.Equal(new Int128(activity.ParentSpanId).Low, jaegerSpan.ParentSpanId);

            Assert.Equal(activity.Links.Count(), jaegerSpan.References.Count);
            var references = jaegerSpan.References.ToArray();
            var jaegerRef = references[0];
            Assert.Equal(linkTraceIdAsInt.High, jaegerRef.TraceIdHigh);
            Assert.Equal(linkTraceIdAsInt.Low, jaegerRef.TraceIdLow);
            Assert.Equal(linkSpanIdAsInt.Low, jaegerRef.SpanId);

            Assert.Equal(0x1, jaegerSpan.Flags);

            Assert.Equal(activity.StartTimeUtc.ToEpochMicroseconds(), jaegerSpan.StartTime);
            Assert.Equal((long)(activity.Duration.TotalMilliseconds * 1000), jaegerSpan.Duration);

            // 2 tags: span.kind & library.name.
            Assert.Equal(2, jaegerSpan.Tags.Count);

            var logs = jaegerSpan.Logs.ToArray();
            var jaegerLog = logs[0];
            Assert.Equal(activity.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
            Assert.Equal(2, jaegerLog.Fields.Count());
            var eventFields = jaegerLog.Fields.ToArray();
            var eventField = eventFields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = eventFields[1];
            Assert.Equal("message", eventField.Key);
            Assert.Equal("Event1", eventField.VStr);

            Assert.Equal(activity.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);

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
        public void JaegerActivityConverterTest_ConvertActivityToJaegerSpan_NoEvents()
        {
            var activity = CreateTestActivity(addEvents: false);
            var traceIdAsInt = new Int128(activity.Context.TraceId);
            var spanIdAsInt = new Int128(activity.Context.SpanId);
            var linkTraceIdAsInt = new Int128(activity.Links.Single().Context.TraceId);
            var linkSpanIdAsInt = new Int128(activity.Links.Single().Context.SpanId);

            var jaegerSpan = activity.ToJaegerSpan();

            Assert.Equal("Name", jaegerSpan.OperationName);
            Assert.Empty(jaegerSpan.Logs);

            Assert.Equal(traceIdAsInt.High, jaegerSpan.TraceIdHigh);
            Assert.Equal(traceIdAsInt.Low, jaegerSpan.TraceIdLow);
            Assert.Equal(spanIdAsInt.Low, jaegerSpan.SpanId);
            Assert.Equal(new Int128(activity.ParentSpanId).Low, jaegerSpan.ParentSpanId);

            Assert.Equal(activity.Links.Count(), jaegerSpan.References.Count);
            var references = jaegerSpan.References.ToArray();
            var jaegerRef = references[0];
            Assert.Equal(linkTraceIdAsInt.High, jaegerRef.TraceIdHigh);
            Assert.Equal(linkTraceIdAsInt.Low, jaegerRef.TraceIdLow);
            Assert.Equal(linkSpanIdAsInt.Low, jaegerRef.SpanId);

            Assert.Equal(0x1, jaegerSpan.Flags);

            Assert.Equal(activity.StartTimeUtc.ToEpochMicroseconds(), jaegerSpan.StartTime);
            Assert.Equal(activity.Duration.TotalMilliseconds * 1000, jaegerSpan.Duration);

            var tags = jaegerSpan.Tags.ToArray();
            var tag = tags[0];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("stringKey", tag.Key);
            Assert.Equal("value", tag.VStr);
            tag = tags[1];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("longKey", tag.Key);
            Assert.Equal("1", tag.VStr);
            tag = tags[2];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("longKey2", tag.Key);
            Assert.Equal("1", tag.VStr);
            tag = tags[3];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("doubleKey", tag.Key);
            Assert.Equal("1", tag.VStr);
            tag = tags[4];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("doubleKey2", tag.Key);
            Assert.Equal("1", tag.VStr);
            tag = tags[5];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("boolKey", tag.Key);
            Assert.Equal("True", tag.VStr);
        }

        [Fact]
        public void JaegerActivityConverterTest_ConvertActivityToJaegerSpan_NoLinks()
        {
            var activity = CreateTestActivity(addLinks: false);
            var traceIdAsInt = new Int128(activity.Context.TraceId);
            var spanIdAsInt = new Int128(activity.Context.SpanId);

            var jaegerSpan = activity.ToJaegerSpan();

            Assert.Equal("Name", jaegerSpan.OperationName);
            Assert.Equal(2, jaegerSpan.Logs.Count);

            Assert.Equal(traceIdAsInt.High, jaegerSpan.TraceIdHigh);
            Assert.Equal(traceIdAsInt.Low, jaegerSpan.TraceIdLow);
            Assert.Equal(spanIdAsInt.Low, jaegerSpan.SpanId);
            Assert.Equal(new Int128(activity.ParentSpanId).Low, jaegerSpan.ParentSpanId);

            Assert.Empty(jaegerSpan.References);

            Assert.Equal(0x1, jaegerSpan.Flags);

            Assert.Equal(activity.StartTimeUtc.ToEpochMicroseconds(), jaegerSpan.StartTime);
            Assert.Equal(activity.Duration.TotalMilliseconds * 1000, jaegerSpan.Duration);

            var tags = jaegerSpan.Tags.ToArray();
            var tag = tags[0];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("stringKey", tag.Key);
            Assert.Equal("value", tag.VStr);
            tag = tags[1];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("longKey", tag.Key);
            Assert.Equal("1", tag.VStr);
            tag = tags[2];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("longKey2", tag.Key);
            Assert.Equal("1", tag.VStr);
            tag = tags[3];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("doubleKey", tag.Key);
            Assert.Equal("1", tag.VStr);
            tag = tags[4];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("doubleKey2", tag.Key);
            Assert.Equal("1", tag.VStr);
            tag = tags[5];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("boolKey", tag.Key);
            Assert.Equal("True", tag.VStr);

            // The second to last tag should be span.kind in this case
            tag = tags[tags.Length - 2];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("span.kind", tag.Key);
            Assert.Equal("client", tag.VStr);

            // The last tag should be library.name in this case
            tag = tags[tags.Length - 1];
            Assert.Equal(JaegerTagType.STRING, tag.VType);
            Assert.Equal("library.name", tag.Key);
            Assert.Equal(nameof(CreateTestActivity), tag.VStr);

            var logs = jaegerSpan.Logs.ToArray();
            var jaegerLog = logs[0];
            Assert.Equal(activity.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);
            Assert.Equal(2, jaegerLog.Fields.Count());
            var eventFields = jaegerLog.Fields.ToArray();
            var eventField = eventFields[0];
            Assert.Equal("key", eventField.Key);
            Assert.Equal("value", eventField.VStr);
            eventField = eventFields[1];
            Assert.Equal("message", eventField.Key);
            Assert.Equal("Event1", eventField.VStr);
            Assert.Equal(activity.Events.First().Timestamp.ToEpochMicroseconds(), jaegerLog.Timestamp);

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
        public void JaegerActivityConverterTest_GenerateJaegerSpan_RemoteEndpointOmittedByDefault()
        {
            // Arrange
            var span = CreateTestActivity();

            // Act
            var jaegerSpan = span.ToJaegerSpan();

            // Assert
            Assert.DoesNotContain(jaegerSpan.Tags, t => t.Key == "peer.service");
        }

        [Fact]
        public void JaegerActivityConverterTest_GenerateJaegerSpan_RemoteEndpointResolution()
        {
            // Arrange
            var span = CreateTestActivity(
                additionalAttributes: new Dictionary<string, object>
                {
                    ["net.peer.name"] = "RemoteServiceName",
                });

            // Act
            var jaegerSpan = span.ToJaegerSpan();

            // Assert
            Assert.Contains(jaegerSpan.Tags, t => t.Key == "peer.service");
            Assert.Equal("RemoteServiceName", jaegerSpan.Tags.First(t => t.Key == "peer.service").VStr);
        }

        [Fact]
        public void JaegerActivityConverterTest_GenerateJaegerSpan_PeerServiceNameIgnoredForServerSpan()
        {
            // Arrange
            var span = CreateTestActivity(
                additionalAttributes: new Dictionary<string, object>
                {
                    ["http.host"] = "DiscardedRemoteServiceName",
                },
                kind: ActivityKind.Server);

            // Act
            var jaegerSpan = span.ToJaegerSpan();

            // Assert
            Assert.Null(jaegerSpan.PeerServiceName);
            Assert.Empty(jaegerSpan.Tags.Where(t => t.Key == "peer.service"));
        }

        [Fact]
        public void JaegerActivityConverterTest_GenerateJaegerSpan_RemoteEndpointResolutionPriority()
        {
            // Arrange
            var span = CreateTestActivity(
                additionalAttributes: new Dictionary<string, object>
                {
                    ["http.host"] = "DiscardedRemoteServiceName",
                    ["peer.service"] = "RemoteServiceName",
                    ["peer.hostname"] = "DiscardedRemoteServiceName",
                });

            // Act
            var jaegerSpan = span.ToJaegerSpan();

            // Assert
            var tags = jaegerSpan.Tags.Where(t => t.Key == "peer.service");
            Assert.Single(tags);
            var tag = tags.First();
            Assert.Equal("RemoteServiceName", tag.VStr);
        }

        internal static Activity CreateTestActivity(
            bool setAttributes = true,
            Dictionary<string, object> additionalAttributes = null,
            bool addEvents = true,
            bool addLinks = true,
            Resource resource = null,
            ActivityKind kind = ActivityKind.Client)
        {
            var startTimestamp = DateTime.UtcNow;
            var endTimestamp = startTimestamp.AddSeconds(60);
            var eventTimestamp = DateTime.UtcNow;
            var traceId = ActivityTraceId.CreateFromString("e8ea7e9ac72de94e91fabc613f9686b2".AsSpan());

            var parentSpanId = ActivitySpanId.CreateFromBytes(new byte[] { 12, 23, 34, 45, 56, 67, 78, 89 });

            var attributes = new Dictionary<string, object>
            {
                { "stringKey", "value" },
                { "longKey", 1L },
                { "longKey2", 1 },
                { "doubleKey", 1D },
                { "doubleKey2", 1F },
                { "boolKey", true },
            };
            if (additionalAttributes != null)
            {
                foreach (var attribute in additionalAttributes)
                {
                    attributes.Add(attribute.Key, attribute.Value);
                }
            }

            var events = new List<ActivityEvent>
            {
                new ActivityEvent(
                    "Event1",
                    eventTimestamp,
                    new ActivityTagsCollection(new Dictionary<string, object>
                    {
                        { "key", "value" },
                    })),
                new ActivityEvent(
                    "Event2",
                    eventTimestamp,
                    new ActivityTagsCollection(new Dictionary<string, object>
                    {
                        { "key", "value" },
                    })),
            };

            var linkedSpanId = ActivitySpanId.CreateFromString("888915b6286b9c41".AsSpan());

            var activitySource = new ActivitySource(nameof(CreateTestActivity));

            var tags = setAttributes ?
                    attributes.Select(kvp => new KeyValuePair<string, object>(kvp.Key, kvp.Value.ToString()))
                    : null;
            var links = addLinks ?
                    new[]
                    {
                        new ActivityLink(new ActivityContext(
                            traceId,
                            linkedSpanId,
                            ActivityTraceFlags.Recorded)),
                    }
                    : null;

            var activity = activitySource.StartActivity(
                "Name",
                kind,
                parentContext: new ActivityContext(traceId, parentSpanId, ActivityTraceFlags.Recorded),
                tags,
                links,
                startTime: startTimestamp);

            if (addEvents)
            {
                foreach (var evnt in events)
                {
                    activity.AddEvent(evnt);
                }
            }

            activity.SetEndTime(endTimestamp);
            activity.Stop();

            return activity;
        }
    }
}
