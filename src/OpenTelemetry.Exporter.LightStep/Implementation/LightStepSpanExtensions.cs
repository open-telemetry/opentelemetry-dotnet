// <copyright file="LightStepSpanExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.LightStep.Implementation
{
    public static class LightStepSpanExtensions
    {
        public static LightStepSpan ToLightStepSpan(this SpanData data)
        {
            var duration = data.EndTimestamp - data.StartTimestamp;
            var span = new LightStepSpan();
            if (data.ParentSpanId != default)
            {
                var sc = new SpanContext { SpanId = data.ParentSpanId.ToLSSpanId() };
                span.References.Add(new Reference
                {
                    Relationship = "CHILD_OF",
                    SpanContext = sc,
                });
            }

            span.OperationName = data.Name;
            var traceId = data.Context.TraceId.ToLSTraceId();
            var spanId = data.Context.SpanId.ToLSSpanId();

            span.SpanContext = new SpanContext
            {
                SpanId = spanId,
                TraceId = traceId,
            };
            span.StartTimestamp = data.StartTimestamp.UtcDateTime;
            span.DurationMicros = Convert.ToUInt64(Math.Abs(duration.Ticks) / 10);

            foreach (var attr in data.Attributes)
            {
                span.Tags.Add(new Tag { Key = attr.Key, StringValue = attr.Value.ToString() });
            }

            foreach (var evt in data.Events)
            {
                var fields = new List<Tag>();

                // TODO: Make this actually pass attributes in correctly
                fields.Add(new Tag { Key = evt.Name, StringValue = evt.Attributes.ToString() });
                span.Logs.Add(new Log { Timestamp = evt.Timestamp.UtcDateTime, Fields = fields });
            }

            return span;
        }

        public static ulong ToLSTraceId(this ActivityTraceId traceId)
        {
            var id = traceId.ToHexString();

            if (id.Length > 16)
            {
                id = id.Substring(id.Length - 16, 16);
            }

            return Convert.ToUInt64(id, 16);
        }

        public static ulong ToLSSpanId(this ActivitySpanId spanId)
        {
            var id = spanId.ToHexString();

            if (id.Length > 16)
            {
                id = id.Substring(id.Length - 16, 16);
            }

            return Convert.ToUInt64(id, 16);
        }
    }
}
