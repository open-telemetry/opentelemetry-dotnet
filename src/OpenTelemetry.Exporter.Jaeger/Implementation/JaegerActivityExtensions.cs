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
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal static class JaegerActivityExtensions
    {
        internal const string JaegerErrorFlagTagName = "error";

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

        public static JaegerSpan ToJaegerSpan(this Activity activity)
        {
            var jaegerTags = new TagEnumerationState
            {
                Tags = PooledList<JaegerTag>.Create(),
            };

            activity.EnumerateTags(ref jaegerTags);

            string peerServiceName = null;
            if (activity.Kind == ActivityKind.Client || activity.Kind == ActivityKind.Producer)
            {
                PeerServiceResolver.Resolve(ref jaegerTags, out peerServiceName, out bool addAsTag);

                if (peerServiceName != null && addAsTag)
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
                PooledList<JaegerTag>.Add(ref jaegerTags.Tags, new JaegerTag("otel.library.name", JaegerTagType.STRING, vStr: activitySource.Name));
                if (!string.IsNullOrEmpty(activitySource.Version))
                {
                    PooledList<JaegerTag>.Add(ref jaegerTags.Tags, new JaegerTag("otel.library.version", JaegerTagType.STRING, vStr: activitySource.Version));
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
                if (activity.ParentSpanId != default)
                {
                    parentSpanId = new Int128(activity.ParentSpanId);
                }
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
                logs: activity.ToJaegerLogs());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledList<JaegerSpanRef> ToJaegerSpanRefs(this Activity activity)
        {
            LinkEnumerationState references = default;

            activity.EnumerateLinks(ref references);

            return references.SpanRefs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PooledList<JaegerLog> ToJaegerLogs(this Activity activity)
        {
            EventEnumerationState logs = default;

            activity.EnumerateEvents(ref logs);

            return logs.Logs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JaegerLog ToJaegerLog(this ActivityEvent timedEvent)
        {
            var jaegerTags = new EventTagsEnumerationState
            {
                Tags = PooledList<JaegerTag>.Create(),
            };

            timedEvent.EnumerateTags(ref jaegerTags);

            if (!jaegerTags.HasEvent)
            {
                // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk_exporters/jaeger.md#events
                PooledList<JaegerTag>.Add(ref jaegerTags.Tags, new JaegerTag("event", JaegerTagType.STRING, vStr: timedEvent.Name));
            }

            // TODO: Use the same function as JaegerConversionExtensions or check that the perf here is acceptable.
            return new JaegerLog(timedEvent.Timestamp.ToEpochMicroseconds(), jaegerTags.Tags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ProcessJaegerTag(ref TagEnumerationState state, string key, JaegerTag jaegerTag)
        {
            if (jaegerTag.VStr != null)
            {
                PeerServiceResolver.InspectTag(ref state, key, jaegerTag.VStr);

                if (key == SpanAttributeConstants.StatusCodeKey)
                {
                    StatusCode? statusCode = StatusHelper.GetStatusCodeForTagValue(jaegerTag.VStr);
                    if (statusCode == StatusCode.Error)
                    {
                        // Error flag: https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk_exporters/jaeger.md#error-flag
                        PooledList<JaegerTag>.Add(ref state.Tags, new JaegerTag(JaegerErrorFlagTagName, JaegerTagType.BOOL, vBool: true));
                    }
                    else if (!statusCode.HasValue || statusCode == StatusCode.Unset)
                    {
                        // Unset Status is not sent: https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk_exporters/jaeger.md#status
                        return;
                    }

                    // Normalize status since it is user-driven.
                    jaegerTag = new JaegerTag(key, JaegerTagType.STRING, vStr: StatusHelper.GetTagValueForStatusCode(statusCode.Value));
                }
                else if (key == JaegerErrorFlagTagName)
                {
                    // Ignore `error` tag if it exists, it will be added based on StatusCode + StatusDescription.
                    return;
                }
            }
            else if (jaegerTag.VLong.HasValue)
            {
                PeerServiceResolver.InspectTag(ref state, key, jaegerTag.VLong.Value);
            }

            PooledList<JaegerTag>.Add(ref state.Tags, jaegerTag);
        }

        private struct TagEnumerationState : IActivityEnumerator<KeyValuePair<string, object>>, PeerServiceResolver.IPeerServiceState
        {
            public PooledList<JaegerTag> Tags;

            public string PeerService { get; set; }

            public int? PeerServicePriority { get; set; }

            public string HostName { get; set; }

            public string IpAddress { get; set; }

            public long Port { get; set; }

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

        private struct LinkEnumerationState : IActivityEnumerator<ActivityLink>
        {
            public bool Created;

            public PooledList<JaegerSpanRef> SpanRefs;

            public bool ForEach(ActivityLink activityLink)
            {
                if (!this.Created)
                {
                    this.SpanRefs = PooledList<JaegerSpanRef>.Create();
                    this.Created = true;
                }

                PooledList<JaegerSpanRef>.Add(ref this.SpanRefs, activityLink.ToJaegerSpanRef());

                return true;
            }
        }

        private struct EventEnumerationState : IActivityEnumerator<ActivityEvent>
        {
            public bool Created;

            public PooledList<JaegerLog> Logs;

            public bool ForEach(ActivityEvent activityEvent)
            {
                if (!this.Created)
                {
                    this.Logs = PooledList<JaegerLog>.Create();
                    this.Created = true;
                }

                PooledList<JaegerLog>.Add(ref this.Logs, activityEvent.ToJaegerLog());

                return true;
            }
        }

        private struct EventTagsEnumerationState : IActivityEnumerator<KeyValuePair<string, object>>
        {
            public PooledList<JaegerTag> Tags;

            public bool HasEvent;

            public bool ForEach(KeyValuePair<string, object> tag)
            {
                if (tag.Value is Array)
                {
                    ProcessJaegerTagArray(ref this.Tags, tag);
                }
                else if (tag.Value != null)
                {
                    PooledList<JaegerTag>.Add(ref this.Tags, tag.ToJaegerTag());
                }

                if (tag.Key == "event")
                {
                    this.HasEvent = true;
                }

                return true;
            }
        }
    }
}
