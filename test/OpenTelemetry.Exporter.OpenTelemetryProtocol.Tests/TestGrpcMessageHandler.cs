// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET
using System.Net.Http;
using System.Net.Http.Headers;
#endif

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

internal sealed class TestGrpcMessageHandler : HttpMessageHandler
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
        var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            RequestMessage = request,
        };

#if NETSTANDARD2_0 || NET462
        const string ResponseTrailersKey = "__ResponseTrailers";

        if (!response.RequestMessage.Properties.TryGetValue(ResponseTrailersKey, out var value))
        {
            value = new CustomResponseTrailers();
            response.RequestMessage.Properties[ResponseTrailersKey] = value;
        }

        var trailers = (HttpHeaders)value;
        trailers.Add("grpc-status", "0");
#else
        response.TrailingHeaders.Add("grpc-status", "0");
#endif

        return response;
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

#if NETSTANDARD2_0 || NET462
    private sealed class CustomResponseTrailers : HttpHeaders
    {
    }
#endif
}
