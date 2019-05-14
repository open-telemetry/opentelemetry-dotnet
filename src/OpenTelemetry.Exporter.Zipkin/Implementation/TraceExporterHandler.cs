// <copyright file="TraceExporterHandler.cs" company="OpenTelemetry Authors">
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
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using OpenTelemetry.Common;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Export;

    internal class TraceExporterHandler : IHandler
    {
        private const string StatusCode = "census.status_code";
        private const string StatusDescription = "census.status_description";
        private const long MillisPerSecond = 1000L;
        private const long NanosPerMillisecond = 1000 * 1000;
        private const long NanosPerSecond = NanosPerMillisecond * MillisPerSecond;
        private readonly ZipkinTraceExporterOptions options;
        private readonly ZipkinEndpoint localEndpoint;
        private readonly HttpClient httpClient;

        public TraceExporterHandler(ZipkinTraceExporterOptions options, HttpClient client)
        {
            this.options = options;
            this.localEndpoint = this.GetLocalZipkinEndpoint();
            this.httpClient = client ?? new HttpClient();
        }

        public async Task ExportAsync(IEnumerable<ISpanData> spanDataList)
        {
            List<ZipkinSpan> zipkinSpans = new List<ZipkinSpan>();

            foreach (var data in spanDataList)
            {
                var zipkinSpan = this.GenerateSpan(data, this.localEndpoint);
                zipkinSpans.Add(zipkinSpan);
            }

            await this.SendSpansAsync(zipkinSpans);
        }

        internal ZipkinSpan GenerateSpan(ISpanData spanData, ZipkinEndpoint localEndpoint)
        {
            ISpanContext context = spanData.Context;
            long startTimestamp = this.ToEpochMicroseconds(spanData.StartTimestamp);
            long endTimestamp = this.ToEpochMicroseconds(spanData.EndTimestamp);

            ZipkinSpan.Builder spanBuilder =
                ZipkinSpan.NewBuilder()
                    .TraceId(this.EncodeTraceId(context.TraceId))
                    .Id(this.EncodeSpanId(context.SpanId))
                    .Kind(this.ToSpanKind(spanData))
                    .Name(spanData.Name)
                    .Timestamp(this.ToEpochMicroseconds(spanData.StartTimestamp))
                    .Duration(endTimestamp - startTimestamp)
                    .LocalEndpoint(localEndpoint);

            if (spanData.ParentSpanId != null && spanData.ParentSpanId.IsValid)
            {
                spanBuilder.ParentId(this.EncodeSpanId(spanData.ParentSpanId));
            }

            foreach (var label in spanData.Attributes.AttributeMap)
            {
                spanBuilder.PutTag(label.Key, this.AttributeValueToString(label.Value));
            }

            Status status = spanData.Status;

            if (status != null)
            {
                spanBuilder.PutTag(StatusCode, status.CanonicalCode.ToString());

                if (status.Description != null)
                {
                    spanBuilder.PutTag(StatusDescription, status.Description);
                }
            }

            foreach (var annotation in spanData.Annotations.Events)
            {
                spanBuilder.AddAnnotation(this.ToEpochMicroseconds(annotation.Timestamp), annotation.Event.Description);
            }

            foreach (var networkEvent in spanData.MessageEvents.Events)
            {
                spanBuilder.AddAnnotation(this.ToEpochMicroseconds(networkEvent.Timestamp), networkEvent.Event.Type.ToString());
            }

            return spanBuilder.Build();
        }

        private long ToEpochMicroseconds(Timestamp timestamp)
        {
            long nanos = (timestamp.Seconds * NanosPerSecond) + timestamp.Nanos;
            long micros = nanos / 1000L;
            return micros;
        }

        private string AttributeValueToString(IAttributeValue attributeValue)
        {
            return attributeValue.Match(
                (arg) => { return arg; },
                (arg) => { return arg.ToString(); },
                (arg) => { return arg.ToString(); },
                (arg) => { return arg.ToString(); },
                (arg) => { return null; });
        }

        private string EncodeTraceId(ITraceId traceId)
        {
            var id = traceId.ToLowerBase16();

            if (id.Length > 16 && this.options.UseShortTraceIds)
            {
                id = id.Substring(id.Length - 16, 16);
            }

            return id;
        }

        private string EncodeSpanId(ISpanId spanId)
        {
            return spanId.ToLowerBase16();
        }

        private ZipkinSpanKind ToSpanKind(ISpanData spanData)
        {
            var zipkinSpanKind = ZipkinSpanKind.CLIENT;

            switch (spanData.Kind)
            {
                case SpanKind.Server:
                    zipkinSpanKind = ZipkinSpanKind.SERVER;
                    break;
                case SpanKind.Client:
                    if (spanData.HasRemoteParent.HasValue && spanData.HasRemoteParent.Value)
                    {
                        zipkinSpanKind = ZipkinSpanKind.SERVER;
                    }
                    else
                    {
                        zipkinSpanKind = ZipkinSpanKind.CLIENT;
                    }

                    break;
                case SpanKind.Consumer:
                    zipkinSpanKind = ZipkinSpanKind.CONSUMER;
                    break;
                case SpanKind.Producer:
                    if (spanData.HasRemoteParent.HasValue && spanData.HasRemoteParent.Value)
                    {
                        zipkinSpanKind = ZipkinSpanKind.CONSUMER;
                    }
                    else
                    {
                        zipkinSpanKind = ZipkinSpanKind.PRODUCER;
                    }

                    break;
                default:
                    break;
            }

            return zipkinSpanKind;
        }

        private async Task SendSpansAsync(IEnumerable<ZipkinSpan> spans)
        {
            try
            {
                var requestUri = this.options.Endpoint;
                var request = this.GetHttpRequestMessage(HttpMethod.Post, requestUri);
                request.Content = this.GetRequestContent(spans);
                await this.DoPost(this.httpClient, request);
            }
            catch (Exception)
            {
            }
        }

        private async Task DoPost(HttpClient client, HttpRequestMessage request)
        {
            try
            {
                using (HttpResponseMessage response = await client.SendAsync(request))
                {
                    if (response.StatusCode != HttpStatusCode.OK &&
                        response.StatusCode != HttpStatusCode.Accepted)
                    {
                        var statusCode = (int)response.StatusCode;
                    }

                    return;
                }
            }
            catch (Exception)
            {
            }
        }

        private HttpRequestMessage GetHttpRequestMessage(HttpMethod method, Uri requestUri)
        {
            var request = new HttpRequestMessage(method, requestUri);

            return request;
        }

        private HttpContent GetRequestContent(IEnumerable<ZipkinSpan> toSerialize)
        {
            try
            {
                string json = JsonConvert.SerializeObject(toSerialize);

                return new StringContent(json, Encoding.UTF8, "application/json");
            }
            catch (Exception)
            {
            }

            return new StringContent(string.Empty, Encoding.UTF8, "application/json");
        }

        private ZipkinEndpoint GetLocalZipkinEndpoint()
        {
            var result = new ZipkinEndpoint()
            {
                ServiceName = this.options.ServiceName,
            };

            string hostName = this.ResolveHostName();

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
