// <copyright file="ZipkinConversionExtensions.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.Zipkin.Implementation
{
    internal static class ZipkinConversionExtensions
    {
        private const string StatusCode = "ot.status_code";
        private const string StatusDescription = "ot.status_description";

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

        internal static ZipkinSpan ToZipkinSpan(this SpanData otelSpan, ZipkinEndpoint defaultLocalEndpoint, bool useShortTraceIds = false)
        {
            var context = otelSpan.Context;
            var startTimestamp = ToEpochMicroseconds(otelSpan.StartTimestamp);
            var endTimestamp = ToEpochMicroseconds(otelSpan.EndTimestamp);

            var spanBuilder =
                ZipkinSpan.NewBuilder()
                    .TraceId(EncodeTraceId(context.TraceId, useShortTraceIds))
                    .Id(EncodeSpanId(context.SpanId))
                    .Kind(ToSpanKind(otelSpan))
                    .Name(otelSpan.Name)
                    .Timestamp(ToEpochMicroseconds(otelSpan.StartTimestamp))
                    .Duration(endTimestamp - startTimestamp);

            if (otelSpan.ParentSpanId != default)
            {
                spanBuilder.ParentId(EncodeSpanId(otelSpan.ParentSpanId));
            }

            Tuple<string, int> remoteEndpointServiceName = null;
            foreach (var label in otelSpan.Attributes)
            {
                string key = label.Key;
                string strVal = label.Value.ToString();

                if (strVal != null
                    && RemoteEndpointServiceNameKeyResolutionDictionary.TryGetValue(key, out int priority)
                    && (remoteEndpointServiceName == null || priority < remoteEndpointServiceName.Item2))
                {
                    remoteEndpointServiceName = new Tuple<string, int>(strVal, priority);
                }

                spanBuilder.PutTag(key, strVal);
            }

            // See https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/data-resource-semantic-conventions.md
            string serviceName = string.Empty;
            string serviceNamespace = string.Empty;
            foreach (var label in otelSpan.LibraryResource.Attributes)
            {
                string key = label.Key;
                object val = label.Value;
                string strVal = val as string;

                if (key == Resource.ServiceNameKey && strVal != null)
                {
                    serviceName = strVal;
                }
                else if (key == Resource.ServiceNamespaceKey && strVal != null)
                {
                    serviceNamespace = strVal;
                }
                else
                {
                    spanBuilder.PutTag(key, strVal ?? val?.ToString());
                }
            }

            if (serviceNamespace != string.Empty)
            {
                serviceName = serviceNamespace + "." + serviceName;
            }

            var endpoint = defaultLocalEndpoint;

            // override default service name
            if (serviceName != string.Empty)
            {
                endpoint = LocalEndpointCache.GetOrAdd(serviceName, _ => new ZipkinEndpoint()
                {
                    Ipv4 = defaultLocalEndpoint.Ipv4,
                    Ipv6 = defaultLocalEndpoint.Ipv6,
                    Port = defaultLocalEndpoint.Port,
                    ServiceName = serviceName,
                });
            }

            spanBuilder.LocalEndpoint(endpoint);

            if ((otelSpan.Kind == SpanKind.Client || otelSpan.Kind == SpanKind.Producer) && remoteEndpointServiceName != null)
            {
                spanBuilder.RemoteEndpoint(RemoteEndpointCache.GetOrAdd(remoteEndpointServiceName.Item1, _ => new ZipkinEndpoint
                {
                    ServiceName = remoteEndpointServiceName.Item1,
                }));
            }

            var status = otelSpan.Status;

            if (status.IsValid)
            {
                spanBuilder.PutTag(StatusCode, status.CanonicalCode.ToString());

                if (status.Description != null)
                {
                    spanBuilder.PutTag(StatusDescription, status.Description);
                }
            }

            foreach (var annotation in otelSpan.Events)
            {
                spanBuilder.AddAnnotation(ToEpochMicroseconds(annotation.Timestamp), annotation.Name);
            }

            return spanBuilder.Build();
        }

        private static long ToEpochMicroseconds(DateTimeOffset timestamp)
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

        private static string EncodeSpanId(ActivitySpanId spanId)
        {
            return spanId.ToHexString();
        }

        private static ZipkinSpanKind ToSpanKind(SpanData otelSpan)
        {
            if (otelSpan.Kind == SpanKind.Server)
            {
                return ZipkinSpanKind.SERVER;
            }
            else if (otelSpan.Kind == SpanKind.Client)
            {
                return ZipkinSpanKind.CLIENT;
            }

            return ZipkinSpanKind.CLIENT;
        }
    }
}
