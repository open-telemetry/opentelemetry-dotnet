// <copyright file="JaegerConversionExtensions.cs" company="OpenTelemetry Authors">
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
using System.Drawing;
using System.Linq;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    public static class JaegerConversionExtensions
    {
        private const int DaysPerYear = 365;

        // Number of days in 4 years
        private const int DaysPer4Years = (DaysPerYear * 4) + 1;       // 1461

        // Number of days in 100 years
        private const int DaysPer100Years = (DaysPer4Years * 25) - 1;  // 36524

        // Number of days in 400 years
        private const int DaysPer400Years = (DaysPer100Years * 4) + 1; // 146097

        // Number of days from 1/1/0001 to 12/31/1969
        private const int DaysTo1970 = (DaysPer400Years * 4) + (DaysPer100Years * 3) + (DaysPer4Years * 17) + DaysPerYear; // 719,162

        private const long UnixEpochTicks = DaysTo1970 * TimeSpan.TicksPerDay;
        private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        private const long UnixEpochMicroseconds = UnixEpochTicks / TicksPerMicrosecond; // 62,135,596,800,000,000

        public static JaegerSpan ToJaegerSpan(this Span span)
        {
            List<JaegerTag> jaegerTags = null;

            if (span?.Attributes is IEnumerable<KeyValuePair<string, object>> attributeMap)
            {
                jaegerTags = attributeMap.Select(a => a.ToJaegerTag()).ToList();
            }

            // The Span.Kind must translate into a tag.
            // See https://opentracing.io/specification/conventions/
            if (span.Kind.HasValue)
            {
                string spanKind = null;

                if (span.Kind.Value == SpanKind.Server)
                {
                    spanKind = "server";
                }
                else if (span.Kind.Value == SpanKind.Client)
                {
                    spanKind = "client";
                }
                else if (span.Kind.Value == SpanKind.Consumer)
                {
                    spanKind = "consumer";
                }
                else if (span.Kind.Value == SpanKind.Producer)
                {
                    spanKind = "producer";
                }

                if (spanKind != null)
                {
                    jaegerTags.Add(new JaegerTag
                    {
                        Key = "span.kind",
                        VType = JaegerTagType.STRING,
                        VStr = spanKind,
                    });
                }
            }

            IEnumerable<JaegerLog> jaegerLogs = null;

            if (span?.Events != null)
            {
                jaegerLogs = span.Events.Select(e => e.ToJaegerLog()).AsEnumerable();
            }

            IEnumerable<JaegerSpanRef> refs = null;

            if (span?.Links != null)
            {
                refs = span.Links.Select(l => l.ToJaegerSpanRef()).Where(l => l != null).AsEnumerable();
            }

            var traceId = Int128.Empty;
            var spanId = Int128.Empty;
            var parentSpanId = Int128.Empty;

            if (span != null && span.Context.IsValid)
            {
                traceId = new Int128(span.Context.TraceId);
                spanId = new Int128(span.Context.SpanId);
                parentSpanId = new Int128(span.ParentSpanId);
            }

            return new JaegerSpan
            {
                TraceIdHigh = traceId.High,
                TraceIdLow = traceId.Low,
                SpanId = spanId.Low,
                ParentSpanId = parentSpanId.Low,
                OperationName = span.Name,
                References = refs,
                Flags = (span.Context.TraceOptions & ActivityTraceFlags.Recorded) > 0 ? 0x1 : 0,
                StartTime = ToEpochMicroseconds(span.StartTimestamp),
                Duration = ToEpochMicroseconds(span.EndTimestamp) - ToEpochMicroseconds(span.StartTimestamp),
                JaegerTags = jaegerTags,
                Logs = jaegerLogs,
            };
        }

        public static JaegerTag ToJaegerTag(this KeyValuePair<string, object> attribute)
        {
            switch (attribute.Value)
            {
                case string s:
                    return new JaegerTag { Key = attribute.Key, VType = JaegerTagType.STRING, VStr = s };
                case int i:
                    return new JaegerTag { Key = attribute.Key, VType = JaegerTagType.LONG, VLong = Convert.ToInt64(i) };
                case long l:
                    return new JaegerTag { Key = attribute.Key, VType = JaegerTagType.LONG, VLong = l };
                case float f:
                    return new JaegerTag { Key = attribute.Key, VType = JaegerTagType.DOUBLE, VDouble = Convert.ToDouble(f) };
                case double d:
                    return new JaegerTag { Key = attribute.Key, VType = JaegerTagType.DOUBLE, VDouble = d };
                case bool b:
                    return new JaegerTag { Key = attribute.Key, VType = JaegerTagType.BOOL, VBool = b };
            }

            return new JaegerTag { Key = attribute.Key, VType = JaegerTagType.STRING, VStr = attribute.Value.ToString() };
        }

        public static JaegerLog ToJaegerLog(this Event timedEvent)
        {
            var tags = timedEvent.Attributes.Select(a => a.ToJaegerTag()).ToList();

            // Matches what OpenTracing and OpenTelemetry defines as the event name.
            // https://github.com/opentracing/specification/blob/master/semantic_conventions.md#log-fields-table
            // https://github.com/open-telemetry/opentelemetry-specification/pull/397/files
            tags.Add(new JaegerTag { Key = "message", VType = JaegerTagType.STRING, VStr = timedEvent.Name });

            return new JaegerLog
            {
                Timestamp = timedEvent.Timestamp.ToEpochMicroseconds(),
                Fields = tags,
            };
        }

        public static JaegerSpanRef ToJaegerSpanRef(this Link link)
        {
            var traceId = Int128.Empty;
            var spanId = Int128.Empty;

            if (link != default)
            {
                traceId = new Int128(link.Context.TraceId);
                spanId = new Int128(link.Context.SpanId);
            }

            return new JaegerSpanRef
            {
                TraceIdHigh = traceId.High,
                TraceIdLow = traceId.Low,
                SpanId = spanId.Low,
                RefType = JaegerSpanRefType.CHILD_OF,
            };
        }

        public static long ToEpochMicroseconds(this DateTimeOffset timestamp)
        {
            // Truncate sub-microsecond precision before offsetting by the Unix Epoch to avoid
            // the last digit being off by one for dates that result in negative Unix times
            long microseconds = timestamp.UtcDateTime.Ticks / TicksPerMicrosecond;
            return microseconds - UnixEpochMicroseconds;
        }
    }
}
