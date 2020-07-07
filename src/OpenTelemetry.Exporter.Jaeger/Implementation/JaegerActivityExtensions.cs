// <copyright file="JaegerActivityExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal static class JaegerActivityExtensions
    {
        private static readonly Dictionary<string, int> PeerServiceKeyResolutionDictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [SpanAttributeConstants.PeerServiceKey] = 0, // peer.service primary.
            ["net.peer.name"] = 1, // peer.service first alternative.
            ["peer.hostname"] = 2, // peer.service second alternative.
            ["peer.address"] = 2, // peer.service second alternative.
            ["http.host"] = 3, // peer.service for Http.
            ["db.instance"] = 4, // peer.service for Redis.
        };

        private static readonly DictionaryEnumerator<string, string, TagState>.ForEachDelegate ProcessActivityTagRef = ProcessActivityTag;
        private static readonly ListEnumerator<ActivityLink, PooledListState<JaegerSpanRef>>.ForEachDelegate ProcessActivityLinkRef = ProcessActivityLink;
        private static readonly ListEnumerator<ActivityEvent, PooledListState<JaegerLog>>.ForEachDelegate ProcessActivityEventRef = ProcessActivityEvent;
        private static readonly DictionaryEnumerator<string, object, PooledListState<JaegerTag>>.ForEachDelegate ProcessTagRef = ProcessTag;

        public static JaegerSpan ToJaegerSpan(this Activity activity)
        {
            var jaegerTags = new TagState
            {
                Tags = PooledList<JaegerTag>.Create(),
            };

            DictionaryEnumerator<string, string, TagState>.AllocationFreeForEach(
                activity.Tags,
                ref jaegerTags,
                ProcessActivityTagRef);

            string peerServiceName = null;
            if ((activity.Kind == ActivityKind.Client || activity.Kind == ActivityKind.Producer) && jaegerTags.PeerService != null)
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
            if (activity.Kind != ActivityKind.Internal)
            {
                string spanKind = null;

                if (activity.Kind == ActivityKind.Server)
                {
                    spanKind = "server";
                }
                else if (activity.Kind == ActivityKind.Client)
                {
                    spanKind = "client";
                }
                else if (activity.Kind == ActivityKind.Consumer)
                {
                    spanKind = "consumer";
                }
                else if (activity.Kind == ActivityKind.Producer)
                {
                    spanKind = "producer";
                }

                if (spanKind != null)
                {
                    PooledList<JaegerTag>.Add(ref jaegerTags.Tags, new JaegerTag("span.kind", JaegerTagType.STRING, vStr: spanKind));
                }
            }

            var activitySource = activity.Source;
            if (!string.IsNullOrEmpty(activitySource.Name))
            {
                PooledList<JaegerTag>.Add(ref jaegerTags.Tags, new JaegerTag("library.name", JaegerTagType.STRING, vStr: activitySource.Name));
                if (!string.IsNullOrEmpty(activitySource.Version))
                {
                    PooledList<JaegerTag>.Add(ref jaegerTags.Tags, new JaegerTag("library.version", JaegerTagType.STRING, vStr: activitySource.Version));
                }
            }

            var traceId = Int128.Empty;
            var spanId = Int128.Empty;
            var parentSpanId = Int128.Empty;

            if (activity.IdFormat == ActivityIdFormat.W3C)
            {
                // TODO: The check above should be enforced by the usage of the exporter. Perhaps enforce at higher-level.
                traceId = new Int128(activity.TraceId);
                spanId = new Int128(activity.SpanId);
                parentSpanId = new Int128(activity.ParentSpanId);
            }

            return new JaegerSpan(
                peerServiceName: peerServiceName,
                traceIdLow: traceId.Low,
                traceIdHigh: traceId.High,
                spanId: spanId.Low,
                parentSpanId: parentSpanId.Low,
                operationName: activity.DisplayName,
                flags: (activity.Context.TraceFlags & ActivityTraceFlags.Recorded) > 0 ? 0x1 : 0,
                startTime: ToEpochMicroseconds(activity.StartTimeUtc),
                duration: (long)activity.Duration.TotalMilliseconds * 1000,
                references: activity.Links.ToJaegerSpanRefs(),
                tags: jaegerTags.Tags,
                logs: activity.Events.ToJaegerLogs());
        }

        public static PooledList<JaegerSpanRef> ToJaegerSpanRefs(this IEnumerable<ActivityLink> links)
        {
            PooledListState<JaegerSpanRef> references = default;

            if (links == null)
            {
                return references.List;
            }

            ListEnumerator<ActivityLink, PooledListState<JaegerSpanRef>>.AllocationFreeForEach(
                links,
                ref references,
                ProcessActivityLinkRef);

            return references.List;
        }

        public static PooledList<JaegerLog> ToJaegerLogs(this IEnumerable<ActivityEvent> events)
        {
            PooledListState<JaegerLog> logs = default;

            if (events == null)
            {
                return logs.List;
            }

            ListEnumerator<ActivityEvent, PooledListState<JaegerLog>>.AllocationFreeForEach(
                events,
                ref logs,
                ProcessActivityEventRef);

            return logs.List;
        }

        public static JaegerLog ToJaegerLog(this ActivityEvent timedEvent)
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

            // TODO: Use the same function as JaegerConversionExtensions or check that the perf here is acceptable.
            return new JaegerLog(timedEvent.Timestamp.ToEpochMicroseconds(), tags.List);
        }

        public static JaegerSpanRef ToJaegerSpanRef(this in ActivityLink link)
        {
            var traceId = new Int128(link.Context.TraceId);
            var spanId = new Int128(link.Context.SpanId);

            return new JaegerSpanRef(JaegerSpanRefType.CHILD_OF, traceId.Low, traceId.High, spanId.Low);
        }

        public static long ToEpochMicroseconds(this DateTime utcDateTime)
        {
            const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
            const long UnixEpochTicks = 621355968000000000; // = DateTimeOffset.FromUnixTimeMilliseconds(0).Ticks
            const long UnixEpochMicroseconds = UnixEpochTicks / TicksPerMicrosecond;

            // Truncate sub-microsecond precision before offsetting by the Unix Epoch to avoid
            // the last digit being off by one for dates that result in negative Unix times
            long microseconds = utcDateTime.Ticks / TicksPerMicrosecond;
            return microseconds - UnixEpochMicroseconds;
        }

        private static bool ProcessActivityTag(ref TagState state, KeyValuePair<string, string> activityTag)
        {
            var jaegerTag = new JaegerTag(activityTag.Key, JaegerTagType.STRING, activityTag.Value);

            if (jaegerTag.VStr != null
                && PeerServiceKeyResolutionDictionary.TryGetValue(activityTag.Key, out int priority)
                && (state.PeerService == null || priority < state.PeerServicePriority))
            {
                state.PeerService = jaegerTag.VStr;
                state.PeerServicePriority = priority;
            }

            PooledList<JaegerTag>.Add(ref state.Tags, jaegerTag);

            return true;
        }

        private static bool ProcessActivityLink(ref PooledListState<JaegerSpanRef> state, ActivityLink link)
        {
            if (!state.Created)
            {
                state.List = PooledList<JaegerSpanRef>.Create();
                state.Created = true;
            }

            PooledList<JaegerSpanRef>.Add(ref state.List, link.ToJaegerSpanRef());

            return true;
        }

        private static bool ProcessActivityEvent(ref PooledListState<JaegerLog> state, ActivityEvent e)
        {
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
