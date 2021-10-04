// <copyright file="IExportClient.cs" company="OpenTelemetry Authors">
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

using System.Threading;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient
{
    /// <summary>Export client interface.</summary>
    /// <typeparam name="TRequest">Type of export request.</typeparam>
    internal interface IExportClient<in TRequest>
    {
        /// <summary>
        /// Method for sending export request to the server.
        /// </summary>
        /// <param name="request">The request to send to the server.</param>
        /// <param name="cancellationToken">An optional token for canceling the call.</param>
        /// <returns>True if the request has been sent successfully, otherwise false.</returns>
        bool SendExportRequest(TRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Method for shutting down the export client.
        /// </summary>
        /// <param name="timeoutMilliseconds">
        /// The number of milliseconds to wait, or <c>Timeout.Infinite</c> to
        /// wait indefinitely.
        /// </param>
        /// <returns>
        /// Returns <c>true</c> if shutdown succeeded; otherwise, <c>false</c>.
        /// </returns>
        bool Shutdown(int timeoutMilliseconds);
    }
}
