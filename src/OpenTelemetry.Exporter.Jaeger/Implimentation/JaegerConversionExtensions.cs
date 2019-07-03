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

namespace OpenTelemetry.Exporter.Jaeger.Implimentation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using OpenTelemetry.Common;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;

    public static class JaegerConversionExtensions
    {
        private const long MillisPerSecond = 1000L;
        private const long NanosPerMillisecond = 1000 * 1000;
        private const long NanosPerSecond = NanosPerMillisecond * MillisPerSecond;

        public static JaegerSpan ToJaegerSpan(this SpanData span)
        {
            var jaegerTags = new List<JaegerTag>();

            if (span?.Attributes?.AttributeMap != null)
            {
                jaegerTags.AddRange(span.Attributes.AttributeMap.Select(a => a.ToJaegerTag()));
            }

            var jaegerLogs = new List<JaegerLog>();

            if (span?.Events?.Events != null)
            {
                jaegerLogs.AddRange(span.Events.Events.Select(e => e.ToJaegerLog()));
            }

            var refs = new List<JaegerSpanRef>();

            if (span?.Links?.Links != null)
            {
                refs.AddRange(span.Links.Links.Select(l => l.ToJaegerSpanRef()).Where(l => l != null));
            }

            Int128? parentSpanId = null;

            if (span?.ParentSpanId?.Bytes != null)
            {
                parentSpanId = new Int128(span.ParentSpanId.Bytes);
            }

            var traceId = span?.Context?.TraceId?.Bytes == null ? Int128.Empty : new Int128(span.Context.TraceId.Bytes);
            var spanId = span?.Context?.SpanId?.Bytes == null ? Int128.Empty : new Int128(span.Context.SpanId.Bytes);

            return new JaegerSpan
            {
                TraceIdHigh = traceId.High,
                TraceIdLow = traceId.Low,
                SpanId = spanId.Low,
                ParentSpanId = parentSpanId?.Low ?? 0,
                OperationName = span.Name,
                References = refs.Count == 0 ? null : refs,
                Flags = span.Context.TraceOptions.IsSampled ? 0x1 : 0,
                StartTime = ToEpochMicroseconds(span.StartTimestamp),
                Duration = ToEpochMicroseconds(span.EndTimestamp) - ToEpochMicroseconds(span.StartTimestamp),
                JaegerTags = jaegerTags,
                Logs = jaegerLogs,
            };
        }

        public static JaegerTag ToJaegerTag(this KeyValuePair<string, IAttributeValue> attribute)
        {
            var ret = attribute.Value.Match(
                (s) => new JaegerTag { Key = attribute.Key, VType = JaegerTagType.STRING, VStr = s },
                (b) => new JaegerTag { Key = attribute.Key, VType = JaegerTagType.BOOL, VBool = b },
                (l) => new JaegerTag { Key = attribute.Key, VType = JaegerTagType.LONG, VLong = l },
                (d) => new JaegerTag { Key = attribute.Key, VType = JaegerTagType.DOUBLE, VDouble = d },
                (obj) => new JaegerTag { Key = attribute.Key, VType = JaegerTagType.STRING, VStr = obj.ToString() });

            return ret;
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
            // var spanRefType = ConvertLinkType(link.Context.Type);

            // if (spanRefType == null)
            // {
            //     return null;
            // }

            var traceId = link?.Context?.TraceId?.Bytes == null ? Int128.Empty : new Int128(link.Context.TraceId.Bytes);
            var spanId = link?.Context?.SpanId?.Bytes == null ? Int128.Empty : new Int128(link.Context.SpanId.Bytes);

            return new JaegerSpanRef
            {
                TraceIdHigh = traceId.High,
                TraceIdLow = traceId.Low,
                SpanId = spanId.Low,
                RefType = JaegerSpanRefType.CHILD_OF,
            };
        }

        // private static JaegerSpanRefType? ConvertLinkType(LinkType linkType)
        // {
        //     if (linkType == LinkType.ChildLinkedSpan)
        //     {
        //         return JaegerSpanRefType.CHILD_OF;
        //     }
             // if (linkType == LinkType.ParentLinkedSpan)
        //     {
        //         return JaegerSpanRefType.FOLLOWS_FROM;
        //     }
             // return null;
        // }

        private static long ToEpochMicroseconds(this Timestamp timestamp)
        {
            long nanos = (timestamp.Seconds * NanosPerSecond) + timestamp.Nanos;
            long micros = nanos / 1000L;
            return micros;
        }
    }
}
