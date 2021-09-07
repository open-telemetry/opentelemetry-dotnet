// <copyright file="IHttpHandler.cs" company="OpenTelemetry Authors">
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
    /// Interface partialy exposing <see cref="HttpClient"/> methods.
    /// </summary>
    public interface IHttpHandler
    {
        /// <summary>
        /// Cancel all pending requests on this instance.
        /// </summary>
        void CancelPendingRequests();

        /// <summary>
        /// Send an HTTP request as an asynchronous operation.
        /// </summary>
        /// <param name="request">The HTTP request message to send.</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <returns>Result of the export operation.</returns>
        HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken = default);
    }
}
