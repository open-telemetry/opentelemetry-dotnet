// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET
using System.Net.Http;
#endif
using System.Net;
using System.Net.Http.Headers;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

/// <summary>
/// Returns a response whose declared content length is configurable, so a test
/// can exceed what the exporter accepts without allocating a large payload.
/// </summary>
internal sealed class OversizedResponseHttpMessageHandler : HttpMessageHandler
{
    private readonly long contentLength;
    private readonly string mediaType;

    public OversizedResponseHttpMessageHandler(long contentLength, string mediaType)
    {
        this.contentLength = contentLength;
        this.mediaType = mediaType;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(this.CreateResponse());

#if NET
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        => this.CreateResponse();
#endif

    private HttpResponseMessage CreateResponse()
    {
        // Only the declared length matters to the size check, so the body itself
        // is kept small.
        var content = new ByteArrayContent([1, 2, 3]);
        content.Headers.ContentType = new MediaTypeHeaderValue(this.mediaType);
        content.Headers.ContentLength = this.contentLength;

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }
}
