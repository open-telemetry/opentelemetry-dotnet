// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

#if NETFRAMEWORK
using System.Net.Http;
#endif

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Protobuf;

/// <summary>Export client interface.</summary>
internal interface IExportClient
{
    /// <summary>
    /// Method for sending export request to the server.
    /// </summary>
    /// <param name="request">The request to send to the server.</param>
    /// <param name="contentLength">length of the content.</param>
    /// <param name="deadlineUtc">The deadline time in utc for export request to finish.</param>
    /// <param name="cancellationToken">An optional token for canceling the call.</param>
    /// <returns><see cref="ExportClientResponse"/>.</returns>
    ExportClientResponse SendExportRequest(byte[] request, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default);

    HttpRequestMessage CreateHttpRequest(byte[] request, int contentLength);

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
