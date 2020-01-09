// <copyright file="LightStepSpanConverterTest.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using OpenTelemetry.Exporter.LightStep.Implementation;
using OpenTelemetry.Trace.Configuration;
using System;
using System.Diagnostics;
using Moq;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;
using Xunit;
using SpanContext = OpenTelemetry.Trace.SpanContext;

namespace OpenTelemetry.Exporter.LightStep.Tests
{
    public class LightStepSpanConverterTest
    {
        private readonly Tracer tracer;

        public LightStepSpanConverterTest()
        {
            tracer = TracerFactory.Create(b => { }).GetTracer(null);
        }

        [Fact]
        public void AllPropertiesShouldTranslate()
        {
            var startTs = DateTime.UtcNow;
            var endTs = startTs.AddSeconds(60);
            var evtTs = DateTime.UtcNow;

            var traceId = ActivityTraceId.CreateRandom();
            var parentId = ActivitySpanId.CreateRandom();

            var traceIdInt = traceId.ToLSTraceId();
            var parentIdInt = parentId.ToLSSpanId();

            var attrs = new Dictionary<string, object>
            {
                ["stringKey"] = "foo",
                ["longKey"] = 1L,
                ["doubleKey"] = 1D,
                ["boolKey"] = true,
            };

            var evts = new List<Event>
            {
                new Event(
                    "evt1",
                    evtTs,
                    new Dictionary<string, object> {{"key", "value"},}
                ),
                new Event(
                    "evt2",
                    evtTs,
                    new Dictionary<string, object> {{"key", "value"},}
                ),
            };

            var linkedSpanId = ActivitySpanId.CreateRandom();
            var link = new Link(new Trace.SpanContext(
                traceId, linkedSpanId, ActivityTraceFlags.Recorded));

            var spanData = CreateSpanData(
                "Test",
                new SpanContext(traceId, parentId, ActivityTraceFlags.Recorded),
                SpanKind.Client,
                startTs,
                new[] { link },
                attrs,
                evts,
                Status.Ok,
                endTs);

            var spanIdInt = spanData.Context.SpanId.ToLSSpanId();

            var lsSpan = spanData.ToLightStepSpan();

            Assert.Equal("Test", lsSpan.OperationName);
            Assert.Equal(2, lsSpan.Logs.Count);
            Assert.Equal(4, lsSpan.Tags.Count);

            Assert.Equal(traceIdInt, lsSpan.SpanContext.TraceId);
            Assert.Equal(spanIdInt, lsSpan.SpanContext.SpanId);
            Assert.Equal(parentIdInt, lsSpan.References[0].SpanContext.SpanId);
        }

        private static SpanData CreateSpanData(string name,
            SpanContext parentContext,
            SpanKind kind,
            DateTimeOffset startTimestamp,
            IEnumerable<Link> links,
            IDictionary<string, object> attributes,
            IEnumerable<Event> events,
            Status status,
            DateTimeOffset endTimestamp)
        {
            var processor = new Mock<SpanProcessor>();

            processor.Setup(p => p.OnEnd(It.IsAny<SpanData>()));

            var tracer = TracerFactory.Create(b =>
                    b.AddProcessorPipeline(p =>
                        p.AddProcessor(_ => processor.Object)))
                .GetTracer(null);

            SpanCreationOptions spanOptions = null;

            if (links != null || attributes != null || startTimestamp != default)
            {
                spanOptions = new SpanCreationOptions
                {
                    Links = links,
                    Attributes = attributes,
                    StartTimestamp = startTimestamp,
                };
            }
            var span = tracer.StartSpan(name, parentContext, kind, spanOptions);

            if (events != null)
            {
                foreach (var evnt in events)
                {
                    span.AddEvent(evnt);
                }
            }

            span.Status = status.IsValid ? status : Status.Ok;
            if (endTimestamp == default)
            {
                span.End();
            }
            else
            {
                span.End(endTimestamp);
            }

            return (SpanData)processor.Invocations[0].Arguments[0];
        }
    }
}
