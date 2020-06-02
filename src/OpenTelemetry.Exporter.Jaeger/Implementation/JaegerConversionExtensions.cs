// <copyright file="JaegerConversionExtensions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal static class JaegerConversionExtensions
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

        private static readonly Dictionary<string, int> PeerServiceKeyResolutionDictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [SpanAttributeConstants.PeerServiceKey] = 0, // peer.service primary.
            ["net.peer.name"] = 1, // peer.service first alternative.
            ["peer.hostname"] = 2, // peer.service second alternative.
            ["peer.address"] = 2, // peer.service second alternative.
            ["http.host"] = 3, // peer.service for Http.
            ["db.instance"] = 4, // peer.service for Redis.
        };

        private static readonly DictionaryEnumerator<string, object, TagState>.ForEachDelegate ProcessAttributeRef = ProcessAttribute;
        private static readonly DictionaryEnumerator<string, object, TagState>.ForEachDelegate ProcessLibraryAttributeRef = ProcessLibraryAttribute;
        private static readonly ListEnumerator<Link, PooledListState<JaegerSpanRef>>.ForEachDelegate ProcessLinkRef = ProcessLink;
        private static readonly ListEnumerator<Event, PooledListState<JaegerLog>>.ForEachDelegate ProcessEventRef = ProcessEvent;
        private static readonly DictionaryEnumerator<string, object, PooledListState<JaegerTag>>.ForEachDelegate ProcessTagRef = ProcessTag;

        public static JaegerSpan ToJaegerSpan(this SpanData span)
        {
            var jaegerTags = new TagState
            {
                Tags = PooledList<JaegerTag>.Create(),
            };

            DictionaryEnumerator<string, object, TagState>.AllocationFreeForEach(
                span.Attributes,
                ref jaegerTags,
                ProcessAttributeRef);

            string peerServiceName = null;
            if ((span.Kind == SpanKind.Client || span.Kind == SpanKind.Producer) && jaegerTags.PeerService != null)
            {
                // Send peer.service for remote calls.
                peerServiceName = jaegerTags.PeerService;

                // If priority = 0 that means peer.service was already included in tags.
                if (jaegerTags.PeerServicePriority > 0)
                {
                    PooledList<JaegerTag>.Add(ref jaegerTags.Tags, new JaegerTag(SpanAttributeConstants.PeerServiceKey, JaegerTagType.STRING, vStr: peerServiceName));
                }
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
                    PooledList<JaegerTag>.Add(ref jaegerTags.Tags, new JaegerTag("span.kind", JaegerTagType.STRING, vStr: spanKind));
                }
            }

            DictionaryEnumerator<string, object, TagState>.AllocationFreeForEach(
                span.LibraryResource?.Attributes ?? Array.Empty<KeyValuePair<string, object>>(),
                ref jaegerTags,
                ProcessLibraryAttributeRef);

            var status = span.Status;

            if (status.IsValid)
            {
                PooledList<JaegerTag>.Add(ref jaegerTags.Tags, new JaegerTag(SpanAttributeConstants.StatusCodeKey, JaegerTagType.STRING, vStr: SpanHelper.GetCachedCanonicalCodeString(status.CanonicalCode)));

                if (status.Description != null)
                {
                    PooledList<JaegerTag>.Add(ref jaegerTags.Tags, new JaegerTag(SpanAttributeConstants.StatusDescriptionKey, JaegerTagType.STRING, vStr: status.Description));
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
                peerServiceName: peerServiceName,
                traceIdLow: traceId.Low,
                traceIdHigh: traceId.High,
                spanId: spanId.Low,
                parentSpanId: parentSpanId.Low,
                operationName: span.Name,
                flags: (span.Context.TraceFlags & ActivityTraceFlags.Recorded) > 0 ? 0x1 : 0,
                startTime: ToEpochMicroseconds(span.StartTimestamp),
                duration: ToEpochMicroseconds(span.EndTimestamp) - ToEpochMicroseconds(span.StartTimestamp),
                references: span.Links.ToJaegerSpanRefs(),
                tags: jaegerTags.Tags,
                logs: span.Events.ToJaegerLogs());
        }

        public static PooledList<JaegerSpanRef> ToJaegerSpanRefs(this IEnumerable<Link> links)
        {
            PooledListState<JaegerSpanRef> references = default;

            if (links == null)
            {
                return references.List;
            }

            ListEnumerator<Link, PooledListState<JaegerSpanRef>>.AllocationFreeForEach(
                links,
                ref references,
                ProcessLinkRef);

            return references.List;
        }

        public static PooledList<JaegerLog> ToJaegerLogs(this IEnumerable<Event> events)
        {
            PooledListState<JaegerLog> logs = default;

            if (events == null)
            {
                return logs.List;
            }

            ListEnumerator<Event, PooledListState<JaegerLog>>.AllocationFreeForEach(
                events,
                ref logs,
                ProcessEventRef);

            return logs.List;
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
            var tags = new PooledListState<JaegerTag>
            {
                Created = true,
                List = PooledList<JaegerTag>.Create(),
            };

            DictionaryEnumerator<string, object, PooledListState<JaegerTag>>.AllocationFreeForEach(
                timedEvent.Attributes,
                ref tags,
                ProcessTagRef);

            // Matches what OpenTracing and OpenTelemetry defines as the event name.
            // https://github.com/opentracing/specification/blob/master/semantic_conventions.md#log-fields-table
            // https://github.com/open-telemetry/opentelemetry-specification/pull/397/files
            PooledList<JaegerTag>.Add(ref tags.List, new JaegerTag("message", JaegerTagType.STRING, vStr: timedEvent.Name));

            return new JaegerLog(timedEvent.Timestamp.ToEpochMicroseconds(), tags.List);
        }

        public static JaegerSpanRef ToJaegerSpanRef(this in Link link)
        {
            var traceId = new Int128(link.Context.TraceId);
            var spanId = new Int128(link.Context.SpanId);

            return new JaegerSpanRef(JaegerSpanRefType.CHILD_OF, traceId.Low, traceId.High, spanId.Low);
        }

        public static long ToEpochMicroseconds(this DateTimeOffset timestamp)
        {
            // Truncate sub-microsecond precision before offsetting by the Unix Epoch to avoid
            // the last digit being off by one for dates that result in negative Unix times
            long microseconds = timestamp.UtcDateTime.Ticks / TicksPerMicrosecond;
            return microseconds - UnixEpochMicroseconds;
        }

        private static bool ProcessAttribute(ref TagState state, KeyValuePair<string, object> label)
        {
            var tag = label.ToJaegerTag();

            if (tag.VStr != null
                && PeerServiceKeyResolutionDictionary.TryGetValue(label.Key, out int priority)
                && (state.PeerService == null || priority < state.PeerServicePriority))
            {
                state.PeerService = tag.VStr;
                state.PeerServicePriority = priority;
            }

            PooledList<JaegerTag>.Add(ref state.Tags, tag);

            return true;
        }

        private static bool ProcessLibraryAttribute(ref TagState state, KeyValuePair<string, object> label)
        {
            switch (label.Key)
            {
                case Resource.LibraryNameKey:
                    PooledList<JaegerTag>.Add(ref state.Tags, label.ToJaegerTag());
                    break;
                case Resource.LibraryVersionKey:
                    PooledList<JaegerTag>.Add(ref state.Tags, label.ToJaegerTag());
                    break;
            }

            return true;
        }

        private static bool ProcessLink(ref PooledListState<JaegerSpanRef> state, Link link)
        {
            if (!state.Created)
            {
                state.List = PooledList<JaegerSpanRef>.Create();
                state.Created = true;
            }

            PooledList<JaegerSpanRef>.Add(ref state.List, link.ToJaegerSpanRef());

            return true;
        }

        private static bool ProcessEvent(ref PooledListState<JaegerLog> state, Event e)
        {
            if (e == null)
            {
                return true;
            }

            if (!state.Created)
            {
                state.List = PooledList<JaegerLog>.Create();
                state.Created = true;
            }

            PooledList<JaegerLog>.Add(ref state.List, e.ToJaegerLog());
            return true;
        }

        private static bool ProcessTag(ref PooledListState<JaegerTag> state, KeyValuePair<string, object> attribute)
        {
            PooledList<JaegerTag>.Add(ref state.List, attribute.ToJaegerTag());
            return true;
        }

        private struct TagState
        {
            public PooledList<JaegerTag> Tags;

            public string PeerService;

            public int PeerServicePriority;
        }

        private struct PooledListState<T>
        {
            public bool Created;

            public PooledList<T> List;
        }
    }
}
