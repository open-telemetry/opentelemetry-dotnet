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
using System.Linq;
using OpenTelemetry.Exporter.LightStep.Implementation;

namespace OpenTelemetry.Exporter.LightStep.Tests
{
    using System;
    using System.Diagnostics;
    using OpenTelemetry.Trace;
    using Xunit;

    public class LightStepSpanConverterTest
    {
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
            var link = new Link(new SpanContext(
                traceId, linkedSpanId, ActivityTraceFlags.Recorded, Tracestate.Empty));

            var span = (Span)Tracing.Tracer
                .SpanBuilder("Test")
                .SetParent(new SpanContext(traceId, parentId, ActivityTraceFlags.Recorded, Tracestate.Empty))
                .SetSpanKind(SpanKind.Client)
                .AddLink(link)
                .SetStartTimestamp(startTs)
                .StartSpan();

            var spanIdInt = span.Context.SpanId.ToLSSpanId();

            foreach (var attribute in attrs)
            {
                span.SetAttribute(attribute);
            }

            foreach (var evnt in evts)
            {
                span.AddEvent(evnt);
            }


            span.End(endTs);
            span.Status = Status.Ok;

            var lsSpan = span.ToLightStepSpan();

            Assert.Equal("Test", lsSpan.OperationName);
            Assert.Equal(2, lsSpan.Logs.Count);
            Assert.Equal(4, lsSpan.Tags.Count);

            Assert.Equal(traceIdInt, lsSpan.SpanContext.TraceId);
            Assert.Equal(spanIdInt, lsSpan.SpanContext.SpanId);
            Assert.Equal(parentIdInt, lsSpan.References[0].SpanContext.SpanId);
        }
    }
}
