// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Http.Headers;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

internal sealed class ExportClientHttpResponse : ExportClientResponse
{
    public ExportClientHttpResponse(
        bool success,
        DateTime? deadlineUtc,
        HttpResponseMessage? response,
        Exception? exception)
        : base(success, deadlineUtc, exception)
    {
        this.Headers = response?.Headers;
        this.StatusCode = response?.StatusCode;
    }

    public HttpResponseHeaders? Headers { get; }

    public HttpStatusCode? StatusCode { get; }
}
