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
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Zipkin.Implementation
{
    internal static class ZipkinActivityConversionExtensions
    {
        private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        private const long UnixEpochTicks = 621355968000000000L; // = DateTimeOffset.FromUnixTimeMilliseconds(0).Ticks
        private const long UnixEpochMicroseconds = UnixEpochTicks / TicksPerMicrosecond;

        private static readonly Dictionary<string, int> RemoteEndpointServiceNameKeyResolutionDictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [SemanticConventions.AttributePeerService] = 0, // priority 0 (highest).
            [SemanticConventions.AttributeNetPeerName] = 1,
            [SemanticConventions.AttributeNetPeerIp] = 2,
            ["peer.hostname"] = 2,
            ["peer.address"] = 2,
            [SemanticConventions.AttributeHttpHost] = 3, // RemoteEndpoint.ServiceName for Http.
            [SemanticConventions.AttributeDbInstance] = 3, // RemoteEndpoint.ServiceName for Redis.
        };

        private static readonly string InvalidSpanId = default(ActivitySpanId).ToHexString();

        private static readonly ConcurrentDictionary<string, ZipkinEndpoint> LocalEndpointCache = new ConcurrentDictionary<string, ZipkinEndpoint>();
        private static readonly ConcurrentDictionary<string, ZipkinEndpoint> RemoteEndpointCache = new ConcurrentDictionary<string, ZipkinEndpoint>();

        private static readonly DictionaryEnumerator<string, object, AttributeEnumerationState>.ForEachDelegate ProcessTagsRef = ProcessTags;
        private static readonly ListEnumerator<ActivityEvent, PooledList<ZipkinAnnotation>>.ForEachDelegate ProcessActivityEventsRef = ProcessActivityEvents;

        internal static ZipkinSpan ToZipkinSpan(this Activity activity, ZipkinEndpoint defaultLocalEndpoint, bool useShortTraceIds = false)
        {
            var context = activity.Context;

            string parentId = EncodeSpanId(activity.ParentSpanId);
            if (string.Equals(parentId, InvalidSpanId, StringComparison.Ordinal))
            {
                parentId = null;
            }

            var attributeEnumerationState = new AttributeEnumerationState
            {
                Tags = PooledList<KeyValuePair<string, object>>.Create(),
            };

            DictionaryEnumerator<string, object, AttributeEnumerationState>.AllocationFreeForEach(activity.TagObjects, ref attributeEnumerationState, ProcessTagsRef);

            var activitySource = activity.Source;
            if (!string.IsNullOrEmpty(activitySource.Name))
            {
                PooledList<KeyValuePair<string, object>>.Add(ref attributeEnumerationState.Tags, new KeyValuePair<string, object>("library.name", activitySource.Name));
                if (!string.IsNullOrEmpty(activitySource.Version))
                {
                    PooledList<KeyValuePair<string, object>>.Add(ref attributeEnumerationState.Tags, new KeyValuePair<string, object>("library.version", activitySource.Version));
                }
            }

            var localEndpoint = defaultLocalEndpoint;

            var serviceName = attributeEnumerationState.ServiceName;

            // override default service name
            if (!string.IsNullOrWhiteSpace(serviceName))
            {
                if (!string.IsNullOrWhiteSpace(attributeEnumerationState.ServiceNamespace))
                {
                    serviceName = attributeEnumerationState.ServiceNamespace + "." + serviceName;
                }

                if (!LocalEndpointCache.TryGetValue(serviceName, out localEndpoint))
                {
                    localEndpoint = defaultLocalEndpoint.Clone(serviceName);
                    LocalEndpointCache.TryAdd(serviceName, localEndpoint);
                }
            }

            ZipkinEndpoint remoteEndpoint = null;
            if ((activity.Kind == ActivityKind.Client || activity.Kind == ActivityKind.Producer) && attributeEnumerationState.RemoteEndpointServiceName != null)
            {
                remoteEndpoint = RemoteEndpointCache.GetOrAdd(attributeEnumerationState.RemoteEndpointServiceName, ZipkinEndpoint.Create);
            }

            var annotations = PooledList<ZipkinAnnotation>.Create();
            ListEnumerator<ActivityEvent, PooledList<ZipkinAnnotation>>.AllocationFreeForEach(activity.Events, ref annotations, ProcessActivityEventsRef);

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
                annotations,
                attributeEnumerationState.Tags,
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

        internal static bool ProcessTags(ref AttributeEnumerationState state, KeyValuePair<string, object> attribute)
        {
            if (attribute.Value == null)
            {
                return true;
            }

            if (attribute.Value is string strVal)
            {
                string key = attribute.Key;
                if (RemoteEndpointServiceNameKeyResolutionDictionary.TryGetValue(key, out int priority)
                    && (state.RemoteEndpointServiceName == null || priority < state.RemoteEndpointServiceNamePriority))
                {
                    state.RemoteEndpointServiceName = strVal;
                    state.RemoteEndpointServiceNamePriority = priority;
                }
                else if (key == Resource.ServiceNameKey)
                {
                    state.ServiceName = strVal;
                }
                else if (key == Resource.ServiceNamespaceKey)
                {
                    state.ServiceNamespace = strVal;
                }
                else
                {
                    PooledList<KeyValuePair<string, object>>.Add(ref state.Tags, new KeyValuePair<string, object>(key, strVal));
                }
            }
            else
            {
                PooledList<KeyValuePair<string, object>>.Add(ref state.Tags, attribute);
            }

            return true;
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

        private static bool ProcessActivityEvents(ref PooledList<ZipkinAnnotation> annotations, ActivityEvent @event)
        {
            PooledList<ZipkinAnnotation>.Add(ref annotations, new ZipkinAnnotation(@event.Timestamp.ToEpochMicroseconds(), @event.Name));
            return true;
        }

        internal struct AttributeEnumerationState
        {
            public PooledList<KeyValuePair<string, object>> Tags;

            public string RemoteEndpointServiceName;

            public int RemoteEndpointServiceNamePriority;

            public string ServiceName;

            public string ServiceNamespace;
        }
    }
}
