// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Http.Headers;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Protobuf;

internal static class TrailingHeadersHelpers
{
    public static readonly string ResponseTrailersKey = "__ResponseTrailers";

    public static HttpHeaders TrailingHeaders(this HttpResponseMessage responseMessage)
    {
#if !NETSTANDARD2_0 && !NET462
        return responseMessage.TrailingHeaders;
#else
        if (responseMessage.RequestMessage.Properties.TryGetValue(ResponseTrailersKey, out var headers) &&
            headers is HttpHeaders httpHeaders)
        {
            return httpHeaders;
        }

        // App targets .NET Standard 2.0 and the handler hasn't set trailers
        // in RequestMessage.Properties with known key. Return empty collection.
        // Client call will likely fail because it is unable to get a grpc-status.
        return ResponseTrailers.Empty;
#endif
    }

#if NETSTANDARD2_0 || NET462
    public static void EnsureTrailingHeaders(this HttpResponseMessage responseMessage)
    {
        if (!responseMessage.RequestMessage.Properties.ContainsKey(ResponseTrailersKey))
        {
            responseMessage.RequestMessage.Properties[ResponseTrailersKey] = new ResponseTrailers();
        }
    }

    private class ResponseTrailers : HttpHeaders
    {
        public static readonly ResponseTrailers Empty = new ResponseTrailers();
    }
#endif
}
