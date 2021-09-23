// <copyright file="OtlpExporterOptionsExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
#if NETSTANDARD2_1
using Grpc.Net.Client;
#endif
using OtlpCollector = Opentelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter
{
    internal static class OtlpExporterOptionsExtensions
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
            return options.GetHeaders<Metadata>((m, k, v) => m.Add(k, v));
        }

        public static THeaders GetHeaders<THeaders>(this OtlpExporterOptions options, Action<THeaders, string, string> addHeader)
            where THeaders : new()
        {
            var optionHeaders = options.Headers;
            var headers = new THeaders();
            if (!string.IsNullOrEmpty(optionHeaders))
            {
                Array.ForEach(
                    optionHeaders.Split(','),
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
                        addHeader(headers, key, value);
                    });
            }

            return headers;
        }

        public static IExportClient<OtlpCollector.ExportTraceServiceRequest> GetTraceExportClient(this OtlpExporterOptions options) =>
            options.Protocol switch
            {
                ExportProtocol.Grpc => new OtlpGrpcTraceExportClient(options),
                ExportProtocol.HttpProtobuf => new OtlpHttpTraceExportClient(options),
                _ => throw new NotSupportedException($"Protocol {options.Protocol} is not supported.")
            };

        public static ExportProtocol? ToExportProtocol(this string protocol) =>
            protocol.Trim() switch
            {
                "grpc" => ExportProtocol.Grpc,
                "http/protobuf" => ExportProtocol.HttpProtobuf,
                _ => null
            };

        public static void InitializeEndpoints(this OtlpExporterOptions options, ExportProtocol protocol)
        {
            switch (protocol)
            {
                case ExportProtocol.Grpc:
                    InitializeGrpcEndpoints(options);
                    break;
                case ExportProtocol.HttpProtobuf:
                    InitializeHttpProtobufEndpoints(options);
                    break;
                default:
                    throw new NotSupportedException($"Export protocol {protocol} is not supported.");
            }
        }

        private static void InitializeGrpcEndpoints(OtlpExporterOptions options)
        {
            if (TryCreateUriFromEndpointEnvVar(OtlpExporterOptions.EndpointEnvVarName, out var endpoint))
            {
                options.Endpoint = endpoint;
            }
        }

        private static void InitializeHttpProtobufEndpoints(OtlpExporterOptions options)
        {
            if (TryCreateUriFromEndpointEnvVar(OtlpExporterOptions.EndpointEnvVarName, out var endpoint))
            {
                options.Endpoint = endpoint;

                if (Uri.TryCreate(endpoint.AbsoluteUri.AppendPath(OtlpExporterOptions.TraceExportPath), UriKind.Absolute, out var traceEndpointUri))
                {
                    options.TracesEndpoint = traceEndpointUri;
                }

                if (Uri.TryCreate(endpoint.AbsoluteUri.AppendPath(OtlpExporterOptions.MetricsExportPath), UriKind.Absolute, out var metricsEndpointUri))
                {
                    options.MetricsEndpoint = metricsEndpointUri;
                }
            }

            if (TryCreateUriFromEndpointEnvVar(OtlpExporterOptions.TracesEndpointEnvVarName, out var tracesEndpoint))
            {
                options.TracesEndpoint = tracesEndpoint;
            }

            if (TryCreateUriFromEndpointEnvVar(OtlpExporterOptions.MetricsEndpointEnvVarName, out var metricsEndpoint))
            {
                options.MetricsEndpoint = metricsEndpoint;
            }
        }

        private static string AppendPath(this string endpoint, string path)
        {
            var separator = endpoint.EndsWith("/") ? string.Empty : "/";
            return string.Concat(endpoint, separator, path);
        }

        private static bool TryCreateUriFromEndpointEnvVar(string endpointEnvVarName, out Uri uri)
        {
            uri = null;
            var uriCreated = false;
            var endpointEnvVar = Environment.GetEnvironmentVariable(endpointEnvVarName);

            if (!string.IsNullOrEmpty(endpointEnvVar))
            {
                uriCreated = Uri.TryCreate(endpointEnvVar, UriKind.Absolute, out uri);
                if (!uriCreated)
                {
                    OpenTelemetryProtocolExporterEventSource.Log.FailedToParseEnvironmentVariable(OtlpExporterOptions.EndpointEnvVarName, endpointEnvVar);
                }
            }

            return uriCreated;
        }
    }
}
