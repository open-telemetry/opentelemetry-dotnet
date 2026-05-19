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

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        this.HttpRequestMessage = request;
#if NET
        this.HttpRequestContent = await request.Content!.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
#else
        this.HttpRequestContent = await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
#endif
        return new HttpResponseMessage();
    }

#if NET
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        this.HttpRequestMessage = request;

        using var stream = request.Content!.ReadAsStream(cancellationToken);
        using var memoryStream = new MemoryStream();

        stream.CopyTo(memoryStream);
        this.HttpRequestContent = memoryStream.ToArray();

        return new HttpResponseMessage();
    }
#endif
}
