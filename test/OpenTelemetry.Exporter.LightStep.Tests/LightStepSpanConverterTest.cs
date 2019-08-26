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

namespace OpenTelemetry.Exporter.LightStep.Tests
{
    using System;
    using System.Diagnostics;
    using OpenTelemetry.Exporter.LightStep;
    using OpenTelemetry.Resources;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;
    using Xunit;
    
    public class LightStepSpanConverterTest
    {
        [Fact]
        public void AllPropertiesShouldTranslate()
        {
            var startTs = DateTime.Now;
            var endTs = startTs.AddSeconds(60);
            var evtTs = DateTime.Now;

            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            var parentId = ActivitySpanId.CreateRandom();

            var traceIdInt = traceId.ToLSTraceId();
            var spanIdInt = spanId.ToLSSpanId();
            var parentIdInt = parentId.ToLSSpanId();

            var attrs = Attributes.Create(new Dictionary<string, object>
            {
                {"stringKey", "foo"}, {"longKey", 1L}, {"doubleKey", 1D}, {"boolKey", true},
            }, 0);
            var evts = TimedEvents<IEvent>.Create(new List<ITimedEvent<IEvent>>
            {
                TimedEvent<IEvent>.Create(
                    evtTs,
                    Event.Create(
                        "evt1",
                        new Dictionary<string, object>
                        {
                            {"key", "value"},
                        }
                        )
                    ),
                TimedEvent<IEvent>.Create(
                    evtTs,
                    Event.Create(
                        "evt2",
                        new Dictionary<string, object>
                        {
                            {"key", "value"},
                        }
                        )
                    ),
            }, 0);
            var linkedSpanId = ActivitySpanId.CreateRandom();
            var link = Link.FromSpanContext(SpanContext.Create(
                traceId, linkedSpanId, ActivityTraceFlags.Recorded, Tracestate.Empty));
            var links = LinkList.Create(new List<ILink> {link}, 0);
            var spanData = SpanData.Create(
                SpanContext.Create(
                    traceId, spanId, ActivityTraceFlags.Recorded, Tracestate.Empty
                ),
                parentId,
                Resource.Empty,
                "Test",
                startTs,
                attrs,
                evts,
                links,
                null,
                Status.Ok,
                SpanKind.Client,
                endTs
            );

            var lsSpan = spanData.ToLightStepSpan();
            
            Assert.Equal("Test", lsSpan.OperationName);
            Assert.Equal(2, lsSpan.Logs.Count);
            Assert.Equal(4, lsSpan.Tags.Count);
            
            Assert.Equal(traceIdInt, lsSpan.SpanContext.TraceId);
            Assert.Equal(spanIdInt, lsSpan.SpanContext.SpanId);
            Assert.Equal(parentIdInt, lsSpan.References[0].SpanContext.SpanId);
        }
    }
}
