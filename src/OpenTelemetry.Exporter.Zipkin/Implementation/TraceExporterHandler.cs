﻿// <copyright file="TraceExporterHandler.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Exporter.Zipkin.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;

    internal class TraceExporterHandler : IHandler
    {
        private const long MillisPerSecond = 1000L;
        private const long NanosPerMillisecond = 1000 * 1000;
        private const long NanosPerSecond = NanosPerMillisecond * MillisPerSecond;

        private static readonly string StatusCode = "ot.status_code";
        private static readonly string StatusDescription = "ot.status_description";

        private readonly ZipkinTraceExporterOptions options;
        private readonly ZipkinEndpoint localEndpoint;
        private readonly HttpClient httpClient;
        private readonly string serviceEndpoint;

        public TraceExporterHandler(ZipkinTraceExporterOptions options, HttpClient client)
        {
            this.options = options;
            this.localEndpoint = this.GetLocalZipkinEndpoint();
            this.httpClient = client ?? new HttpClient();
            this.serviceEndpoint = options.Endpoint?.ToString();
        }

        public async Task ExportAsync(IEnumerable<SpanData> spanDataList)
        {
            var zipkinSpans = new List<ZipkinSpan>();

            foreach (var data in spanDataList)
            {
                bool shouldExport = true;
                foreach (var label in data.Attributes.AttributeMap)
                {
                    if (label.Key == "http.url")
                    {
                        if (label.Value is string urlStr && urlStr == this.serviceEndpoint)
                        {
                            // do not track calls to Zipkin
                            shouldExport = false;
                        }

                        break;
                    }
                }

                if (shouldExport)
                {
                    var zipkinSpan = this.GenerateSpan(data, this.localEndpoint);
                    zipkinSpans.Add(zipkinSpan);
                }
            }

            if (zipkinSpans.Count == 0)
            {
                return;
            }

            try
            {
                await this.SendSpansAsync(zipkinSpans);
            }
            catch (Exception)
            {
                // Ignored
            }
        }

        internal ZipkinSpan GenerateSpan(SpanData spanData, ZipkinEndpoint localEndpoint)
        {
            var context = spanData.Context;
            var startTimestamp = this.ToEpochMicroseconds(spanData.StartTimestamp);
            var endTimestamp = this.ToEpochMicroseconds(spanData.EndTimestamp);

            var spanBuilder =
                ZipkinSpan.NewBuilder()
                    .ActivityTraceId(this.EncodeTraceId(context.TraceId))
                    .Id(this.EncodeSpanId(context.SpanId))
                    .Kind(this.ToSpanKind(spanData))
                    .Name(spanData.Name)
                    .Timestamp(this.ToEpochMicroseconds(spanData.StartTimestamp))
                    .Duration(endTimestamp - startTimestamp)
                    .LocalEndpoint(localEndpoint);

            if (spanData.ParentSpanId != default)
            {
                spanBuilder.ParentId(this.EncodeSpanId(spanData.ParentSpanId));
            }

            foreach (var label in spanData.Attributes.AttributeMap)
            {
                spanBuilder.PutTag(label.Key, label.Value.ToString());
            }

            var status = spanData.Status;

            if (status.IsValid)
            {
                spanBuilder.PutTag(StatusCode, status.CanonicalCode.ToString());

                if (status.Description != null)
                {
                    spanBuilder.PutTag(StatusDescription, status.Description);
                }
            }

            foreach (var annotation in spanData.Events.Events)
            {
                spanBuilder.AddAnnotation(this.ToEpochMicroseconds(annotation.Timestamp), annotation.Name);
            }

            return spanBuilder.Build();
        }

        private long ToEpochMicroseconds(DateTimeOffset timestamp)
        {
            return timestamp.ToUnixTimeMilliseconds() * 1000;
        }

        private string EncodeTraceId(ActivityTraceId traceId)
        {
            var id = traceId.ToHexString();

            if (id.Length > 16 && this.options.UseShortTraceIds)
            {
                id = id.Substring(id.Length - 16, 16);
            }

            return id;
        }

        private string EncodeSpanId(ActivitySpanId spanId)
        {
            return spanId.ToHexString();
        }

        private ZipkinSpanKind ToSpanKind(SpanData spanData)
        {
            if (spanData.Kind == SpanKind.Server)
            {
                return ZipkinSpanKind.SERVER;
            }
            else if (spanData.Kind == SpanKind.Client)
            {
                return ZipkinSpanKind.CLIENT;
            }

            return ZipkinSpanKind.CLIENT;
        }

        private Task SendSpansAsync(IEnumerable<ZipkinSpan> spans)
        {
            var requestUri = this.options.Endpoint;
            var request = this.GetHttpRequestMessage(HttpMethod.Post, requestUri);
            request.Content = this.GetRequestContent(spans);
            return this.DoPost(this.httpClient, request);
        }

        private async Task DoPost(HttpClient client, HttpRequestMessage request)
        {
            using (var response = await client.SendAsync(request))
            {
                if (response.StatusCode != HttpStatusCode.OK &&
                    response.StatusCode != HttpStatusCode.Accepted)
                {
                    var statusCode = (int)response.StatusCode;
                }
            }
        }

        private HttpRequestMessage GetHttpRequestMessage(HttpMethod method, Uri requestUri)
        {
            var request = new HttpRequestMessage(method, requestUri);

            return request;
        }

        private HttpContent GetRequestContent(IEnumerable<ZipkinSpan> toSerialize)
        {
            var content = string.Empty;
            try
            {
                content = JsonConvert.SerializeObject(toSerialize);
            }
            catch (Exception)
            {
                // Ignored
            }

            return new StringContent(content, Encoding.UTF8, "application/json");
        }

        private ZipkinEndpoint GetLocalZipkinEndpoint()
        {
            var result = new ZipkinEndpoint()
            {
                ServiceName = this.options.ServiceName,
            };

            var hostName = this.ResolveHostName();

            if (!string.IsNullOrEmpty(hostName))
            {
                result.Ipv4 = this.ResolveHostAddress(hostName, AddressFamily.InterNetwork);

                result.Ipv6 = this.ResolveHostAddress(hostName, AddressFamily.InterNetworkV6);
            }

            return result;
        }

        private string ResolveHostAddress(string hostName, AddressFamily family)
        {
            string result = null;

            try
            {
                var results = Dns.GetHostAddresses(hostName);

                if (results != null && results.Length > 0)
                {
                    foreach (var addr in results)
                    {
                        if (addr.AddressFamily.Equals(family))
                        {
                            var sanitizedAddress = new IPAddress(addr.GetAddressBytes()); // Construct address sans ScopeID
                            result = sanitizedAddress.ToString();

                            break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignore
            }

            return result;
        }

        private string ResolveHostName()
        {
            string result = null;

            try
            {
                result = Dns.GetHostName();

                if (!string.IsNullOrEmpty(result))
                {
                    var response = Dns.GetHostEntry(result);

                    if (response != null)
                    {
                        return response.HostName;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore
            }

            return result;
        }
    }
}
