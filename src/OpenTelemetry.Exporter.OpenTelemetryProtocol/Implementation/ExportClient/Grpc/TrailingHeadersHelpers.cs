// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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

internal static class TrailingHeadersHelpers
{
#if NETSTANDARD2_0 || NET462
    private const string ResponseTrailersKey = "__ResponseTrailers";
#endif

    public static HttpHeaders TrailingHeaders(this HttpResponseMessage responseMessage)
    {
#if NETSTANDARD2_0 || NET462

        if (responseMessage.RequestMessage.Properties.TryGetValue(ResponseTrailersKey, out var headers) &&
            headers is HttpHeaders httpHeaders)
        {
            return httpHeaders;
        }

        // App targets .NET Standard 2.0 and the handler hasn't set trailers
        // in RequestMessage.Properties with known key. Return empty collection.
        // Client call will likely fail because it is unable to get a grpc-status.
        return ResponseTrailers.Empty;
#else
        return responseMessage.TrailingHeaders;
#endif
    }

#if NETSTANDARD2_0 || NET462
    private class ResponseTrailers : HttpHeaders
    {
        public static readonly ResponseTrailers Empty = new();
    }
#endif
}
