// <copyright file="OtlpExporterTest.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using Google.Protobuf.Collections;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
#if NET452
using OpenTelemetry.Internal;
#endif
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using GrpcCore = Grpc.Core;
using OtlpCollector = Opentelemetry.Proto.Collector.Trace.V1;
using OtlpCommon = Opentelemetry.Proto.Common.V1;
using OtlpTrace = Opentelemetry.Proto.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
    public class OtlpExporterTest
    {
        static OtlpExporterTest()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            };

            ActivitySource.AddActivityListener(listener);
        }

        [Fact]
        public void OtlpExporter_BadArgs()
        {
            TracerProviderBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddOtlpExporter());
        }

        [Theory]
        [InlineData(true, null)]
        [InlineData(false, "test-service")]
        public void ToOtlpResourceSpansTest(bool addResource, string optionsServiceName)
        {
            var evenTags = new[] { new KeyValuePair<string, object>("k0", "v0") };
            var oddTags = new[] { new KeyValuePair<string, object>("k1", "v1") };
            var sources = new[]
            {
                new ActivitySource("even", "2.4.6"),
                new ActivitySource("odd", "1.3.5"),
            };

            using var exporter = new OtlpExporter(
                new OtlpExporterOptions
                {
                    ServiceName = optionsServiceName,
                },
                new NoopTraceServiceClient());

            if (addResource)
            {
                exporter.SetResource(
                    new Resources.Resource(
                        new List<KeyValuePair<string, object>>
                        {
                            new KeyValuePair<string, object>(Resources.ResourceSemanticConventions.AttributeServiceName, "service-name"),
                            new KeyValuePair<string, object>(Resources.ResourceSemanticConventions.AttributeServiceNamespace, "ns1"),
                        }));
            }
            else
            {
                exporter.SetResource(Resources.Resource.Empty);
            }

            var builder = Sdk.CreateTracerProviderBuilder()
                .AddSource(sources[0].Name)
                .AddSource(sources[1].Name);

            using var openTelemetrySdk = builder.Build();

            var processor = new BatchExportProcessor<Activity>(new TestExporter<Activity>(RunTest));
            const int numOfSpans = 10;
            bool isEven;
            for (var i = 0; i < numOfSpans; i++)
            {
                isEven = i % 2 == 0;
                var source = sources[i % 2];
                var activityKind = isEven ? ActivityKind.Client : ActivityKind.Server;
                var activityTags = isEven ? evenTags : oddTags;

                using Activity activity = source.StartActivity($"span-{i}", activityKind, parentContext: default, activityTags);
                processor.OnEnd(activity);
            }

            processor.Shutdown();

            void RunTest(Batch<Activity> batch)
            {
                var request = new OtlpCollector.ExportTraceServiceRequest();

                request.AddBatch(exporter.ProcessResource, batch);

                Assert.Single(request.ResourceSpans);
                var oltpResource = request.ResourceSpans.First().Resource;
                if (addResource)
                {
                    Assert.Contains(oltpResource.Attributes, (kvp) => kvp.Key == Resources.ResourceSemanticConventions.AttributeServiceName && kvp.Value.StringValue == "service-name");
                    Assert.Contains(oltpResource.Attributes, (kvp) => kvp.Key == Resources.ResourceSemanticConventions.AttributeServiceNamespace && kvp.Value.StringValue == "ns1");
                }
                else
                {
                    Assert.Contains(oltpResource.Attributes, (kvp) => kvp.Key == Resources.ResourceSemanticConventions.AttributeServiceName && kvp.Value.StringValue == optionsServiceName);
                }

                foreach (var instrumentationLibrarySpans in request.ResourceSpans.First().InstrumentationLibrarySpans)
                {
                    Assert.Equal(numOfSpans / 2, instrumentationLibrarySpans.Spans.Count);
                    Assert.NotNull(instrumentationLibrarySpans.InstrumentationLibrary);

                    var expectedSpanNames = new List<string>();
                    var start = instrumentationLibrarySpans.InstrumentationLibrary.Name == "even" ? 0 : 1;
                    for (var i = start; i < numOfSpans; i += 2)
                    {
                        expectedSpanNames.Add($"span-{i}");
                    }

                    var otlpSpans = instrumentationLibrarySpans.Spans;
                    Assert.Equal(expectedSpanNames.Count, otlpSpans.Count);

                    var kv0 = new OtlpCommon.KeyValue { Key = "k0", Value = new OtlpCommon.AnyValue { StringValue = "v0" } };
                    var kv1 = new OtlpCommon.KeyValue { Key = "k1", Value = new OtlpCommon.AnyValue { StringValue = "v1" } };

                    var expectedTag = instrumentationLibrarySpans.InstrumentationLibrary.Name == "even"
                        ? kv0
                        : kv1;

                    foreach (var otlpSpan in otlpSpans)
                    {
                        Assert.Contains(otlpSpan.Name, expectedSpanNames);
                        Assert.Contains(expectedTag, otlpSpan.Attributes);
                    }
                }
            }
        }

        [Fact]
        public void ToOtlpSpanTest()
        {
            using var activitySource = new ActivitySource(nameof(this.ToOtlpSpanTest));

            using var rootActivity = activitySource.StartActivity("root", ActivityKind.Producer);

            var attributes = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("bool", true),
                new KeyValuePair<string, object>("long", 1L),
                new KeyValuePair<string, object>("string", "text"),
                new KeyValuePair<string, object>("double", 3.14),
                new KeyValuePair<string, object>("int", 1),
                new KeyValuePair<string, object>("datetime", DateTime.UtcNow),
                new KeyValuePair<string, object>("bool_array", new bool[] { true, false }),
                new KeyValuePair<string, object>("int_array", new int[] { 1, 2 }),
                new KeyValuePair<string, object>("double_array", new double[] { 1.0, 2.09 }),
                new KeyValuePair<string, object>("string_array", new string[] { "a", "b" }),
            };

            foreach (var kvp in attributes)
            {
                rootActivity.SetTag(kvp.Key, kvp.Value);
            }

            var startTime = new DateTime(2020, 02, 20, 20, 20, 20, DateTimeKind.Utc);

            DateTimeOffset dateTimeOffset;
#if NET452
            dateTimeOffset = DateTimeOffsetExtensions.FromUnixTimeMilliseconds(0);
#else
            dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(0);
#endif

            var expectedUnixTimeTicks = (ulong)(startTime.Ticks - dateTimeOffset.Ticks);
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
            AssertOtlpAttributes(attributes, otlpSpan.Attributes);

            var expectedStartTimeUnixNano = 100 * expectedUnixTimeTicks;
            Assert.Equal(expectedStartTimeUnixNano, otlpSpan.StartTimeUnixNano);
            var expectedEndTimeUnixNano = expectedStartTimeUnixNano + (duration.TotalMilliseconds * 1_000_000);
            Assert.Equal(expectedEndTimeUnixNano, otlpSpan.EndTimeUnixNano);

            var childLinks = new List<ActivityLink> { new ActivityLink(rootActivity.Context, new ActivityTagsCollection(attributes)) };
            var childActivity = activitySource.StartActivity(
                "child",
                ActivityKind.Client,
                rootActivity.Context,
                links: childLinks);

            childActivity.SetStatus(Status.Error);

            var childEvents = new List<ActivityEvent> { new ActivityEvent("e0"), new ActivityEvent("e1", default, new ActivityTagsCollection(attributes)) };
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

            // Assert.Equal(OtlpTrace.Status.Types.StatusCode.NotFound, otlpSpan.Status.Code);

            Assert.Equal(Status.Error.Description ?? string.Empty, otlpSpan.Status.Message);
            Assert.Empty(otlpSpan.Attributes);

            Assert.Equal(childEvents.Count, otlpSpan.Events.Count);
            for (var i = 0; i < childEvents.Count; i++)
            {
                Assert.Equal(childEvents[i].Name, otlpSpan.Events[i].Name);
                AssertOtlpAttributes(childEvents[i].Tags.ToList(), otlpSpan.Events[i].Attributes);
            }

            childLinks.Reverse();
            Assert.Equal(childLinks.Count, otlpSpan.Links.Count);
            for (var i = 0; i < childLinks.Count; i++)
            {
                AssertOtlpAttributes(childLinks[i].Tags.ToList(), otlpSpan.Links[i].Attributes);
            }
        }

        [Fact]
        public void ToOtlpSpanPeerServiceTest()
        {
            using var activitySource = new ActivitySource(nameof(this.ToOtlpSpanTest));

            using var rootActivity = activitySource.StartActivity("root", ActivityKind.Client);

            rootActivity.SetTag(SemanticConventions.AttributeHttpHost, "opentelemetry.io");

            var otlpSpan = rootActivity.ToOtlpSpan();

            Assert.NotNull(otlpSpan);

            var peerService = otlpSpan.Attributes.FirstOrDefault(kvp => kvp.Key == SemanticConventions.AttributePeerService);

            Assert.NotNull(peerService);
            Assert.Equal("opentelemetry.io", peerService.Value.StringValue);
        }

        [Fact]
        public void UseOpenTelemetryProtocolActivityExporterWithCustomActivityProcessor()
        {
            const string ActivitySourceName = "otlp.test";
            TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

            bool startCalled = false;
            bool endCalled = false;

            testActivityProcessor.StartAction =
                (a) =>
                {
                    startCalled = true;
                };

            testActivityProcessor.EndAction =
                (a) =>
                {
                    endCalled = true;
                };

            var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                            .AddSource(ActivitySourceName)
                            .AddProcessor(testActivityProcessor)
                            .AddOtlpExporter()
                            .Build();

            using var source = new ActivitySource(ActivitySourceName);
            var activity = source.StartActivity("Test Otlp Activity");
            activity?.Stop();

            Assert.True(startCalled);
            Assert.True(endCalled);
        }

        private static void AssertOtlpAttributes(
            List<KeyValuePair<string, object>> expectedAttributes,
            RepeatedField<OtlpCommon.KeyValue> otlpAttributes)
        {
            int expectedSize = 0;
            for (int i = 0; i < expectedAttributes.Count; i++)
            {
                var current = expectedAttributes[i].Value;

                if (current.GetType().IsArray)
                {
                    if (current is bool[] boolArray)
                    {
                        int index = 0;
                        foreach (var item in boolArray)
                        {
                            Assert.Equal(expectedAttributes[i].Key, otlpAttributes[i + index].Key);
                            AssertOtlpAttributeValue(item, otlpAttributes[i + index]);
                            index++;
                            expectedSize++;
                        }
                    }
                    else if (current is int[] intArray)
                    {
                        int index = 1;
                        foreach (var item in intArray)
                        {
                            Assert.Equal(expectedAttributes[i].Key, otlpAttributes[i + index].Key);
                            AssertOtlpAttributeValue(item, otlpAttributes[i + index]);
                            index++;
                            expectedSize++;
                        }
                    }
                    else if (current is double[] doubleArray)
                    {
                        int index = 2;
                        foreach (var item in doubleArray)
                        {
                            Assert.Equal(expectedAttributes[i].Key, otlpAttributes[i + index].Key);
                            AssertOtlpAttributeValue(item, otlpAttributes[i + index]);
                            index++;
                            expectedSize++;
                        }
                    }
                    else if (current is string[] stringArray)
                    {
                        int index = 3;
                        foreach (var item in stringArray)
                        {
                            Assert.Equal(expectedAttributes[i].Key, otlpAttributes[i + index].Key);
                            AssertOtlpAttributeValue(item, otlpAttributes[i + index]);
                            index++;
                            expectedSize++;
                        }
                    }
                }
                else
                {
                    Assert.Equal(expectedAttributes[i].Key, otlpAttributes[i].Key);
                    AssertOtlpAttributeValue(current, otlpAttributes[i]);
                    expectedSize++;
                }
            }

            Assert.Equal(expectedSize, otlpAttributes.Count);
        }

        private static void AssertOtlpAttributeValue(object originalValue, OtlpCommon.KeyValue akv)
        {
            switch (originalValue)
            {
                case string s:
                    Assert.Equal(s, akv.Value.StringValue);
                    break;
                case bool b:
                    Assert.Equal(b, akv.Value.BoolValue);
                    break;
                case long l:
                    Assert.Equal(l, akv.Value.IntValue);
                    break;
                case double d:
                    Assert.Equal(d, akv.Value.DoubleValue);
                    break;
                case int i:
                    Assert.Equal(i, akv.Value.IntValue);
                    break;
                default:
                    Assert.Equal(originalValue.ToString(), akv.Value.StringValue);
                    break;
            }
        }

        private class NoopTraceServiceClient : OtlpCollector.TraceService.ITraceServiceClient
        {
            public OtlpCollector.ExportTraceServiceResponse Export(OtlpCollector.ExportTraceServiceRequest request, GrpcCore.Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default)
            {
                return null;
            }
        }
    }
}
