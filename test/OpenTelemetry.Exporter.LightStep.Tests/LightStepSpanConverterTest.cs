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

            var span = new TestSpan(
                "Test",
                new SpanContext(traceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded),
                SpanKind.Client,
                startTs,
                new[] { link },
                parentId,
                attrs,
                evts,
                Status.Ok,
                endTs);

            var spanIdInt = span.Context.SpanId.ToLSSpanId();

            var lsSpan = span.ToLightStepSpan();

            Assert.Equal("Test", lsSpan.OperationName);
            Assert.Equal(2, lsSpan.Logs.Count);
            Assert.Equal(4, lsSpan.Tags.Count);

            Assert.Equal(traceIdInt, lsSpan.SpanContext.TraceId);
            Assert.Equal(spanIdInt, lsSpan.SpanContext.SpanId);
            Assert.Equal(parentIdInt, lsSpan.References[0].SpanContext.SpanId);
        }
    }

    internal class TestSpan : IReadableSpan
    {
        public TestSpan(string name,
            Trace.SpanContext context,
            SpanKind kind,
            DateTimeOffset startTimestamp,
            IEnumerable<Link> links,
            ActivitySpanId parentSpanId,
            IEnumerable<KeyValuePair<string, object>> attributes,
            IEnumerable<Event> events,
            Status status,
            DateTimeOffset endTimestamp)
        {
            this.Name = name;
            this.Context = context;
            this.Kind = kind;
            this.StartTimestamp = startTimestamp;
            this.Links = links;
            this.ParentSpanId = parentSpanId;
            this.Attributes = attributes;
            this.Events = events;
            this.Status = status;
            this.EndTimestamp = endTimestamp;
        }

        public Trace.SpanContext Context { get; }
        public string Name { get; }
        public Status Status { get; }
        public ActivitySpanId ParentSpanId { get; }
        public IEnumerable<KeyValuePair<string, object>> Attributes { get; }
        public IEnumerable<Event> Events { get; }
        public IEnumerable<Link> Links { get; }
        public DateTimeOffset StartTimestamp { get; }
        public DateTimeOffset EndTimestamp { get; }
        public SpanKind? Kind { get; }
        public Resource LibraryResource { get; }
    }
}
