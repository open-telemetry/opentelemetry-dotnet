// <copyright file="ZipkinActivityConversionExtensions.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Zipkin.Implementation
{
    internal static class ZipkinActivityConversionExtensions
    {
        internal const string ZipkinErrorFlagTagName = "error";
        private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        private const long UnixEpochTicks = 621355968000000000L; // = DateTimeOffset.FromUnixTimeMilliseconds(0).Ticks
        private const long UnixEpochMicroseconds = UnixEpochTicks / TicksPerMicrosecond;

#if !NET452
        private static readonly ConcurrentDictionary<(string, int), ZipkinEndpoint> RemoteEndpointCache = new ConcurrentDictionary<(string, int), ZipkinEndpoint>();
#else
        private static readonly ConcurrentDictionary<string, ZipkinEndpoint> RemoteEndpointCache = new ConcurrentDictionary<string, ZipkinEndpoint>();
#endif

        internal static ZipkinSpan ToZipkinSpan(this Activity activity, ZipkinEndpoint localEndpoint, bool useShortTraceIds = false)
        {
            var context = activity.Context;

            string parentId = activity.ParentSpanId == default ?
                null
                : EncodeSpanId(activity.ParentSpanId);

            var tagState = new TagEnumerationState
            {
                Tags = PooledList<KeyValuePair<string, object>>.Create(),
            };

            activity.EnumerateTags(ref tagState);

            var activitySource = activity.Source;
            if (!string.IsNullOrEmpty(activitySource.Name))
            {
                PooledList<KeyValuePair<string, object>>.Add(ref tagState.Tags, new KeyValuePair<string, object>("otel.library.name", activitySource.Name));
                if (!string.IsNullOrEmpty(activitySource.Version))
                {
                    PooledList<KeyValuePair<string, object>>.Add(ref tagState.Tags, new KeyValuePair<string, object>("otel.library.version", activitySource.Version));
                }
            }

            ZipkinEndpoint remoteEndpoint = null;
            if (activity.Kind == ActivityKind.Client || activity.Kind == ActivityKind.Producer)
            {
                PeerServiceResolver.Resolve(ref tagState, out string peerServiceName, out bool addAsTag);

                if (peerServiceName != null)
                {
#if !NET452
                    remoteEndpoint = RemoteEndpointCache.GetOrAdd((peerServiceName, default), ZipkinEndpoint.Create);
#else
                    remoteEndpoint = RemoteEndpointCache.GetOrAdd(peerServiceName, ZipkinEndpoint.Create);
#endif
                    if (addAsTag)
                    {
                        PooledList<KeyValuePair<string, object>>.Add(ref tagState.Tags, new KeyValuePair<string, object>(SemanticConventions.AttributePeerService, peerServiceName));
                    }
                }
            }

            if (tagState.StatusCode == StatusCode.Error)
            {
                // Error flag rule from https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk_exporters/zipkin.md#status
                PooledList<KeyValuePair<string, object>>.Add(
                    ref tagState.Tags,
                    new KeyValuePair<string, object>(
                        ZipkinErrorFlagTagName,
                        tagState.StatusDescription ?? string.Empty));
            }

            EventEnumerationState eventState = default;
            activity.EnumerateEvents(ref eventState);

            return new ZipkinSpan(
                EncodeTraceId(context.TraceId, useShortTraceIds),
                parentId,
                EncodeSpanId(context.SpanId),
                ToActivityKind(activity),
                activity.DisplayName,
                activity.StartTimeUtc.ToEpochMicroseconds(),
                duration: activity.Duration.ToEpochMicroseconds(),
                localEndpoint,
                remoteEndpoint,
                eventState.Annotations,
                tagState.Tags,
                null,
                null);
        }

        internal static string EncodeSpanId(ActivitySpanId spanId)
        {
            return spanId.ToHexString();
        }

        internal static long ToEpochMicroseconds(this DateTimeOffset dateTimeOffset)
        {
            // Truncate sub-microsecond precision before offsetting by the Unix Epoch to avoid
            // the last digit being off by one for dates that result in negative Unix times
            long microseconds = dateTimeOffset.Ticks / TicksPerMicrosecond;
            return microseconds - UnixEpochMicroseconds;
        }

        internal static long ToEpochMicroseconds(this TimeSpan timeSpan)
        {
            return timeSpan.Ticks / TicksPerMicrosecond;
        }

        internal static long ToEpochMicroseconds(this DateTime utcDateTime)
        {
            // Truncate sub-microsecond precision before offsetting by the Unix Epoch to avoid
            // the last digit being off by one for dates that result in negative Unix times
            long microseconds = utcDateTime.Ticks / TicksPerMicrosecond;
            return microseconds - UnixEpochMicroseconds;
        }

        private static string EncodeTraceId(ActivityTraceId traceId, bool useShortTraceIds)
        {
            var id = traceId.ToHexString();

            if (id.Length > 16 && useShortTraceIds)
            {
                id = id.Substring(id.Length - 16, 16);
            }

            return id;
        }

        private static string ToActivityKind(Activity activity)
        {
            return activity.Kind switch
            {
                ActivityKind.Server => "SERVER",
                ActivityKind.Producer => "PRODUCER",
                ActivityKind.Consumer => "CONSUMER",
                ActivityKind.Client => "CLIENT",
                _ => null,
            };
        }

        internal struct TagEnumerationState : IActivityEnumerator<KeyValuePair<string, object>>, PeerServiceResolver.IPeerServiceState
        {
            public PooledList<KeyValuePair<string, object>> Tags;

            public string PeerService { get; set; }

            public int? PeerServicePriority { get; set; }

            public string HostName { get; set; }

            public string IpAddress { get; set; }

            public long Port { get; set; }

            public StatusCode? StatusCode { get; set; }

            public string StatusDescription { get; set; }

            public bool ForEach(KeyValuePair<string, object> activityTag)
            {
                if (activityTag.Value == null)
                {
                    return true;
                }

                string key = activityTag.Key;

                if (activityTag.Value is string strVal)
                {
                    PeerServiceResolver.InspectTag(ref this, key, strVal);

                    if (key == SpanAttributeConstants.StatusCodeKey)
                    {
                        this.StatusCode = StatusHelper.GetStatusCodeForTagValue(strVal);

                        if (!this.StatusCode.HasValue || this.StatusCode == Trace.StatusCode.Unset)
                        {
                            // Unset Status is not sent: https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk_exporters/zipkin.md#status
                            return true;
                        }

                        // Normalize status since it is user-driven.
                        activityTag = new KeyValuePair<string, object>(key, StatusHelper.GetTagValueForStatusCode(this.StatusCode.Value));
                    }
                    else if (key == SpanAttributeConstants.StatusDescriptionKey)
                    {
                        // Description is sent as `error` but only if StatusCode is Error. See: https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk_exporters/zipkin.md#status
                        this.StatusDescription = strVal;
                        return true;
                    }
                    else if (key == ZipkinErrorFlagTagName)
                    {
                        // Ignore `error` tag if it exists, it will be added based on StatusCode + StatusDescription.
                        return true;
                    }
                }
                else if (activityTag.Value is int intVal && activityTag.Key == SemanticConventions.AttributeNetPeerPort)
                {
                    PeerServiceResolver.InspectTag(ref this, key, intVal);
                }

                PooledList<KeyValuePair<string, object>>.Add(ref this.Tags, activityTag);

                return true;
            }
        }

        private struct EventEnumerationState : IActivityEnumerator<ActivityEvent>
        {
            public bool Created;

            public PooledList<ZipkinAnnotation> Annotations;

            public bool ForEach(ActivityEvent activityEvent)
            {
                if (!this.Created)
                {
                    this.Annotations = PooledList<ZipkinAnnotation>.Create();
                    this.Created = true;
                }

                PooledList<ZipkinAnnotation>.Add(ref this.Annotations, new ZipkinAnnotation(activityEvent.Timestamp.ToEpochMicroseconds(), activityEvent.Name));

                return true;
            }
        }
    }
}
