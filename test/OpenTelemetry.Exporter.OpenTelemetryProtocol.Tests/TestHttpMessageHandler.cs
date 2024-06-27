// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET
using System.Net.Http;
#endif

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

internal class TestHttpMessageHandler : HttpMessageHandler
{
    public HttpRequestMessage? HttpRequestMessage { get; private set; }

    public byte[]? HttpRequestContent { get; private set; }

    public virtual HttpResponseMessage InternalSend(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        this.HttpRequestMessage = request;
        this.HttpRequestContent = request.Content!.ReadAsByteArrayAsync().Result;
        return new HttpResponseMessage();
    }

#if NET
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return this.InternalSend(request, cancellationToken);
    }
#endif

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(this.InternalSend(request, cancellationToken));
    }
}
