// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET
using System.Net.Http;
#endif

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

internal sealed class TestHttpMessageHandler : HttpMessageHandler
{
    public HttpRequestMessage? HttpRequestMessage { get; private set; }

    public byte[]? HttpRequestContent { get; private set; }

    public HttpResponseMessage InternalSend(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        this.HttpRequestMessage = request;
#if NET
        this.HttpRequestContent = request.Content!.ReadAsByteArrayAsync(cancellationToken).Result;
#else
        this.HttpRequestContent = request.Content!.ReadAsByteArrayAsync().Result;
#endif
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
