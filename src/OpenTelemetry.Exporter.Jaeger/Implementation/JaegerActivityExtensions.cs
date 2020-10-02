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
            [SemanticConventions.AttributePeerService] = 0, // priority 0 (highest).
            ["peer.hostname"] = 1,
            ["peer.address"] = 1,
            [SemanticConventions.AttributeHttpHost] = 2, // peer.service for Http.
            [SemanticConventions.AttributeDbInstance] = 2, // peer.service for Redis.
        };

        private static readonly ListEnumerator<ActivityEvent, PooledListState<JaegerLog>>.ForEachDelegate ProcessActivityEventRef = ProcessActivityEvent;
        private static readonly DictionaryEnumerator<string, object, PooledListState<JaegerTag>>.ForEachDelegate ProcessTagRef = ProcessTag;

        public static JaegerSpan ToJaegerSpan(this Activity activity)
        {
            var jaegerTags = new TagState
            {
                Tags = PooledList<JaegerTag>.Create(),
            };

            activity.EnumerateTagValues(ref jaegerTags);

            string peerServiceName = null;
            if (activity.Kind == ActivityKind.Client || activity.Kind == ActivityKind.Producer)
            {
                // If priority = 0 that means peer.service may have already been included in tags
                var addPeerServiceTag = jaegerTags.PeerServicePriority > 0;

                var hostNameOrIpAddress = jaegerTags.HostName ?? jaegerTags.IpAddress;

                // peer.service has not already been included, but net.peer.name/ip and optionally net.peer.port are present
                if ((jaegerTags.PeerService == null || addPeerServiceTag)
                    && hostNameOrIpAddress != null)
                {
                    peerServiceName = jaegerTags.Port == default
                        ? hostNameOrIpAddress
                        : $"{hostNameOrIpAddress}:{jaegerTags.Port}";

                    // Add the peer.service tag
                    addPeerServiceTag = true;
                }

                if (peerServiceName == null && jaegerTags.PeerService != null)
                {
                    peerServiceName = jaegerTags.PeerService;
                }

                if (peerServiceName != null && addPeerServiceTag)
                {
                    PooledList<JaegerTag>.Add(ref jaegerTags.Tags, new JaegerTag(SemanticConventions.AttributePeerService, JaegerTagType.STRING, vStr: peerServiceName));
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
                references: activity.ToJaegerSpanRefs(),
                tags: jaegerTags.Tags,
                logs: activity.Events.ToJaegerLogs());
        }

        public static PooledList<JaegerSpanRef> ToJaegerSpanRefs(this Activity activity)
        {
            LinkState references = default;

            activity.EnumerateLinks(ref references);

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
                timedEvent.Tags,
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

            // Assume FOLLOWS_FROM for links, mirrored from Java: https://github.com/open-telemetry/opentelemetry-java/pull/481#discussion_r312577862
            var refType = JaegerSpanRefType.FOLLOWS_FROM;

            return new JaegerSpanRef(refType, traceId.Low, traceId.High, spanId.Low);
        }

        public static JaegerTag ToJaegerTag(this KeyValuePair<string, object> attribute)
        {
            return attribute.Value switch
            {
                string s => new JaegerTag(attribute.Key, JaegerTagType.STRING, vStr: s),
                int i => new JaegerTag(attribute.Key, JaegerTagType.LONG, vLong: Convert.ToInt64(i)),
                long l => new JaegerTag(attribute.Key, JaegerTagType.LONG, vLong: l),
                float f => new JaegerTag(attribute.Key, JaegerTagType.DOUBLE, vDouble: Convert.ToDouble(f)),
                double d => new JaegerTag(attribute.Key, JaegerTagType.DOUBLE, vDouble: d),
                bool b => new JaegerTag(attribute.Key, JaegerTagType.BOOL, vBool: b),
                _ => new JaegerTag(attribute.Key, JaegerTagType.STRING, vStr: attribute.Value.ToString()),
            };
        }

        public static long ToEpochMicroseconds(this DateTime utcDateTime)
        {
            // Truncate sub-microsecond precision before offsetting by the Unix Epoch to avoid
            // the last digit being off by one for dates that result in negative Unix times
            long microseconds = utcDateTime.Ticks / TicksPerMicrosecond;
            return microseconds - UnixEpochMicroseconds;
        }

        public static long ToEpochMicroseconds(this DateTimeOffset timestamp)
        {
            // Truncate sub-microsecond precision before offsetting by the Unix Epoch to avoid
            // the last digit being off by one for dates that result in negative Unix times
            long microseconds = timestamp.UtcDateTime.Ticks / TicksPerMicrosecond;
            return microseconds - UnixEpochMicroseconds;
        }

        private static void ProcessJaegerTagArray(ref PooledList<JaegerTag> tags, KeyValuePair<string, object> activityTag)
        {
            if (activityTag.Value is int[] intArray)
            {
                foreach (var item in intArray)
                {
                    JaegerTag jaegerTag = new JaegerTag(activityTag.Key, JaegerTagType.LONG, vLong: Convert.ToInt64(item));
                    PooledList<JaegerTag>.Add(ref tags, jaegerTag);
                }
            }
            else if (activityTag.Value is string[] stringArray)
            {
                foreach (var item in stringArray)
                {
                    JaegerTag jaegerTag = new JaegerTag(activityTag.Key, JaegerTagType.STRING, vStr: item);
                    PooledList<JaegerTag>.Add(ref tags, jaegerTag);
                }
            }
            else if (activityTag.Value is bool[] boolArray)
            {
                foreach (var item in boolArray)
                {
                    JaegerTag jaegerTag = new JaegerTag(activityTag.Key, JaegerTagType.BOOL, vBool: item);
                    PooledList<JaegerTag>.Add(ref tags, jaegerTag);
                }
            }
            else if (activityTag.Value is double[] doubleArray)
            {
                foreach (var item in doubleArray)
                {
                    JaegerTag jaegerTag = new JaegerTag(activityTag.Key, JaegerTagType.DOUBLE, vDouble: item);
                    PooledList<JaegerTag>.Add(ref tags, jaegerTag);
                }
            }
        }

        private static void ProcessJaegerTag(ref TagState state, string key, JaegerTag jaegerTag)
        {
            if (jaegerTag.VStr != null)
            {
                if (PeerServiceKeyResolutionDictionary.TryGetValue(key, out int priority)
                    && (state.PeerService == null || priority < state.PeerServicePriority))
                {
                    state.PeerService = jaegerTag.VStr;
                    state.PeerServicePriority = priority;
                }
                else if (key == SemanticConventions.AttributeNetPeerName)
                {
                    state.HostName = jaegerTag.VStr;
                }
                else if (key == SemanticConventions.AttributeNetPeerIp)
                {
                    state.IpAddress = jaegerTag.VStr;
                }
                else if (key == SemanticConventions.AttributeNetPeerPort && long.TryParse(jaegerTag.VStr, out var port))
                {
                    state.Port = port;
                }
            }
            else if (jaegerTag.VLong.HasValue && key == SemanticConventions.AttributeNetPeerPort)
            {
                state.Port = jaegerTag.VLong.Value;
            }

            PooledList<JaegerTag>.Add(ref state.Tags, jaegerTag);
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
            if (attribute.Value is Array)
            {
                ProcessJaegerTagArray(ref state.List, attribute);
            }
            else if (attribute.Value != null)
            {
                PooledList<JaegerTag>.Add(ref state.List, attribute.ToJaegerTag());
            }

            return true;
        }

        private struct TagState : IActivityEnumerator<KeyValuePair<string, object>>
        {
            public PooledList<JaegerTag> Tags;

            public string PeerService;

            public int PeerServicePriority;

            public string HostName;

            public string IpAddress;

            public long Port;

            public bool ForEach(KeyValuePair<string, object> activityTag)
            {
                if (activityTag.Value is Array)
                {
                    ProcessJaegerTagArray(ref this.Tags, activityTag);
                }
                else if (activityTag.Value != null)
                {
                    ProcessJaegerTag(ref this, activityTag.Key, activityTag.ToJaegerTag());
                }

                return true;
            }
        }

        private struct LinkState : IActivityEnumerator<ActivityLink>
        {
            public bool Created;

            public PooledList<JaegerSpanRef> List;

            public bool ForEach(ActivityLink activityLink)
            {
                if (!this.Created)
                {
                    this.List = PooledList<JaegerSpanRef>.Create();
                    this.Created = true;
                }

                PooledList<JaegerSpanRef>.Add(ref this.List, activityLink.ToJaegerSpanRef());

                return true;
            }
        }

        private struct PooledListState<T>
        {
            public bool Created;

            public PooledList<T> List;
        }
    }
}
