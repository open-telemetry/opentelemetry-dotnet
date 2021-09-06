// <copyright file="HttpHandler.cs" company="OpenTelemetry Authors">
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
using System.Net.Http;
using System.Threading;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    /// <summary>
    /// Class decorating <see cref="System.Net.Http.HttpClient"/>.
    /// </summary>
    internal class HttpHandler : IHttpHandler
    {
        internal readonly HttpClient HttpClient;

        public HttpHandler(TimeSpan timeout)
        {
            this.HttpClient = new HttpClient
            {
                Timeout = timeout,
            };
        }

        public void CancelPendingRequests()
        {
            this.HttpClient.CancelPendingRequests();
        }

        public void Dispose()
        {
            this.HttpClient.Dispose();
        }

        public HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
#if NET5_0
            return this.HttpClient.Send(request);
#else
            return AsyncHelper.RunSync(() => this.HttpClient.SendAsync(request));
#endif
        }
    }
}
