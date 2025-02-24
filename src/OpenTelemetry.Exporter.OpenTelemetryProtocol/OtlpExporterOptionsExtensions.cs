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
#if NET462_OR_GREATER || NETSTANDARD2_0
using Grpc.Core;
#endif

namespace OpenTelemetry.Exporter;

internal static class OtlpExporterOptionsExtensions
{
    private const string TraceGrpcServicePath = "opentelemetry.proto.collector.trace.v1.TraceService/Export";
    private const string MetricsGrpcServicePath = "opentelemetry.proto.collector.metrics.v1.MetricsService/Export";
    private const string LogsGrpcServicePath = "opentelemetry.proto.collector.logs.v1.LogsService/Export";

    private const string TraceHttpServicePath = "v1/traces";
    private const string MetricsHttpServicePath = "v1/metrics";
    private const string LogsHttpServicePath = "v1/logs";

#if NET462_OR_GREATER || NETSTANDARD2_0
    public static Channel CreateChannel(this OtlpExporterOptions options)
    {
        if (options.Endpoint.Scheme != Uri.UriSchemeHttp && options.Endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new NotSupportedException($"Endpoint URI scheme ({options.Endpoint.Scheme}) is not supported. Currently only \"http\" and \"https\" are supported.");
        }

        ChannelCredentials channelCredentials;
        if (options.Endpoint.Scheme == Uri.UriSchemeHttps)
        {
            if (!string.IsNullOrEmpty(options.ClientCertificateFile) && !string.IsNullOrEmpty(options.ClientKeyFile))
            {
                string clientCertPem = System.IO.File.ReadAllText(options.ClientCertificateFile);
                string clientKeyPem = System.IO.File.ReadAllText(options.ClientKeyFile);
                var keyPair = new KeyCertificatePair(clientCertPem, clientKeyPem);

                string? rootCertPem = null;
                if (!string.IsNullOrEmpty(options.CertificateFile))
                {
                    rootCertPem = System.IO.File.ReadAllText(options.CertificateFile);
                }

                channelCredentials = new SslCredentials(rootCertPem, keyPair);
            }
            else
            {
                string? rootCertPem = null;
                if (!string.IsNullOrEmpty(options.CertificateFile))
                {
                    rootCertPem = System.IO.File.ReadAllText(options.CertificateFile);
                }
                channelCredentials = new SslCredentials(rootCertPem);
            }
        }
        else
        {
            channelCredentials = ChannelCredentials.Insecure;
        }

        return new Channel(options.Endpoint.Authority, channelCredentials);
    }

    public static Metadata GetMetadataFromHeaders(this OtlpExporterOptions options) => options.GetHeaders<Metadata>((m, k, v) => m.Add(k, v));
#endif

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

        foreach (var header in OtlpExporterOptions.StandardHeaders)
        {
            addHeader(headers, header.Key, header.Value);
        }

        return headers;
    }

    public static OtlpExporterTransmissionHandler GetExportTransmissionHandler(this OtlpExporterOptions options, ExperimentalOptions experimentalOptions, OtlpSignalType otlpSignalType)
    {
        var exportClient = GetExportClient(options, otlpSignalType);

        // `HttpClient.Timeout.TotalMilliseconds` would be populated with the correct timeout value for both the exporter configuration cases:
        // 1. User provides their own HttpClient. This case is straightforward as the user wants to use their `HttpClient` and thereby the same client's timeout value.
        // 2. If the user configures timeout via the exporter options, then the timeout set for the `HttpClient` initialized by the exporter will be set to user provided value.
        double timeoutMilliseconds = exportClient is OtlpHttpExportClient httpTraceExportClient
            ? httpTraceExportClient.HttpClient.Timeout.TotalMilliseconds
            : options.TimeoutMilliseconds;

        if (experimentalOptions.EnableInMemoryRetry)
        {
            return new OtlpExporterRetryTransmissionHandler(exportClient, timeoutMilliseconds);
        }
        else if (experimentalOptions.EnableDiskRetry)
        {
            Debug.Assert(!string.IsNullOrEmpty(experimentalOptions.DiskRetryDirectoryPath), $"{nameof(experimentalOptions.DiskRetryDirectoryPath)} is null or empty");

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
        var httpClient = options.HttpClientFactory?.Invoke() ?? throw new InvalidOperationException("OtlpExporterOptions was missing HttpClientFactory or it returned null.");

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

                                return client;
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