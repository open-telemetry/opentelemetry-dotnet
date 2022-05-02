// <copyright file="BaseOtlpHttpExportClient.cs" company="OpenTelemetry Authors">
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
using System.Net.Http;
using System.Threading;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient
{
    /// <summary>Base class for sending OTLP export request over HTTP.</summary>
    /// <typeparam name="TRequest">Type of export request.</typeparam>
    internal abstract class BaseOtlpHttpExportClient<TRequest> : IExportClient<TRequest>
    {
        protected BaseOtlpHttpExportClient(OtlpExporterOptions options, HttpClient httpClient, string signalPath)
        {
            Guard.ThrowIfNull(options);
            Guard.ThrowIfNull(httpClient);
            Guard.ThrowIfNull(signalPath);
            Guard.ThrowIfInvalidTimeout(options.TimeoutMilliseconds);

            Uri exporterEndpoint = !options.ProgrammaticallyModifiedEndpoint
                ? options.Endpoint.AppendPathIfNotPresent(signalPath)
                : options.Endpoint;
            this.Endpoint = new UriBuilder(exporterEndpoint).Uri;
            this.Headers = options.GetHeaders<Dictionary<string, string>>((d, k, v) => d.Add(k, v));
            this.HttpClient = httpClient;
        }

        internal HttpClient HttpClient { get; }

        internal Uri Endpoint { get; set; }

        internal IReadOnlyDictionary<string, string> Headers { get; }

        /// <inheritdoc/>
        public bool SendExportRequest(TRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                using var httpRequest = this.CreateHttpRequest(request);

                using var httpResponse = this.SendHttpRequest(httpRequest, cancellationToken);

                httpResponse?.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);

                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public bool Shutdown(int timeoutMilliseconds)
        {
            this.HttpClient.CancelPendingRequests();
            return true;
        }

        protected abstract HttpContent CreateHttpContent(TRequest exportRequest);

        protected HttpRequestMessage CreateHttpRequest(TRequest exportRequest)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, this.Endpoint);
            foreach (var header in this.Headers)
            {
                request.Headers.Add(header.Key, header.Value);
            }

            request.Content = this.CreateHttpContent(exportRequest);

            return request;
        }

        protected HttpResponseMessage SendHttpRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
#if NET6_0_OR_GREATER
            return this.HttpClient.Send(request, cancellationToken);
#else
            return this.HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult();
#endif
        }
    }
}
