// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// Includes work from:
/*
* Copyright 2019 The gRPC Authors
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
*     http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net;
using System.Net.Http.Headers;
using Grpc.Core;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Custom.ExportClient;

// https://github.com/grpc/grpc-dotnet/blob/master/src/Grpc.Net.Client/Internal/GrpcProtocolHelpers.cs
internal class GrpcProtocolHelper
{
    private const string GrpcStatusHeader = "grpc-status";
    private const string GrpcMessageHeader = "grpc-message";
    private const string MessageEncodingHeader = "grpc-encoding";
    private const string MessageAcceptEncodingHeader = "grpc-accept-encoding";
    private const string GrpcContentType = "application/grpc";

    private static readonly Version Http2Version = new Version(2, 0);

    internal static void ProcessHttpResponse(HttpResponseMessage httpResponse, out RpcException? rpcException)
    {
        rpcException = null;
        var status = ValidateHeaders(httpResponse, out var trailers);

        if (status != null && status.HasValue)
        {
            if (status.Value.StatusCode == StatusCode.OK)
            {
                // https://github.com/grpc/grpc-dotnet/blob/1416340c85bb5925b5fed0c101e7e6de71e367e0/src/Grpc.Net.Client/Internal/GrpcCall.cs#L526-L527
                // Status OK should always be set as part of Trailers.
                // Change the status code to a more accurate status.
                // This is consistent with Grpc.Core client behavior.
                status = new Status(StatusCode.Internal, "Failed to deserialize response message. The response header contains a gRPC status of OK, which means any message returned to the client for this call should be ignored. A unary or client streaming gRPC call must have a response message, which makes this response invalid.");
                rpcException = new RpcException(status.Value, trailers ?? Metadata.Empty);
            }
            else
            {
                rpcException = new RpcException(status.Value, trailers ?? Metadata.Empty);
            }
        }

        if (status == null)
        {
            // TODO: We need to read the response message here (content)
            // if the returned status is OK but content is null then change status to internal error.
            // ref: https://github.com/grpc/grpc-dotnet/blob/1416340c85bb5925b5fed0c101e7e6de71e367e0/src/Grpc.Net.Client/Internal/GrpcCall.cs#L558-L575

            // Check to see if the status is part of trailers
            // TODO: Proper handling of isBrowser/isWinHttp
            status = GetResponseStatus(httpResponse, false, false);

            if (status != null && status.HasValue && status.Value.StatusCode != StatusCode.OK)
            {
                rpcException = new RpcException(status.Value, trailers ?? Metadata.Empty);
            }
        }
    }

    private static bool TryGetStatusCore(HttpHeaders httpHeaders, out Status? status)
    {
        var grpcStatus = GetHeaderValue(httpHeaders, GrpcStatusHeader);

        // grpc-status is a required trailer
        if (grpcStatus == null)
        {
            status = null;
            return false;
        }

        int statusValue;
        if (!int.TryParse(grpcStatus, out statusValue))
        {
            throw new InvalidOperationException("Unexpected grpc-status value: " + grpcStatus);
        }

        // grpc-message is optional
        // Always read the gRPC message from the same headers collection as the status
        var grpcMessage = GetHeaderValue(httpHeaders, GrpcMessageHeader);

        if (!string.IsNullOrEmpty(grpcMessage))
        {
            // https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-HTTP2.md#responses
            // The value portion of Status-Message is conceptually a Unicode string description of the error,
            // physically encoded as UTF-8 followed by percent-encoding.
            grpcMessage = Uri.UnescapeDataString(grpcMessage);
        }

        status = new Status((StatusCode)statusValue, grpcMessage ?? string.Empty);
        return true;
    }

    private static Status? ValidateHeaders(HttpResponseMessage httpResponse, out Metadata? trailers)
    {
        // gRPC status can be returned in the header when there is no message (e.g. unimplemented status)
        // An explicitly specified status header has priority over other failing statuses
        if (TryGetStatusCore(httpResponse.Headers, out var status))
        {
            // Trailers are in the header because there is no message.
            // Note that some default headers will end up in the trailers (e.g. Date, Server).
            trailers = BuildMetadata(httpResponse.Headers);
            return status;
        }

        trailers = null;

        // ALPN negotiation is sending HTTP/1.1 and HTTP/2.
        // Check that the response wasn't downgraded to HTTP/1.1.
        if (httpResponse.Version < Http2Version)
        {
            return new Status(StatusCode.Internal, $"Bad gRPC response. Response protocol downgraded to HTTP/{httpResponse.Version.ToString(2)}.");
        }

        if (httpResponse.StatusCode != HttpStatusCode.OK)
        {
            var statusCode = MapHttpStatusToGrpcCode(httpResponse.StatusCode);
            return new Status(statusCode, "Bad gRPC response. HTTP status code: " + (int)httpResponse.StatusCode);
        }

        // Don't access Headers.ContentType property because it is not threadsafe.
        var contentType = GetHeaderValue(httpResponse.Content?.Headers, "Content-Type");
        if (contentType == null)
        {
            return new Status(StatusCode.Cancelled, "Bad gRPC response. Response did not have a content-type header.");
        }

        if (!IsContentType(GrpcContentType, contentType))
        {
            return new Status(StatusCode.Cancelled, "Bad gRPC response. Invalid content-type value: " + contentType);
        }

        // Call is still in progress
        return null;
    }

    private static StatusCode MapHttpStatusToGrpcCode(HttpStatusCode httpStatusCode)
    {
        switch (httpStatusCode)
        {
            case HttpStatusCode.BadRequest: // 400
#if !NETSTANDARD2_0 && !NET462
            case HttpStatusCode.RequestHeaderFieldsTooLarge: // 431
#else
            case (HttpStatusCode)431:
#endif
                return StatusCode.Internal;
            case HttpStatusCode.Unauthorized: // 401
                return StatusCode.Unauthenticated;
            case HttpStatusCode.Forbidden: // 403
                return StatusCode.PermissionDenied;
            case HttpStatusCode.NotFound: // 404
                return StatusCode.Unimplemented;
#if !NETSTANDARD2_0 && !NET462
            case HttpStatusCode.TooManyRequests: // 429
#else
            case (HttpStatusCode)429:
#endif
            case HttpStatusCode.BadGateway: // 502
            case HttpStatusCode.ServiceUnavailable: // 503
            case HttpStatusCode.GatewayTimeout: // 504
                return StatusCode.Unavailable;
            default:
                if ((int)httpStatusCode >= 100 && (int)httpStatusCode < 200)
                {
                    // 1xx. These headers should have been ignored.
                    return StatusCode.Internal;
                }

                return StatusCode.Unknown;
        }
    }

    private static Metadata BuildMetadata(HttpHeaders responseHeaders)
    {
        var headers = new Metadata();

#if NET6_0_OR_GREATER
        // Use NonValidated to avoid race-conditions and because it is faster.
        foreach (var header in responseHeaders.NonValidated)
#else
        foreach (var header in responseHeaders)
#endif
        {
            if (ShouldSkipHeader(header.Key))
            {
                continue;
            }

            foreach (var value in header.Value)
            {
                if (header.Key.EndsWith(Metadata.BinaryHeaderSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    headers.Add(header.Key, ParseBinaryHeader(value));
                }
                else
                {
                    headers.Add(header.Key, value);
                }
            }
        }

        return headers;
    }

    private static byte[] ParseBinaryHeader(string base64)
    {
        string decodable;
        switch (base64.Length % 4)
        {
            case 0:
                // base64 has the required padding
                decodable = base64;
                break;
            case 2:
                // 2 chars padding
                decodable = base64 + "==";
                break;
            case 3:
                // 3 chars padding
                decodable = base64 + "=";
                break;
            default:
                // length%4 == 1 should be illegal
                throw new FormatException("Invalid Base-64 header value.");
        }

        return Convert.FromBase64String(decodable);
    }

    private static bool ShouldSkipHeader(string name)
    {
        if (name.Length == 0)
        {
            return false;
        }

        switch (name[0])
        {
            case ':':
                // ASP.NET Core includes pseudo headers in the set of request headers
                // whereas, they are not in gRPC implementations. We will filter them
                // out when we construct the list of headers on the context.
                return true;
            case 'g':
            case 'G':
                // Exclude known grpc headers. This matches Grpc.Core client behavior.
                return string.Equals(name, GrpcStatusHeader, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, GrpcMessageHeader, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, MessageEncodingHeader, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, MessageAcceptEncodingHeader, StringComparison.OrdinalIgnoreCase);
            case 'c':
            case 'C':
                // Exclude known HTTP headers. This matches Grpc.Core client behavior.
                return string.Equals(name, "content-encoding", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "content-type", StringComparison.OrdinalIgnoreCase);
            default:
                return false;
        }
    }

    private static Status GetResponseStatus(HttpResponseMessage httpResponse, bool isBrowser, bool isWinHttp)
    {
        Status? status;
        try
        {
            if (!TryGetStatusCore(httpResponse.TrailingHeaders(), out status))
            {
                var detail = "No grpc-status found on response.";
                if (isBrowser)
                {
                    detail += " If the gRPC call is cross domain then CORS must be correctly configured. Access-Control-Expose-Headers needs to include 'grpc-status' and 'grpc-message'.";
                }

                if (isWinHttp)
                {
                    detail += " Using gRPC with WinHttp has Windows and package version requirements. See https://aka.ms/aspnet/grpc/netstandard for details.";
                }

                status = new Status(StatusCode.Cancelled, detail);
            }
        }
        catch (Exception ex)
        {
            // Handle error from parsing badly formed status
            status = new Status(StatusCode.Cancelled, ex.Message, ex);
        }

        return status.HasValue ? status.Value : default;
    }

    private static string? GetHeaderValue(HttpHeaders? headers, string name, bool first = false)
    {
        if (headers == null)
        {
            return null;
        }

#if NET6_0_OR_GREATER
        if (!headers.NonValidated.TryGetValues(name, out var values))
        {
            return null;
        }

        using (var e = values.GetEnumerator())
        {
            if (!e.MoveNext())
            {
                return null;
            }

            var result = e.Current;
            if (!e.MoveNext())
            {
                return result;
            }

            if (first)
            {
                return result;
            }
        }

        throw new InvalidOperationException($"Multiple {name} headers.");
#else
        if (!headers.TryGetValues(name, out var values))
        {
            return null;
        }

        // HttpHeaders appears to always return an array, but fallback to converting values to one just in case
        var valuesArray = values as string[] ?? values.ToArray();

        switch (valuesArray.Length)
        {
            case 0:
                return null;
            case 1:
                return valuesArray[0];
            default:
                if (first)
                {
                    return valuesArray[0];
                }

                throw new InvalidOperationException($"Multiple {name} headers.");
        }
#endif
    }

    private static bool IsContentType(string contentType, string? s)
    {
        if (s == null)
        {
            return false;
        }

        if (!s.StartsWith(contentType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (s.Length == contentType.Length)
        {
            // Exact match
            return true;
        }

        // Support variations on the content-type (e.g. +proto, +json)
        var nextChar = s[contentType.Length];
        if (nextChar == ';')
        {
            return true;
        }

        if (nextChar == '+')
        {
            // Accept any message format. Marshaller could be set to support third-party formats
            return true;
        }

        return false;
    }
}
