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
using System.Linq;
using Google.Protobuf.Collections;
using Moq;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using Xunit;
using OtlpCommon = Opentelemetry.Proto.Common.V1;
using OtlpTrace = Opentelemetry.Proto.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
    public class SpanDataExtensionsTest
    {
        [Fact]
        public void ToOtlpResourceSpansTest()
        {
            var spanProcessor = new Mock<SpanProcessor>();

            var evenResource = new Resource(new[] { new KeyValuePair<string, object>("k0", "v0") });
            var oddResource = new Resource(new[] { new KeyValuePair<string, object>("k1", "v1") });
            var tracers = new[]
            {
                TracerFactory.Create(b => b.SetResource(evenResource)
                    .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object)))
                    .GetTracer("even", "2.4.6"),
                TracerFactory.Create(b => b.SetResource(oddResource)
                    .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object)))
                    .GetTracer("odd", "1.3.5"),
            };

            TelemetrySpan span = null;
            const int numOfSpans = 10;
            for (var i = 0; i < numOfSpans; i++)
            {
                var isEven = i % 2 == 0;
                var tracer = tracers[i % 2];
                var spanKind = isEven ? SpanKind.Client : SpanKind.Server;
                if (span == null)
                {
                    span = tracer.StartRootSpan("span-0", spanKind, null);
                }
                else
                {
                    span = tracer.StartSpan($"span-{i}", span.Context, spanKind, null);
                }

                span.End();
            }

            var spanDataList = new List<SpanData>();
            var invocations = spanProcessor.Invocations;
            for (var i = 0; i < invocations.Count; i += 2 /* Just want one of the OnStart/OnEnd pair. */)
            {
                spanDataList.Add((SpanData)invocations[i].Arguments[0]);
            }

            spanDataList.Reverse();

            var otlpResourceSpans = spanDataList.ToOtlpResourceSpans();

            Assert.Equal(2, otlpResourceSpans.Count());

            var evenAttribKeyValue = new OtlpCommon.AttributeKeyValue { Key = "k0" };
            evenAttribKeyValue.StringValue = "v0";
            foreach (var resourceSpans in otlpResourceSpans)
            {
                Assert.Single(resourceSpans.InstrumentationLibrarySpans);
                Assert.Equal(numOfSpans / 2, resourceSpans.InstrumentationLibrarySpans[0].Spans.Count);
                Assert.NotNull(resourceSpans.Resource);

                var expectedSpanNames = new List<string>();
                var start = resourceSpans.Resource.Attributes.Contains(evenAttribKeyValue) ? 0 : 1;
                for (var i = start; i < numOfSpans; i += 2)
                {
                    expectedSpanNames.Add($"span-{i}");
                }

                var otlpSpans = resourceSpans.InstrumentationLibrarySpans[0].Spans;
                Assert.Equal(expectedSpanNames.Count, otlpSpans.Count);
                foreach (var otlpSpan in otlpSpans)
                {
                    Assert.Contains(otlpSpan.Name, expectedSpanNames);
                }
            }
        }

        [Fact]
        public void ToOtlpSpanTest()
        {
            var startTimestamp = new DateTimeOffset(2020, 02, 20, 20, 20, 20, TimeSpan.Zero);
            var expectedUnixTimeTicks = (ulong)startTimestamp.ToUnixTimeSeconds() * TimeSpan.TicksPerSecond;
            var duration = TimeSpan.FromMilliseconds(1555);

            var attributes = new Dictionary<string, object>
            {
                ["bool"] = true,
                ["long"] = 1L,
                ["string"] = "text",
                ["double"] = 3.14,
                ["unknow_attrib_type"] =
                    new byte[] { 1 }, // TODO: update when arrays of standard attribute types are supported
            };

            var rootSpan = CreateSpanData(
                "root",
                default,
                SpanKind.Producer,
                default(Status),
                startTimestamp,
                duration,
                attributes);

            Span<byte> traceIdSpan = stackalloc byte[16];
            rootSpan.Context.TraceId.CopyTo(traceIdSpan);
            var traceId = traceIdSpan.ToArray();

            var otlpSpan = rootSpan.ToOtlpSpan();

            Assert.NotNull(otlpSpan);
            Assert.Equal("root", otlpSpan.Name);
            Assert.Equal(SpanKind.Producer, (SpanKind)otlpSpan.Kind);
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

            var childEvents = new[] { new Event("e0"), new Event("e1", attributes) };
            var childLinks = new[] { new Link(rootSpan.Context, attributes) };
            var childSpan = CreateSpanData(
                "child",
                rootSpan.Context,
                SpanKind.Client,
                Status.NotFound,
                events: childEvents,
                links: childLinks);

            Span<byte> parentIdSpan = stackalloc byte[8];
            rootSpan.Context.SpanId.CopyTo(parentIdSpan);
            var parentId = parentIdSpan.ToArray();

            otlpSpan = childSpan.ToOtlpSpan();

            Assert.NotNull(otlpSpan);
            Assert.Equal("child", otlpSpan.Name);
            Assert.Equal(SpanKind.Client, (SpanKind)otlpSpan.Kind);
            Assert.Equal(traceId, otlpSpan.TraceId);
            Assert.Equal(parentId, otlpSpan.ParentSpanId);
            Assert.Equal(OtlpTrace.Status.Types.StatusCode.NotFound, otlpSpan.Status.Code);
            Assert.Equal(Status.NotFound.Description ?? string.Empty, otlpSpan.Status.Message);
            Assert.Empty(otlpSpan.Attributes);
            Assert.Equal(childEvents.Length, otlpSpan.Events.Count);
            for (var i = 0; i < childEvents.Length; i++)
            {
                Assert.Equal(childEvents[i].Name, otlpSpan.Events[i].Name);
                AssertOtlpAttributes(childEvents[i].Attributes, otlpSpan.Events[i].Attributes);
            }

            Assert.Equal(childLinks.Length, otlpSpan.Links.Count);
            for (var i = 0; i < childLinks.Length; i++)
            {
                AssertOtlpAttributes(childLinks[i].Attributes, otlpSpan.Links[i].Attributes);
            }
        }

        private static void AssertOtlpAttributes(
            IDictionary<string, object> expectedAttributes,
            RepeatedField<OtlpCommon.AttributeKeyValue> otlpAttributes)
        {
            Assert.Equal(expectedAttributes.Count, otlpAttributes.Count);
            foreach (var otlpAttrib in otlpAttributes)
            {
                Assert.True(expectedAttributes.TryGetValue(otlpAttrib.Key, out object originalObj));
                AssertOtlpAttributeValue(otlpAttrib, originalObj);
            }
        }

        private static void AssertOtlpAttributeValue(OtlpCommon.AttributeKeyValue akv, object originalValue)
        {
            switch (originalValue)
            {
                case string s:
                    Assert.Equal(akv.StringValue, s);
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

        private static SpanData CreateSpanData(
            string name,
            SpanContext parentContext,
            SpanKind kind,
            Status status,
            DateTimeOffset? startTimestamp = null,
            TimeSpan? duration = null,
            IDictionary<string, object> attributes = null,
            IEnumerable<Link> links = null,
            IEnumerable<Event> events = null,
            string library = "otlp-tester")
        {
            var spanProcessor = new Mock<SpanProcessor>();
            spanProcessor.Setup(p => p.OnEnd(It.IsAny<SpanData>()));

            var tracer = TracerFactory.Create(b => b
                .AddProcessorPipeline(p => p.AddProcessor(_ => spanProcessor.Object)))
                .GetTracer(library, "1.0.1");

            SpanCreationOptions spanOptions = null;

            var startTime = startTimestamp ?? DateTimeOffset.UtcNow;
            spanOptions = new SpanCreationOptions
            {
                Links = links,
                Attributes = attributes,
                StartTimestamp = startTime,
            };

            TelemetrySpan span;
            if (parentContext == default)
            {
                span = tracer.StartRootSpan(name, kind, spanOptions);
            }
            else
            {
                span = tracer.StartSpan(name, parentContext, kind, spanOptions);
            }

            if (events != null)
            {
                foreach (var ev in events)
                {
                    span.AddEvent(ev);
                }
            }

            span.Status = status;

            if (!duration.HasValue)
            {
                span.End();
            }
            else
            {
                span.End(startTime.Add(duration.Value));
            }

            return (SpanData)spanProcessor.Invocations[0].Arguments[0];
        }
    }
}
