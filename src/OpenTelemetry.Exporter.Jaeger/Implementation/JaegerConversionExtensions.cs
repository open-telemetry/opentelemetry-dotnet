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

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;

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

        public static JaegerSpan ToJaegerSpan(this SpanData span)
        {
            IEnumerable<JaegerTag> jaegerTags = null;

            if (span?.Attributes?.AttributeMap is IEnumerable<KeyValuePair<string, object>> attributeMap)
            {
                jaegerTags = attributeMap.Select(a => a.ToJaegerTag()).AsEnumerable();
            }

            IEnumerable<JaegerLog> jaegerLogs = null;

            if (span?.Events?.Events != null)
            {
                jaegerLogs = span.Events.Events.Select(e => e.ToJaegerLog()).AsEnumerable();
            }

            IEnumerable<JaegerSpanRef> refs = null;

            if (span?.Links?.Links != null)
            {
                refs = span.Links.Links.Select(l => l.ToJaegerSpanRef()).Where(l => l != null).AsEnumerable();
            }

            var parentSpanId = new Int128(span.ParentSpanId);

            var traceId = span?.Context?.TraceId == null ? Int128.Empty : new Int128(span.Context.TraceId);
            var spanId = span?.Context?.SpanId == null ? Int128.Empty : new Int128(span.Context.SpanId);

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

        public static JaegerLog ToJaegerLog(this ITimedEvent<IEvent> timedEvent)
        {
            var tags = timedEvent.Event.Attributes.Select(a => a.ToJaegerTag()).ToList();
            tags.Add(new JaegerTag { Key = "description", VType = JaegerTagType.STRING, VStr = timedEvent.Event.Name });

            return new JaegerLog
            {
                Timestamp = timedEvent.Timestamp.ToEpochMicroseconds(),
                Fields = tags,
            };
        }

        public static JaegerSpanRef ToJaegerSpanRef(this ILink link)
        {
            var traceId = link?.Context?.TraceId == null ? Int128.Empty : new Int128(link.Context.TraceId);
            var spanId = link?.Context?.SpanId == null ? Int128.Empty : new Int128(link.Context.SpanId);

            return new JaegerSpanRef
            {
                TraceIdHigh = traceId.High,
                TraceIdLow = traceId.Low,
                SpanId = spanId.Low,
                RefType = JaegerSpanRefType.CHILD_OF,
            };
        }

        public static long ToEpochMicroseconds(this DateTime timestamp)
        {
            // Truncate sub-microsecond precision before offsetting by the Unix Epoch to avoid
            // the last digit being off by one for dates that result in negative Unix times
            long microseconds = timestamp.Ticks / TicksPerMicrosecond;
            return microseconds - UnixEpochMicroseconds;
        }
    }
}
