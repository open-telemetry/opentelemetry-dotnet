// <copyright file="OtlpExporterOptionsGrpcExtensions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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
// </copyright>

using System;
using Grpc.Core;
#if NETSTANDARD2_1
using Grpc.Net.Client;
#endif

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    internal static class OtlpExporterOptionsGrpcExtensions
    {
#if NETSTANDARD2_1
        public static GrpcChannel CreateChannel(this OtlpExporterOptions options)
#else
        public static Channel CreateChannel(this OtlpExporterOptions options)
#endif
        {
            if (options.Endpoint.Scheme != Uri.UriSchemeHttp && options.Endpoint.Scheme != Uri.UriSchemeHttps)
            {
                throw new NotSupportedException($"Endpoint URI scheme ({options.Endpoint.Scheme}) is not supported. Currently only \"http\" and \"https\" are supported.");
            }

#if NETSTANDARD2_1
            return GrpcChannel.ForAddress(options.Endpoint);
#else
            ChannelCredentials channelCredentials;
            if (options.Endpoint.Scheme == Uri.UriSchemeHttps)
            {
                channelCredentials = new SslCredentials();
            }
            else
            {
                channelCredentials = ChannelCredentials.Insecure;
            }

            return new Channel(options.Endpoint.Authority, channelCredentials);
#endif
        }

        public static Metadata GetMetadataFromHeaders(this OtlpExporterOptions options)
        {
            var headers = options.Headers;
            var metadata = new Metadata();
            if (!string.IsNullOrEmpty(headers))
            {
                Array.ForEach(
                    headers.Split(','),
                    (pair) =>
                    {
                        // Specify the maximum number of substrings to return to 2
                        // This treats everything that follows the first `=` in the string as the value to be added for the metadata key
                        var keyValueData = pair.Split(new char[] { '=' }, 2);
                        if (keyValueData.Length != 2)
                        {
                            throw new ArgumentException("Headers provided in an invalid format.");
                        }

                        var key = keyValueData[0].Trim();
                        var value = keyValueData[1].Trim();
                        metadata.Add(key, value);
                    });
            }

            return metadata;
        }
    }
}
