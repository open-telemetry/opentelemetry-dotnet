// <copyright file="JaegerHttpClient.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal sealed class JaegerHttpClient : IJaegerClient
    {
        private static readonly MediaTypeHeaderValue ContentTypeHeader = new("application/vnd.apache.thrift.binary");

        private readonly Uri endpoint;
        private readonly HttpClient httpClient;
        private bool disposed;

        public JaegerHttpClient(Uri endpoint, HttpClient httpClient)
        {
            Debug.Assert(endpoint != null, "endpoint is null");
            Debug.Assert(httpClient != null, "httpClient is null");

            this.endpoint = endpoint;
            this.httpClient = httpClient;
        }

        public bool Connected => true;

        public void Close()
        {
        }

        public void Connect()
        {
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.httpClient.Dispose();

            this.disposed = true;
        }

        public int Send(byte[] buffer, int offset, int count)
        {
            // Prevent Jaeger's HTTP operations from being instrumented.
            using var scope = SuppressInstrumentationScope.Begin();

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, this.endpoint);

            request.Content = new ByteArrayContent(buffer, offset, count)
            {
                Headers = { ContentType = ContentTypeHeader },
            };

#if NET6_0_OR_GREATER
            using HttpResponseMessage response = this.httpClient.Send(request);
#else
            using HttpResponseMessage response = this.httpClient.SendAsync(request).GetAwaiter().GetResult();
#endif
            response.EnsureSuccessStatusCode();

            return count;
        }
    }
}
