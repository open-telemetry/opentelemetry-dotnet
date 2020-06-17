// <copyright file="ZipkinTraceExporter.cs" company="OpenTelemetry Authors">
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
        private readonly ZipkinTraceExporterOptions options;
        private readonly HttpClient httpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZipkinTraceExporter"/> class.
        /// </summary>
        /// <param name="options">Configuration options.</param>
        /// <param name="client">Http client to use to upload telemetry.</param>
        public ZipkinTraceExporter(ZipkinTraceExporterOptions options, HttpClient client = null)
        {
            this.options = options;
            this.LocalEndpoint = this.GetLocalZipkinEndpoint();
            this.httpClient = client ?? new HttpClient();
        }

        internal ZipkinEndpoint LocalEndpoint { get; }

        /// <inheritdoc/>
        public override async Task<ExportResult> ExportAsync(IEnumerable<SpanData> batch, CancellationToken cancellationToken)
        {
            try
            {
                await this.SendSpansAsync(batch).ConfigureAwait(false);
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

        private Task SendSpansAsync(IEnumerable<SpanData> spans)
        {
            var requestUri = this.options.Endpoint;

            var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new JsonContent(this, spans),
            };

            // avoid cancelling here: this is no return point: if we reached this point
            // and cancellation is requested, it's better if we try to finish sending spans rather than drop it
            return this.httpClient.SendAsync(request);
        }

        private ZipkinEndpoint GetLocalZipkinEndpoint()
        {
            var hostName = this.ResolveHostName();

            string ipv4 = null;
            string ipv6 = null;
            if (!string.IsNullOrEmpty(hostName))
            {
                ipv4 = this.ResolveHostAddress(hostName, AddressFamily.InterNetwork);
                ipv6 = this.ResolveHostAddress(hostName, AddressFamily.InterNetworkV6);
            }

            return new ZipkinEndpoint(
                this.options.ServiceName,
                ipv4,
                ipv6,
                null);
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

            private static Utf8JsonWriter writer;

            private readonly ZipkinTraceExporter exporter;
            private readonly IEnumerable<SpanData> spans;

            public JsonContent(ZipkinTraceExporter exporter, IEnumerable<SpanData> spans)
            {
                this.exporter = exporter;
                this.spans = spans;

                this.Headers.ContentType = JsonHeader;
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                if (writer == null)
                {
                    writer = new Utf8JsonWriter(stream);
                }
                else
                {
                    writer.Reset(stream);
                }

                writer.WriteStartArray();

                foreach (var span in this.spans)
                {
                    var zipkinSpan = span.ToZipkinSpan(this.exporter.LocalEndpoint, this.exporter.options.UseShortTraceIds);

                    zipkinSpan.Write(writer);

                    zipkinSpan.Return();
                }

                writer.WriteEndArray();

                return writer.FlushAsync();
            }

            protected override bool TryComputeLength(out long length)
            {
                // We can't know the length of the content being pushed to the output stream.
                length = -1;
                return false;
            }
        }
    }
}
