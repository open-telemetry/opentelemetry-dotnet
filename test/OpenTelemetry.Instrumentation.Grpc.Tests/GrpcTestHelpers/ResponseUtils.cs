// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Http.Headers;

namespace OpenTelemetry.Instrumentation.Grpc.Tests.GrpcTestHelpers;

internal static class ResponseUtils
{
    internal const string MessageEncodingHeader = "grpc-encoding";
    internal const string IdentityGrpcEncoding = "identity";
    internal const string StatusTrailer = "grpc-status";
    internal static readonly MediaTypeHeaderValue GrpcContentTypeHeaderValue = new MediaTypeHeaderValue("application/grpc");
    internal static readonly Version ProtocolVersion = new Version(2, 0);
    private const int MessageDelimiterSize = 4; // how many bytes it takes to encode "Message-Length"
    private const int HeaderSize = MessageDelimiterSize + 1; // message length + compression flag

    public static HttpResponseMessage CreateResponse(
        HttpStatusCode statusCode,
        HttpContent payload,
        global::Grpc.Core.StatusCode? grpcStatusCode = global::Grpc.Core.StatusCode.OK)
    {
        payload.Headers.ContentType = GrpcContentTypeHeaderValue;

        var message = new HttpResponseMessage(statusCode)
        {
            Content = payload,
            Version = ProtocolVersion,
        };

        message.RequestMessage = new HttpRequestMessage();
#if NETFRAMEWORK
        message.RequestMessage.Properties[TrailingHeadersHelpers.ResponseTrailersKey] = new ResponseTrailers();
#endif
        message.Headers.Add(MessageEncodingHeader, IdentityGrpcEncoding);

        if (grpcStatusCode != null)
        {
            message.TrailingHeaders().Add(StatusTrailer, grpcStatusCode.Value.ToString("D"));
        }

        return message;
    }

    public static Task WriteHeaderAsync(Stream stream, int length, bool compress, CancellationToken cancellationToken)
    {
        var headerData = new byte[HeaderSize];

        // Compression flag
        headerData[0] = compress ? (byte)1 : (byte)0;

        // Message length
        EncodeMessageLength(length, headerData.AsSpan(1));

        return stream.WriteAsync(headerData, 0, headerData.Length, cancellationToken);
    }

    private static void EncodeMessageLength(int messageLength, Span<byte> destination)
    {
        Debug.Assert(destination.Length >= MessageDelimiterSize, "Buffer too small to encode message length.");

        BinaryPrimitives.WriteUInt32BigEndian(destination, (uint)messageLength);
    }

#if NETFRAMEWORK
    private class ResponseTrailers : HttpHeaders
    {
    }
#endif
}
