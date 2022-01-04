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
using System.Net.Http;
using System.Reflection;
using Grpc.Core;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Internal;
#if NETSTANDARD2_1 || NET5_0_OR_GREATER
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;
#endif
using MetricsOtlpCollector = Opentelemetry.Proto.Collector.Metrics.V1;
using TraceOtlpCollector = Opentelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter
{
    internal static class OtlpExporterOptionsExtensions
    {
#if NETSTANDARD2_1 || NET5_0_OR_GREATER
        public static GrpcChannel CreateChannel(this OtlpExporterOptions options)
#else
        public static Channel CreateChannel(this OtlpExporterOptions options)
#endif
        {
            if (options.Endpoint.Scheme != Uri.UriSchemeHttp && options.Endpoint.Scheme != Uri.UriSchemeHttps)
            {
                throw new NotSupportedException($"Endpoint URI scheme ({options.Endpoint.Scheme}) is not supported. Currently only \"http\" and \"https\" are supported.");
            }

#if NETSTANDARD2_1 || NET5_0_OR_GREATER
            // The specification does not dictate the parameters of the default retry policy.
            // These defaults are borrowed from opentelemetry-java.
            var retryPolicy = new RetryPolicy
            {
                // https://github.com/open-telemetry/opentelemetry-java/blob/6f755cc8128f8077643b4990327c22b293c46e45/exporters/otlp/common/src/main/java/io/opentelemetry/exporter/otlp/internal/retry/RetryPolicyBuilder.java#L19-L22
                MaxAttempts = 5,
                InitialBackoff = TimeSpan.FromSeconds(1),
                MaxBackoff = TimeSpan.FromSeconds(5),
                BackoffMultiplier = 1.5,

                // https://github.com/open-telemetry/opentelemetry-java/commit/6f755cc8128f8077643b4990327c22b293c46e45#diff-2d25b543bc2c5a13e495895b6f6a2db80e1167bbe3387024cb917efda35a99acR24-R34
                RetryableStatusCodes =
                {
                    StatusCode.Cancelled,
                    StatusCode.DeadlineExceeded,
                    StatusCode.ResourceExhausted,
                    StatusCode.Aborted,
                    StatusCode.OutOfRange,
                    StatusCode.Unavailable,
                    StatusCode.DataLoss,
                },
            };

            var methodConfig = new MethodConfig
            {
                // MethodName.Default applies the retry policy to all methods called by this channel.
                Names = { MethodName.Default },
                RetryPolicy = retryPolicy,
            };

            var serviceConfig = new ServiceConfig { MethodConfigs = { methodConfig } };

            var channelOptions = new GrpcChannelOptions { ServiceConfig = serviceConfig };

            return GrpcChannel.ForAddress(options.Endpoint, channelOptions);
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

        public static IExportClient<TraceOtlpCollector.ExportTraceServiceRequest> GetTraceExportClient(this OtlpExporterOptions options) =>
            options.Protocol switch
            {
                OtlpExportProtocol.Grpc => new OtlpGrpcTraceExportClient(options),
                OtlpExportProtocol.HttpProtobuf => new OtlpHttpTraceExportClient(
                    options,
                    options.HttpClientFactory?.Invoke() ?? throw new InvalidOperationException("OtlpExporterOptions was missing HttpClientFactory or it returned null.")),
                _ => throw new NotSupportedException($"Protocol {options.Protocol} is not supported."),
            };

        public static IExportClient<MetricsOtlpCollector.ExportMetricsServiceRequest> GetMetricsExportClient(this OtlpExporterOptions options) =>
            options.Protocol switch
            {
                OtlpExportProtocol.Grpc => new OtlpGrpcMetricsExportClient(options),
                OtlpExportProtocol.HttpProtobuf => new OtlpHttpMetricsExportClient(
                    options,
                    options.HttpClientFactory?.Invoke() ?? throw new InvalidOperationException("OtlpExporterOptions was missing HttpClientFactory or it returned null.")),
                _ => throw new NotSupportedException($"Protocol {options.Protocol} is not supported."),
            };

        public static OtlpExportProtocol? ToOtlpExportProtocol(this string protocol) =>
            protocol.Trim() switch
            {
                "grpc" => OtlpExportProtocol.Grpc,
                "http/protobuf" => OtlpExportProtocol.HttpProtobuf,
                _ => null,
            };

        public static void TryEnableIHttpClientFactoryIntegration(this OtlpExporterOptions options, IServiceProvider serviceProvider, string httpClientName)
        {
            if (serviceProvider != null
                && options.Protocol == OtlpExportProtocol.HttpProtobuf
                && options.HttpClientFactory == options.DefaultHttpClientFactory)
            {
                options.HttpClientFactory = () =>
                {
                    Type httpClientFactoryType = Type.GetType("System.Net.Http.IHttpClientFactory, Microsoft.Extensions.Http", throwOnError: false);
                    if (httpClientFactoryType != null)
                    {
                        object httpClientFactory = serviceProvider.GetService(httpClientFactoryType);
                        if (httpClientFactory != null)
                        {
                            MethodInfo createClientMethod = httpClientFactoryType.GetMethod(
                                "CreateClient",
                                BindingFlags.Public | BindingFlags.Instance,
                                binder: null,
                                new Type[] { typeof(string) },
                                modifiers: null);
                            if (createClientMethod != null)
                            {
                                HttpClient client = (HttpClient)createClientMethod.Invoke(httpClientFactory, new object[] { httpClientName });

                                client.Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds);

                                return client;
                            }
                        }
                    }

                    return options.DefaultHttpClientFactory();
                };
            }
        }

        internal static void AppendExportPath(this OtlpExporterOptions options, Uri initialEndpoint, string exportRelativePath)
        {
            // The exportRelativePath is only appended when the options.Endpoint property wasn't set by the user,
            // the protocol is HttpProtobuf and the OTEL_EXPORTER_OTLP_ENDPOINT environment variable
            // is present. If the user provides a custom value for options.Endpoint that value is taken as is.
            if (ReferenceEquals(initialEndpoint, options.Endpoint))
            {
                if (options.Protocol == OtlpExportProtocol.HttpProtobuf)
                {
                    if (EnvironmentVariableHelper.LoadUri(OtlpExporterOptions.EndpointEnvVarName, out Uri endpoint))
                    {
                        // At this point we can conclude that endpoint was initialized from OTEL_EXPORTER_OTLP_ENDPOINT
                        // and has to be appended by export relative path (traces/metrics).
                        options.Endpoint = endpoint.AppendPathIfNotPresent(exportRelativePath);
                    }
                }
            }
        }

        internal static Uri AppendPathIfNotPresent(this Uri uri, string path)
        {
            var absoluteUri = uri.AbsoluteUri;
            var separator = string.Empty;

            if (absoluteUri.EndsWith("/"))
            {
                // Endpoint already ends with 'path/'
                if (absoluteUri.EndsWith(string.Concat(path, "/"), StringComparison.OrdinalIgnoreCase))
                {
                    return uri;
                }
            }
            else
            {
                // Endpoint already ends with 'path'
                if (absoluteUri.EndsWith(path, StringComparison.OrdinalIgnoreCase))
                {
                    return uri;
                }

                separator = "/";
            }

            return new Uri(string.Concat(uri.AbsoluteUri, separator, path));
        }
    }
}
