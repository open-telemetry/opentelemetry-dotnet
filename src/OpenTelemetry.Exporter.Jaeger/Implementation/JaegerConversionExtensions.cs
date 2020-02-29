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
using System.Linq;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal static class JaegerConversionExtensions
    {
        private const string StatusCode = "ot.status_code";
        private const string StatusDescription = "ot.status_description";

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

        private static readonly Dictionary<string, int> PeerServiceKeyResolutionDictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["peer.service"] = 0, // peer.service primary.
            ["net.peer.name"] = 1, // peer.service first alternative.
            ["peer.hostname"] = 2, // peer.service second alternative.
            ["peer.address"] = 2, // peer.service second alternative.
            ["http.host"] = 3, // peer.service for Http.
            ["db.instance"] = 4, // peer.service for Redis.
        };

        private static readonly Dictionary<CanonicalCode, string> CanonicalCodeDictionary = new Dictionary<CanonicalCode, string>();

        public static JaegerSpan ToJaegerSpan(this SpanData span)
        {
            var jaegerTags = PooledList<JaegerTag>.Create();

            Tuple<string, int> peerService = null;
            foreach (var label in span.Attributes)
            {
                var tag = label.ToJaegerTag();

                if (tag.VStr != null
                    && PeerServiceKeyResolutionDictionary.TryGetValue(label.Key, out int priority)
                    && (peerService == null || priority < peerService.Item2))
                {
                    peerService = new Tuple<string, int>(tag.VStr, priority);
                }

                PooledList<JaegerTag>.Add(ref jaegerTags, tag);
            }

            // Send peer.service for remote calls. If priority = 0 that means peer.service was already included.
            if ((span.Kind == SpanKind.Client || span.Kind == SpanKind.Producer)
                && peerService != null
                && peerService.Item2 > 0)
            {
                PooledList<JaegerTag>.Add(ref jaegerTags, new JaegerTag("peer.service", JaegerTagType.STRING, vStr: peerService.Item1));
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
                    PooledList<JaegerTag>.Add(ref jaegerTags, new JaegerTag("span.kind", JaegerTagType.STRING, vStr: spanKind));
                }
            }

            foreach (var label in span.LibraryResource?.Attributes ?? Array.Empty<KeyValuePair<string, object>>())
            {
                switch (label.Key)
                {
                    case Resource.LibraryNameKey:
                        PooledList<JaegerTag>.Add(ref jaegerTags, label.ToJaegerTag());
                        break;
                    case Resource.LibraryVersionKey:
                        PooledList<JaegerTag>.Add(ref jaegerTags, label.ToJaegerTag());
                        break;
                }
            }

            var status = span.Status;

            if (status.IsValid)
            {
                if (!CanonicalCodeDictionary.TryGetValue(status.CanonicalCode, out string statusCode))
                {
                    statusCode = status.CanonicalCode.ToString();
                    CanonicalCodeDictionary.Add(status.CanonicalCode, statusCode);
                }

                PooledList<JaegerTag>.Add(ref jaegerTags, new JaegerTag(StatusCode, JaegerTagType.STRING, vStr: statusCode));

                if (status.Description != null)
                {
                    PooledList<JaegerTag>.Add(ref jaegerTags, new JaegerTag(StatusDescription, JaegerTagType.STRING, vStr: status.Description));
                }
            }

            var traceId = Int128.Empty;
            var spanId = Int128.Empty;
            var parentSpanId = Int128.Empty;

            if (span.Context.IsValid)
            {
                traceId = new Int128(span.Context.TraceId);
                spanId = new Int128(span.Context.SpanId);
                parentSpanId = new Int128(span.ParentSpanId);
            }

            return new JaegerSpan(
                peerServiceName: peerService?.Item1,
                traceIdLow: traceId.Low,
                traceIdHigh: traceId.High,
                spanId: spanId.Low,
                parentSpanId: parentSpanId.Low,
                operationName: span.Name,
                flags: (span.Context.TraceOptions & ActivityTraceFlags.Recorded) > 0 ? 0x1 : 0,
                startTime: ToEpochMicroseconds(span.StartTimestamp),
                duration: ToEpochMicroseconds(span.EndTimestamp) - ToEpochMicroseconds(span.StartTimestamp),
                references: span.Links.ToJaegerSpanRefs(),
                tags: jaegerTags,
                logs: span.Events.ToJaegerLogs());
        }

        public static PooledList<JaegerSpanRef> ToJaegerSpanRefs(this IEnumerable<Link> links)
        {
            bool created = false;
            PooledList<JaegerSpanRef> references = default;

            foreach (var link in links ?? Array.Empty<Link>())
            {
                if (!created)
                {
                    references = PooledList<JaegerSpanRef>.Create();
                    created = true;
                }

                PooledList<JaegerSpanRef>.Add(ref references, link.ToJaegerSpanRef());
            }

            return references;
        }

        public static PooledList<JaegerLog> ToJaegerLogs(this IEnumerable<Event> events)
        {
            bool created = false;
            PooledList<JaegerLog> logs = default;

            foreach (var e in events ?? Array.Empty<Event>())
            {
                if (e == null)
                {
                    continue;
                }

                if (!created)
                {
                    logs = PooledList<JaegerLog>.Create();
                    created = true;
                }

                PooledList<JaegerLog>.Add(ref logs, e.ToJaegerLog());
            }

            return logs;
        }

        public static JaegerTag ToJaegerTag(this KeyValuePair<string, object> attribute)
        {
            switch (attribute.Value)
            {
                case string s:
                    return new JaegerTag(attribute.Key, JaegerTagType.STRING, vStr: s);
                case int i:
                    return new JaegerTag(attribute.Key, JaegerTagType.LONG, vLong: Convert.ToInt64(i));
                case long l:
                    return new JaegerTag(attribute.Key, JaegerTagType.LONG, vLong: l);
                case float f:
                    return new JaegerTag(attribute.Key, JaegerTagType.DOUBLE, vDouble: Convert.ToDouble(f));
                case double d:
                    return new JaegerTag(attribute.Key, JaegerTagType.DOUBLE, vDouble: d);
                case bool b:
                    return new JaegerTag(attribute.Key, JaegerTagType.BOOL, vBool: b);
            }

            return new JaegerTag(attribute.Key, JaegerTagType.STRING, vStr: attribute.Value.ToString());
        }

        public static JaegerLog ToJaegerLog(this Event timedEvent)
        {
            var tags = PooledList<JaegerTag>.Create();

            foreach (var attribute in timedEvent.Attributes)
            {
                PooledList<JaegerTag>.Add(ref tags, attribute.ToJaegerTag());
            }

            // Matches what OpenTracing and OpenTelemetry defines as the event name.
            // https://github.com/opentracing/specification/blob/master/semantic_conventions.md#log-fields-table
            // https://github.com/open-telemetry/opentelemetry-specification/pull/397/files
            PooledList<JaegerTag>.Add(ref tags, new JaegerTag("message", JaegerTagType.STRING, vStr: timedEvent.Name));

            return new JaegerLog(timedEvent.Timestamp.ToEpochMicroseconds(), tags);
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

            return new JaegerSpanRef(JaegerSpanRefType.CHILD_OF, traceId.Low, traceId.High, spanId.Low);
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
