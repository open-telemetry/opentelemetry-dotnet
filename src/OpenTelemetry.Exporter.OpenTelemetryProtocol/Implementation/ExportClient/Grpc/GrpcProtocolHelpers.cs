// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#if NET462
using System.Net.Http;
#endif
using System.Net.Http.Headers;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;

internal static class GrpcProtocolHelpers
{
    internal const string StatusTrailer = "grpc-status";
    internal const string MessageTrailer = "grpc-message";

    public static Status GetResponseStatus(HttpResponseMessage httpResponse, HttpHeaders trailingHeaders)
    {
        try
        {
            return trailingHeaders.Any()
                ? GetStatusCore(trailingHeaders)
                : GetStatusCore(httpResponse.Headers);
        }
        catch (Exception ex)
        {
            // Handle error from parsing badly formed status
            return new Status(StatusCode.Internal, ex.Message, ex);
        }
    }

    public static Status GetStatusCore(HttpHeaders headers)
    {
        var grpcStatus = GetHeaderValue(headers, StatusTrailer);

        // grpc-status is a required trailer
        if (grpcStatus == null)
        {
            return Status.NoReply;
        }

        int statusValue;
        if (!int.TryParse(grpcStatus, out statusValue))
        {
            throw new InvalidOperationException("Unexpected grpc-status value: " + grpcStatus);
        }

        // grpc-message is optional
        // Always read the gRPC message from the same headers collection as the status
        var grpcMessage = GetHeaderValue(headers, MessageTrailer);

        if (!string.IsNullOrEmpty(grpcMessage))
        {
            // https://github.com/grpc/grpc/blob/master/doc/PROTOCOL-HTTP2.md#responses
            // The value portion of Status-Message is conceptually a Unicode string description of the error,
            // physically encoded as UTF-8 followed by percent-encoding.
            grpcMessage = Uri.UnescapeDataString(grpcMessage);
        }

        return new Status((StatusCode)statusValue, grpcMessage ?? string.Empty);
    }

    public static string? GetHeaderValue(HttpHeaders? headers, string name, bool first = false)
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
}
