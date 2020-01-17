// <copyright file="spanDataHelper.cs" company="OpenTelemetry Authors">
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
using Moq;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.Jaeger.Tests.Implementation
{
    class SpanDataHelper
    {
        public static SpanData CreateSpanData(string name,
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
