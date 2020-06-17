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
        private static readonly Dictionary<string, int> RemoteEndpointServiceNameKeyResolutionDictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [SpanAttributeConstants.PeerServiceKey] = 0, // RemoteEndpoint.ServiceName primary.
            ["net.peer.name"] = 1, // RemoteEndpoint.ServiceName first alternative.
            ["peer.hostname"] = 2, // RemoteEndpoint.ServiceName second alternative.
            ["peer.address"] = 2, // RemoteEndpoint.ServiceName second alternative.
            ["http.host"] = 3, // RemoteEndpoint.ServiceName for Http.
            ["db.instance"] = 4, // RemoteEndpoint.ServiceName for Redis.
        };

        private static readonly ConcurrentDictionary<string, ZipkinEndpoint> LocalEndpointCache = new ConcurrentDictionary<string, ZipkinEndpoint>();
        private static readonly ConcurrentDictionary<string, ZipkinEndpoint> RemoteEndpointCache = new ConcurrentDictionary<string, ZipkinEndpoint>();

        private static readonly DictionaryEnumerator<string, string, AttributeEnumerationState>.ForEachDelegate ProcessTagsRef = ProcessTags;
        private static readonly ListEnumerator<ActivityEvent, PooledList<ZipkinAnnotation>>.ForEachDelegate ProcessActivityEventsRef = ProcessActivityEvents;

        internal static ZipkinSpan ToZipkinSpan(this Activity activity, ZipkinEndpoint defaultLocalEndpoint, bool useShortTraceIds = false)
        {
            var context = activity.Context;
            var startTimestamp = activity.StartTimeUtc.ToEpochMicroseconds();

            string parentId = null;
            if (activity.ParentSpanId != default)
            {
                parentId = EncodeSpanId(activity.ParentSpanId);
            }

            var attributeEnumerationState = new AttributeEnumerationState
            {
                Tags = PooledList<KeyValuePair<string, string>>.Create(),
            };

            DictionaryEnumerator<string, string, AttributeEnumerationState>.AllocationFreeForEach(activity.Tags, ref attributeEnumerationState, ProcessTagsRef);

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
                activity.OperationName,
                activity.StartTimeUtc.ToEpochMicroseconds(),
                duration: (long)activity.Duration.TotalMilliseconds * 1000, // duration in Microseconds
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

        internal static long ToEpochMicroseconds(this DateTimeOffset timestamp)
        {
            return timestamp.ToUnixTimeMilliseconds() * 1000;
        }

        internal static long ToEpochMicroseconds(this DateTime utcDateTime)
        {
            const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
            const long UnixEpochTicks = 621355968000000000; // = DateTimeOffset.FromUnixTimeMilliseconds(0).Ticks
            const long UnixEpochMicroseconds = UnixEpochTicks / TicksPerMicrosecond;

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
            switch (activity.Kind)
            {
                case ActivityKind.Server:
                    return "SERVER";
                case ActivityKind.Producer:
                    return "PRODUCER";
                case ActivityKind.Consumer:
                    return "CONSUMER";
                default:
                    return "CLIENT";
            }
        }

        private static bool ProcessActivityEvents(ref PooledList<ZipkinAnnotation> annotations, ActivityEvent @event)
        {
            PooledList<ZipkinAnnotation>.Add(ref annotations, new ZipkinAnnotation(@event.Timestamp.ToEpochMicroseconds(), @event.Name));
            return true;
        }

        private static bool ProcessTags(ref AttributeEnumerationState state, KeyValuePair<string, string> attribute)
        {
            string key = attribute.Key;
            string strVal = attribute.Value?.ToString();

            if (strVal != null)
            {
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
            }

            PooledList<KeyValuePair<string, string>>.Add(ref state.Tags, new KeyValuePair<string, string>(key, strVal));

            return true;
        }

        private struct AttributeEnumerationState
        {
            public PooledList<KeyValuePair<string, string>> Tags;

            public string RemoteEndpointServiceName;

            public int RemoteEndpointServiceNamePriority;

            public string ServiceName;

            public string ServiceNamespace;
        }
    }
}
