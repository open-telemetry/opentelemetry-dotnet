// <copyright file="ZipkinTraceExporter.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Zipkin.Implementation;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.Zipkin
{
    /// <summary>
    /// Zipkin exporter.
    /// </summary>
    public class ZipkinTraceExporter : SpanExporter
    {
        private const long MillisPerSecond = 1000L;
        private const long NanosPerMillisecond = 1000 * 1000;
        private const long NanosPerSecond = NanosPerMillisecond * MillisPerSecond;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        private readonly ZipkinTraceExporterOptions options;
        private readonly ZipkinEndpoint localEndpoint;
        private readonly HttpClient httpClient;
        private readonly string serviceEndpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZipkinTraceExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options.</param>
        /// <param name="client">Http client to use to upload telemetry.</param>
        public ZipkinTraceExporter(ZipkinTraceExporterOptions options, HttpClient client = null)
        {
            this.options = options;
            this.localEndpoint = this.GetLocalZipkinEndpoint();
            this.httpClient = client ?? new HttpClient();
            this.serviceEndpoint = options.Endpoint?.ToString();
        }

        /// <inheritdoc/>
        public override async Task<ExportResult> ExportAsync(IEnumerable<SpanData> otelSpanList, CancellationToken cancellationToken)
        {
            var zipkinSpans = new List<ZipkinSpan>();

            foreach (var data in otelSpanList)
            {
                bool shouldExport = true;
                foreach (var label in data.Attributes)
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
                    var zipkinSpan = data.ToZipkinSpan(this.localEndpoint, this.options.UseShortTraceIds);
                    zipkinSpans.Add(zipkinSpan);
                }
            }

            if (zipkinSpans.Count == 0)
            {
                return ExportResult.Success;
            }

            try
            {
                await this.SendSpansAsync(zipkinSpans, cancellationToken);
                return ExportResult.Success;
            }
            catch (Exception)
            {
                // TODO distinguish retryable exceptions
                return ExportResult.FailedNotRetryable;
            }
        }

        /// <inheritdoc/>
        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private Task SendSpansAsync(IEnumerable<ZipkinSpan> spans, CancellationToken cancellationToken)
        {
            var requestUri = this.options.Endpoint;
            var request = this.GetHttpRequestMessage(HttpMethod.Post, requestUri);
            request.Content = this.GetRequestContent(spans);

            // avoid cancelling here: this is no return point: if we reached this point
            // and cancellation is requested, it's better if we try to finish sending spans rather than drop it
            return this.DoPostAsync(this.httpClient, request);
        }

        private async Task DoPostAsync(HttpClient client, HttpRequestMessage request)
        {
            using (var response = await client.SendAsync(request).ConfigureAwait(false))
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
            return new JsonContent(toSerialize, Options);
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

        private class JsonContent : HttpContent
        {
            private static readonly MediaTypeHeaderValue JsonHeader = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8",
            };

            private readonly IEnumerable<ZipkinSpan> spans;
            private readonly JsonSerializerOptions options;

            public JsonContent(IEnumerable<ZipkinSpan> spans, JsonSerializerOptions options)
            {
                this.spans = spans;
                this.options = options;

                this.Headers.ContentType = JsonHeader;
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
                => await JsonSerializer.SerializeAsync(stream, this.spans, this.options).ConfigureAwait(false);

            protected override bool TryComputeLength(out long length)
            {
                // We can't know the length of the content being pushed to the output stream.
                length = -1;
                return false;
            }
        }
    }
}
