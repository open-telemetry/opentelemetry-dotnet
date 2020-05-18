// <copyright file="SpanDataExtensionsTest.cs" company="OpenTelemetry Authors">
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

using Google.Protobuf.Collections;

using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

using OtlpCommon = Opentelemetry.Proto.Common.V1;
using OtlpTrace = Opentelemetry.Proto.Trace.V1;

using Xunit;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
    public class ActivityExtensionsTest
    {
        static ActivityExtensionsTest()
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
        public void ToOtlpResourceSpansTest()
        {
            var evenTags = new []{ new KeyValuePair<string, string>("k0", "v0") };
            var oddTags = new []{ new KeyValuePair<string, string>("k1", "v1") };
            var sources = new[]
            {
                new ActivitySource("even", "2.4.6"),
                new ActivitySource("odd", "1.3.5"),
            };

            var activities = new List<Activity>();
            Activity activity = null;
            const int numOfSpans = 10;
            bool isEven;
            for (var i = 0; i < numOfSpans; i++)
            {
                isEven = i % 2 == 0;
                var source = sources[i % 2];
                var activityKind = isEven ? ActivityKind.Client : ActivityKind.Server;
                var activityTags = isEven ? evenTags : oddTags;

                activity = source.StartActivity($"span-{i}", activityKind, activity?.Context ?? default, activityTags);

                activities.Add(activity);
            }

            activities.Reverse();

            var otlpResourceSpans = activities.ToOtlpResourceSpans();

            Assert.Single(otlpResourceSpans);

            foreach (var instrumentationLibrarySpans in otlpResourceSpans.First().InstrumentationLibrarySpans)
            {
                Assert.Equal(numOfSpans/2, instrumentationLibrarySpans.Spans.Count);
                Assert.NotNull(instrumentationLibrarySpans.InstrumentationLibrary);

                var expectedSpanNames = new List<string>();
                var start = instrumentationLibrarySpans.InstrumentationLibrary.Name == "even" ? 0 : 1;
                for (var i = start; i < numOfSpans; i += 2)
                {
                    expectedSpanNames.Add($"span-{i}");
                }

                var otlpSpans = instrumentationLibrarySpans.Spans;
                Assert.Equal(expectedSpanNames.Count, otlpSpans.Count);

                var expectedTag = instrumentationLibrarySpans.InstrumentationLibrary.Name == "even"
                    ? new OtlpCommon.AttributeKeyValue { Key = "k0", StringValue = "v0" }
                    : new OtlpCommon.AttributeKeyValue { Key = "k1", StringValue = "v1" };
                foreach (var otlpSpan in otlpSpans)
                {
                    Assert.Contains(otlpSpan.Name, expectedSpanNames);
                    Assert.Contains(expectedTag, otlpSpan.Attributes);
                }
            }
        }

        [Fact]
        public void ToOtlpSpanTest()
        {
            var activitySource = new ActivitySource(nameof(this.ToOtlpSpanTest));

            using var rootActivity = activitySource.StartActivity("root", ActivityKind.Producer);

            var attributes = new Dictionary<string, object>
            {
                ["bool"] = true,
                ["long"] = 1L,
                ["string"] = "text",
                ["double"] = 3.14,
                ["unknown_attrib_type"] =
                    new byte[] { 1 }, // TODO: update if arrays of standard attribute types are supported
            };

            var tags = new List<KeyValuePair<string, string>>(attributes.Count);
            foreach (var kvp in attributes)
            {
                rootActivity.AddTag(kvp.Key, kvp.Value.ToString());
                tags.Add(new KeyValuePair<string, string>(kvp.Key, kvp.Value.ToString()));
            }

            tags.Reverse(); // Activity.AddTag put tags on the front of backing collection.

            var startTime = new DateTime(2020, 02, 20, 20, 20, 20, DateTimeKind.Utc);
            var expectedUnixTimeTicks = (ulong)(startTime.Ticks - DateTimeOffset.FromUnixTimeMilliseconds(0).Ticks);
            var duration = TimeSpan.FromMilliseconds(1555);

            rootActivity.SetStartTime(startTime);
            rootActivity.SetEndTime(startTime + duration);

            Span<byte> traceIdSpan = stackalloc byte[16];
            rootActivity.TraceId.CopyTo(traceIdSpan);
            var traceId = traceIdSpan.ToArray();

            var otlpSpan = rootActivity.ToOtlpSpan();

            Assert.NotNull(otlpSpan);
            Assert.Equal("root", otlpSpan.Name);
            Assert.Equal(OtlpTrace.Span.Types.SpanKind.Producer, otlpSpan.Kind);
            Assert.Equal(traceId, otlpSpan.TraceId);
            Assert.Empty(otlpSpan.ParentSpanId);
            Assert.Null(otlpSpan.Status);
            Assert.Empty(otlpSpan.Events);
            Assert.Empty(otlpSpan.Links);
            AssertActivityTagsIntoOtlpAttributes(tags, otlpSpan.Attributes);

            var expectedStartTimeUnixNano = 100 * expectedUnixTimeTicks;
            Assert.Equal(expectedStartTimeUnixNano, otlpSpan.StartTimeUnixNano);
            var expectedEndTimeUnixNano = expectedStartTimeUnixNano + duration.TotalMilliseconds * 1_000_000;
            Assert.Equal(expectedEndTimeUnixNano, otlpSpan.EndTimeUnixNano);

            var childLinks = new List<ActivityLink> { new ActivityLink(rootActivity.Context, attributes) };
            var childActivity = activitySource.StartActivity(
                "child",
                ActivityKind.Client,
                rootActivity.Context,
                links: childLinks);

            var childEvents = new List<ActivityEvent> { new ActivityEvent("e0"), new ActivityEvent("e1", attributes) };
            childActivity.AddEvent(childEvents[0]);
            childActivity.AddEvent(childEvents[1]);

            Span<byte> parentIdSpan = stackalloc byte[8];
            rootActivity.Context.SpanId.CopyTo(parentIdSpan);
            var parentId = parentIdSpan.ToArray();

            otlpSpan = childActivity.ToOtlpSpan();

            Assert.NotNull(otlpSpan);
            Assert.Equal("child", otlpSpan.Name);
            Assert.Equal(OtlpTrace.Span.Types.SpanKind.Client, otlpSpan.Kind);
            Assert.Equal(traceId, otlpSpan.TraceId);
            Assert.Equal(parentId, otlpSpan.ParentSpanId);
            Assert.Empty(otlpSpan.Attributes);

            childEvents.Reverse();
            Assert.Equal(childEvents.Count, otlpSpan.Events.Count);
            for (var i = 0; i < childEvents.Count; i++)
            {
                Assert.Equal(childEvents[i].Name, otlpSpan.Events[i].Name);
                AssertOtlpAttributes(childEvents[i].Attributes.ToList(), otlpSpan.Events[i].Attributes);
            }

            childLinks.Reverse();
            Assert.Equal(childLinks.Count, otlpSpan.Links.Count);
            for (var i = 0; i < childLinks.Count; i++)
            {
                AssertOtlpAttributes(childLinks[i].Attributes.ToList(), otlpSpan.Links[i].Attributes);
            }
        }

        private static void AssertActivityTagsIntoOtlpAttributes(
            List<KeyValuePair<string, string>> expectedTags,
            RepeatedField<OtlpCommon.AttributeKeyValue> otlpAttributes)
        {
            Assert.Equal(expectedTags.Count, otlpAttributes.Count);
            for (var i = 0; i < expectedTags.Count; i++)
            {
                Assert.Equal(expectedTags[i].Key, otlpAttributes[i].Key);
                AssertOtlpAttributeValue(expectedTags[i].Value, otlpAttributes[i]);
            }
        }

        private static void AssertOtlpAttributes(
            List<KeyValuePair<string, object>> expectedAttributes,
            RepeatedField<OtlpCommon.AttributeKeyValue> otlpAttributes)
        {
            Assert.Equal(expectedAttributes.Count(), otlpAttributes.Count);
            for (int i = 0; i < otlpAttributes.Count; i++)
            {
                Assert.Equal(expectedAttributes[i].Key, otlpAttributes[i].Key);
                AssertOtlpAttributeValue(expectedAttributes[i].Value, otlpAttributes[i]);
            }
        }

        private static void AssertOtlpAttributeValue(object originalValue, OtlpCommon.AttributeKeyValue akv)
        {
            switch (originalValue)
            {
                case string s:
                    Assert.Equal(akv.StringValue,  s);
                    break;
                case bool b:
                    Assert.Equal(akv.BoolValue, b);
                    break;
                case long l:
                    Assert.Equal(akv.IntValue, l);
                    break;
                case double d:
                    Assert.Equal(akv.DoubleValue, d);
                    break;
                default:
                    Assert.Equal(akv.StringValue, originalValue?.ToString());
                    break;
            }
        }
    }
}
