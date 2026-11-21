// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET
using System.Net.Http;
#endif
using System.Net;
using System.Net.Http.Headers;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

/// <summary>
/// Returns a successful response with a fixed body, as an OTLP/HTTP or OTLP/gRPC server would.
/// </summary>
internal sealed class PartialSuccessHttpMessageHandler : HttpMessageHandler
{
    private readonly byte[] body;
    private readonly bool grpc;
    private readonly string? mediaType;
    private readonly bool declareLength;

    public PartialSuccessHttpMessageHandler(byte[] body, bool grpc, string? mediaType = "default", bool declareLength = true)
    {
        this.body = body;
        this.grpc = grpc;
        this.mediaType = mediaType == "default"
            ? (grpc ? "application/grpc" : "application/x-protobuf")
            : mediaType;
        this.declareLength = declareLength;
    }

    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(this.CreateResponse(request));

#if NET
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        => this.CreateResponse(request);
#endif

    private HttpResponseMessage CreateResponse(HttpRequestMessage request)
    {
        this.RequestCount++;

        HttpContent content = this.declareLength
            ? new ByteArrayContent(this.body)
            : new UnknownLengthContent(this.body);

        if (this.mediaType is not null)
        {
            content.Headers.ContentType = new MediaTypeHeaderValue(this.mediaType);
        }

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = content,
        };

        if (this.grpc)
        {
            response.Headers.Add("grpc-status", "0");
        }

        return response;
    }

    private sealed class UnknownLengthContent(byte[] body) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(body, 0, body.Length);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

#if NET
        protected override Stream CreateContentReadStream(CancellationToken cancellationToken)
            => new MemoryStream(body, writable: false);
#endif

        protected override Task<Stream> CreateContentReadStreamAsync()
            => Task.FromResult<Stream>(new MemoryStream(body, writable: false));
    }
}
