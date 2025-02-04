// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Diagnostics;
using System.Reflection;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

#if NET6_0_OR_GREATER
using System.Security.Cryptography.X509Certificates;
using Grpc.Net.Client;
#elif NETSTANDARD2_1 || NET
using Grpc.Net.Client;
#elif NET462_OR_GREATER || NETSTANDARD2_0
using Grpc.Core;
#endif

namespace OpenTelemetry.Exporter
{
    internal static class OtlpExporterOptionsExtensions
    {
        private const string TraceGrpcServicePath = "opentelemetry.proto.collector.trace.v1.TraceService/Export";
        private const string MetricsGrpcServicePath = "opentelemetry.proto.collector.metrics.v1.MetricsService/Export";
        private const string LogsGrpcServicePath = "opentelemetry.proto.collector.logs.v1.LogsService/Export";

        private const string TraceHttpServicePath = "v1/traces";
        private const string MetricsHttpServicePath = "v1/metrics";
        private const string LogsHttpServicePath = "v1/logs";

#if NET6_0_OR_GREATER
        public static GrpcChannel CreateChannel(this OtlpExporterOptions options)
        {
            if (options.Endpoint.Scheme != Uri.UriSchemeHttp && options.Endpoint.Scheme != Uri.UriSchemeHttps)
            {
                throw new NotSupportedException(
                    $"Endpoint URI scheme ({options.Endpoint.Scheme}) is not supported. " +
                    "Currently only \"http\" and \"https\" are supported.");
            }

            var handler = new HttpClientHandler();

            // Set up custom certificate validation if CertificateFile is provided
            if (!string.IsNullOrEmpty(options.CertificateFile))
            {
                var trustedCertificate = X509Certificate2.CreateFromPemFile(options.CertificateFile);
                handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    if (cert != null && chain != null)
                    {
                        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                        chain.ChainPolicy.CustomTrustStore.Add(trustedCertificate);
                        return chain.Build(cert);
                    }

                    return false;
                };
            }

            // Set up client certificate if provided
            if (!string.IsNullOrEmpty(options.ClientCertificateFile) && !string.IsNullOrEmpty(options.ClientKeyFile))
            {
                var clientCertificate = X509Certificate2.CreateFromPemFile(options.ClientCertificateFile, options.ClientKeyFile);
                handler.ClientCertificates.Add(clientCertificate);
            }

            var grpcChannelOptions = new GrpcChannelOptions
            {
                HttpHandler = handler,
                DisposeHttpClient = true,
            };

            return GrpcChannel.ForAddress(options.Endpoint, grpcChannelOptions);
        }
#elif NETSTANDARD2_1 || NET
        public static GrpcChannel CreateChannel(this OtlpExporterOptions options)
        {
            if (options.Endpoint.Scheme != Uri.UriSchemeHttp && options.Endpoint.Scheme != Uri.UriSchemeHttps)
            {
                throw new NotSupportedException(
                    $"Endpoint URI scheme ({options.Endpoint.Scheme}) is not supported. " +
                    "Currently only \"http\" and \"https\" are supported.");
            }

            return GrpcChannel.ForAddress(options.Endpoint);
        }
#elif NET462_OR_GREATER || NETSTANDARD2_0
        public static Channel CreateChannel(this OtlpExporterOptions options)
        {
            if (options.Endpoint.Scheme != Uri.UriSchemeHttp && options.Endpoint.Scheme != Uri.UriSchemeHttps)
            {
                throw new NotSupportedException(
                    $"Endpoint URI scheme ({options.Endpoint.Scheme}) is not supported. " +
                    "Currently only \"http\" and \"https\" are supported.");
            }

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
        }
#else
#error Not supported
#endif

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
                // According to the specification, URL-encoded headers must be supported.
                optionHeaders = Uri.UnescapeDataString(optionHeaders);

                Array.ForEach(
                    optionHeaders.Split(','),
                    (pair) =>
                    {
                        // Specify the maximum number of substrings to return to 2
                        // This treats everything that follows the first `=` in the string as the value to be added for the metadata key.
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

            foreach (var header in OtlpExporterOptions.StandardHeaders)
            {
                addHeader(headers, header.Key, header.Value);
            }

            return headers;
        }

        public static OtlpExporterTransmissionHandler GetExportTransmissionHandler(this OtlpExporterOptions options, ExperimentalOptions experimentalOptions, OtlpSignalType otlpSignalType)
        {
            var exportClient = GetExportClient(options, otlpSignalType);

            // HttpClient.Timeout.TotalMilliseconds is set correctly whether:
            // 1. The user provides their own HttpClient (using that client’s Timeout value), or
            // 2. The exporter creates the HttpClient using the configured timeout.
            double timeoutMilliseconds = exportClient is OtlpHttpExportClient httpTraceExportClient
                ? httpTraceExportClient.HttpClient.Timeout.TotalMilliseconds
                : options.TimeoutMilliseconds;

            if (experimentalOptions.EnableInMemoryRetry)
            {
                return new OtlpExporterRetryTransmissionHandler(exportClient, timeoutMilliseconds);
            }
            else if (experimentalOptions.EnableDiskRetry)
            {
                Debug.Assert(
                    !string.IsNullOrEmpty(experimentalOptions.DiskRetryDirectoryPath),
                    $"{nameof(experimentalOptions.DiskRetryDirectoryPath)} is null or empty");

                return new OtlpExporterPersistentStorageTransmissionHandler(
                    exportClient,
                    timeoutMilliseconds,
                    Path.Combine(experimentalOptions.DiskRetryDirectoryPath, "traces"));
            }
            else
            {
                return new OtlpExporterTransmissionHandler(exportClient, timeoutMilliseconds);
            }
        }

        public static IExportClient GetExportClient(this OtlpExporterOptions options, OtlpSignalType otlpSignalType)
        {
            var httpClient = options.HttpClientFactory?.Invoke()
                             ?? throw new InvalidOperationException("OtlpExporterOptions was missing HttpClientFactory or it returned null.");

            if (options.Protocol != OtlpExportProtocol.Grpc && options.Protocol != OtlpExportProtocol.HttpProtobuf)
            {
                throw new NotSupportedException($"Protocol {options.Protocol} is not supported.");
            }

#if NET462_OR_GREATER || NETSTANDARD2_0
            if (options.Protocol == OtlpExportProtocol.Grpc)
            {
                var servicePath = otlpSignalType switch
                {
                    OtlpSignalType.Traces => TraceGrpcServicePath,
                    OtlpSignalType.Metrics => MetricsGrpcServicePath,
                    OtlpSignalType.Logs => LogsGrpcServicePath,
                    _ => throw new NotSupportedException($"OtlpSignalType {otlpSignalType} is not supported."),
                };
                return new GrpcExportClient(options, servicePath);
            }
#endif

            return otlpSignalType switch
            {
                OtlpSignalType.Traces => options.Protocol == OtlpExportProtocol.Grpc
                    ? new OtlpGrpcExportClient(options, httpClient, TraceGrpcServicePath)
                    : new OtlpHttpExportClient(options, httpClient, TraceHttpServicePath),

                OtlpSignalType.Metrics => options.Protocol == OtlpExportProtocol.Grpc
                    ? new OtlpGrpcExportClient(options, httpClient, MetricsGrpcServicePath)
                    : new OtlpHttpExportClient(options, httpClient, MetricsHttpServicePath),

                OtlpSignalType.Logs => options.Protocol == OtlpExportProtocol.Grpc
                    ? new OtlpGrpcExportClient(options, httpClient, LogsGrpcServicePath)
                    : new OtlpHttpExportClient(options, httpClient, LogsHttpServicePath),

                _ => throw new NotSupportedException($"OtlpSignalType {otlpSignalType} is not supported."),
            };
        }

        public static void TryEnableIHttpClientFactoryIntegration(this OtlpExporterOptions options, IServiceProvider serviceProvider, string httpClientName)
        {
            if (serviceProvider != null
                && options.Protocol == OtlpExportProtocol.HttpProtobuf
                && options.HttpClientFactory == options.DefaultHttpClientFactory)
            {
                options.HttpClientFactory = () =>
                {
                    Type? httpClientFactoryType = Type.GetType("System.Net.Http.IHttpClientFactory, Microsoft.Extensions.Http", throwOnError: false);
                    if (httpClientFactoryType != null)
                    {
                        object? httpClientFactory = serviceProvider.GetService(httpClientFactoryType);
                        if (httpClientFactory != null)
                        {
                            MethodInfo? createClientMethod = httpClientFactoryType.GetMethod(
                                "CreateClient",
                                BindingFlags.Public | BindingFlags.Instance,
                                binder: null,
                                new Type[] { typeof(string) },
                                modifiers: null);

                            if (createClientMethod != null)
                            {
                                HttpClient? client = (HttpClient?)createClientMethod.Invoke(httpClientFactory, new object[] { httpClientName });

                                if (client != null)
                                {
                                    client.Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds);

                                    // Set up a new HttpClientHandler to configure certificates and callbacks.
                                    var handler = new HttpClientHandler();

#if NET6_0_OR_GREATER
                                    // Add server certificate validation if CertificateFile is specified.
                                    if (!string.IsNullOrEmpty(options.CertificateFile))
                                    {
                                        var trustedCertificate = X509Certificate2.CreateFromPemFile(options.CertificateFile);
                                        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                                        {
                                            if (cert != null && chain != null)
                                            {
                                                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                                                chain.ChainPolicy.CustomTrustStore.Add(trustedCertificate);
                                                return chain.Build(cert);
                                            }

                                            return false;
                                        };
                                    }

                                    // Add client certificate if ClientCertificateFile and ClientKeyFile are specified.
                                    if (!string.IsNullOrEmpty(options.ClientCertificateFile) && !string.IsNullOrEmpty(options.ClientKeyFile))
                                    {
                                        var clientCertificate = X509Certificate2.CreateFromPemFile(options.ClientCertificateFile, options.ClientKeyFile);
                                        handler.ClientCertificates.Add(clientCertificate);
                                    }

                                    // Re-create HttpClient using the custom handler.
                                    return new HttpClient(handler) { Timeout = client.Timeout };
#else
                                    // If certificates are required but the environment is unsupported.
                                    if (!string.IsNullOrEmpty(options.CertificateFile) ||
                                        (!string.IsNullOrEmpty(options.ClientCertificateFile) && !string.IsNullOrEmpty(options.ClientKeyFile)))
                                    {
                                        throw new PlatformNotSupportedException("mTLS support requires .NET 6.0 or later.");
                                    }
#endif
                                }
                            }
                        }
                    }

                    return options.DefaultHttpClientFactory();
                };
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
