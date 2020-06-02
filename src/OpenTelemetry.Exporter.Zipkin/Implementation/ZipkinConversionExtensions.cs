// <copyright file="ZipkinConversionExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.Zipkin.Implementation
{
    internal static class ZipkinConversionExtensions
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

        private static readonly DictionaryEnumerator<string, object, AttributeEnumerationState>.ForEachDelegate ProcessAttributesRef = ProcessAttributes;
        private static readonly DictionaryEnumerator<string, object, AttributeEnumerationState>.ForEachDelegate ProcessLibraryResourcesRef = ProcessLibraryResources;
        private static readonly ListEnumerator<Event, PooledList<ZipkinAnnotation>>.ForEachDelegate ProcessEventsRef = ProcessEvents;

        internal static ZipkinSpan ToZipkinSpan(this SpanData otelSpan, ZipkinEndpoint defaultLocalEndpoint, bool useShortTraceIds = false)
        {
            var context = otelSpan.Context;
            var startTimestamp = ToEpochMicroseconds(otelSpan.StartTimestamp);
            var endTimestamp = ToEpochMicroseconds(otelSpan.EndTimestamp);

            string parentId = null;
            if (otelSpan.ParentSpanId != default)
            {
                parentId = EncodeSpanId(otelSpan.ParentSpanId);
            }

            var attributeEnumerationState = new AttributeEnumerationState
            {
                Tags = PooledList<KeyValuePair<string, string>>.Create(),
            };

            DictionaryEnumerator<string, object, AttributeEnumerationState>.AllocationFreeForEach(otelSpan.Attributes, ref attributeEnumerationState, ProcessAttributesRef);
            DictionaryEnumerator<string, object, AttributeEnumerationState>.AllocationFreeForEach(otelSpan.LibraryResource.Attributes, ref attributeEnumerationState, ProcessLibraryResourcesRef);

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
            if ((otelSpan.Kind == SpanKind.Client || otelSpan.Kind == SpanKind.Producer) && attributeEnumerationState.RemoteEndpointServiceName != null)
            {
                remoteEndpoint = RemoteEndpointCache.GetOrAdd(attributeEnumerationState.RemoteEndpointServiceName, ZipkinEndpoint.Create);
            }

            var status = otelSpan.Status;

            if (status.IsValid)
            {
                PooledList<KeyValuePair<string, string>>.Add(ref attributeEnumerationState.Tags, new KeyValuePair<string, string>(SpanAttributeConstants.StatusCodeKey, SpanHelper.GetCachedCanonicalCodeString(status.CanonicalCode)));

                if (status.Description != null)
                {
                    PooledList<KeyValuePair<string, string>>.Add(ref attributeEnumerationState.Tags, new KeyValuePair<string, string>(SpanAttributeConstants.StatusDescriptionKey, status.Description));
                }
            }

            var annotations = PooledList<ZipkinAnnotation>.Create();
            ListEnumerator<Event, PooledList<ZipkinAnnotation>>.AllocationFreeForEach(otelSpan.Events, ref annotations, ProcessEventsRef);

            return new ZipkinSpan(
                EncodeTraceId(context.TraceId, useShortTraceIds),
                parentId,
                EncodeSpanId(context.SpanId),
                ToSpanKind(otelSpan),
                otelSpan.Name,
                ToEpochMicroseconds(otelSpan.StartTimestamp),
                duration: endTimestamp - startTimestamp,
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

        internal static long ToEpochMicroseconds(DateTimeOffset timestamp)
        {
            return timestamp.ToUnixTimeMilliseconds() * 1000;
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

        private static string ToSpanKind(SpanData otelSpan)
        {
            switch (otelSpan.Kind)
            {
                case SpanKind.Server:
                    return "SERVER";
                case SpanKind.Producer:
                    return "PRODUCER";
                case SpanKind.Consumer:
                    return "CONSUMER";
                default:
                    return "CLIENT";
            }
        }

        private static bool ProcessEvents(ref PooledList<ZipkinAnnotation> annotations, Event @event)
        {
            PooledList<ZipkinAnnotation>.Add(ref annotations, new ZipkinAnnotation(ToEpochMicroseconds(@event.Timestamp), @event.Name));
            return true;
        }

        private static bool ProcessAttributes(ref AttributeEnumerationState state, KeyValuePair<string, object> attribute)
        {
            string key = attribute.Key;
            if (!(attribute.Value is string strVal))
            {
                strVal = attribute.Value?.ToString();
            }

            if (strVal != null
                && RemoteEndpointServiceNameKeyResolutionDictionary.TryGetValue(key, out int priority)
                && (state.RemoteEndpointServiceName == null || priority < state.RemoteEndpointServiceNamePriority))
            {
                state.RemoteEndpointServiceName = strVal;
                state.RemoteEndpointServiceNamePriority = priority;
            }

            PooledList<KeyValuePair<string, string>>.Add(ref state.Tags, new KeyValuePair<string, string>(key, strVal));

            return true;
        }

        private static bool ProcessLibraryResources(ref AttributeEnumerationState state, KeyValuePair<string, object> label)
        {
            // See https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/data-resource-semantic-conventions.md

            string key = label.Key;
            object val = label.Value;
            string strVal = val as string;

            if (key == Resource.ServiceNameKey && strVal != null)
            {
                state.ServiceName = strVal;
            }
            else if (key == Resource.ServiceNamespaceKey && strVal != null)
            {
                state.ServiceNamespace = strVal;
            }
            else
            {
                PooledList<KeyValuePair<string, string>>.Add(ref state.Tags, new KeyValuePair<string, string>(key, strVal ?? val?.ToString()));
            }

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
