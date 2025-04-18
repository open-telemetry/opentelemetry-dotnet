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
#if NET8_0_OR_GREATER
using System.Security.Cryptography.X509Certificates;
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

#if NET8_0_OR_GREATER
    /// <summary>
    /// Creates a channel with mTLS support if certificate files are provided.
    /// </summary>
    /// <param name="options">The OTLP exporter options.</param>
    /// <returns>A gRPC channel.</returns>
    public static ChannelBase CreateSecureChannel(this OtlpExporterOptions options)
    {
        if (options.Endpoint.Scheme != Uri.UriSchemeHttp && options.Endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new NotSupportedException($"Endpoint URI scheme ({options.Endpoint.Scheme}) is not supported. Currently only \"http\" and \"https\" are supported.");
        }

        try
        {
            // For insecure connections or when no certificates are provided
            if (options.Endpoint.Scheme == Uri.UriSchemeHttp ||
                (string.IsNullOrEmpty(options.CertificateFilePath) &&
                 (string.IsNullOrEmpty(options.ClientCertificateFilePath) || string.IsNullOrEmpty(options.ClientKeyFilePath))))
            {
                return new Channel(options.Endpoint.Authority, ChannelCredentials.Insecure);
            }

            // For secure connections with mTLS
            SslCredentials sslCredentials;

            // Load certificates only if file paths are provided
            try
            {
                string? rootCertPem = null;
                KeyCertificatePair? clientCerts = null;

                // Load server certificate for validation if provided
                if (!string.IsNullOrEmpty(options.CertificateFilePath))
                {
                    try
                    {
                        var trustedCertificate = MTlsUtility.LoadCertificateWithValidation(options.CertificateFilePath);
                        rootCertPem = File.ReadAllText(options.CertificateFilePath);
                        OpenTelemetryProtocolExporterEventSource.Log.MTlsConfigurationSuccess("gRPC server validation");
                    }
                    catch (Exception ex)
                    {
                        OpenTelemetryProtocolExporterEventSource.Log.MTlsCertificateLoadError(ex);
                    }
                }

                // Load client certificate and key if both are provided
                if (!string.IsNullOrEmpty(options.ClientCertificateFilePath) &&
                    !string.IsNullOrEmpty(options.ClientKeyFilePath))
                {
                    try
                    {
                        var clientCertificate = MTlsUtility.LoadCertificateWithValidation(
                            options.ClientCertificateFilePath,
                            options.ClientKeyFilePath);

                        string clientCertPem = File.ReadAllText(options.ClientCertificateFilePath);
                        string clientKeyPem = File.ReadAllText(options.ClientKeyFilePath);
                        clientCerts = new KeyCertificatePair(clientCertPem, clientKeyPem);

                        OpenTelemetryProtocolExporterEventSource.Log.MTlsConfigurationSuccess("gRPC client authentication");
                    }
                    catch (Exception ex)
                    {
                        OpenTelemetryProtocolExporterEventSource.Log.MTlsCertificateLoadError(ex);
                    }
                }

                // Create SSL credentials with the loaded certificates
                sslCredentials = clientCerts != null
                    ? new SslCredentials(rootCertPem, clientCerts)
                    : new SslCredentials(rootCertPem);
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.MTlsCertificateLoadError(ex);
                return new Channel(options.Endpoint.Authority, ChannelCredentials.Insecure);
            }

            return new Channel(options.Endpoint.Authority, sslCredentials);
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.MTlsCertificateLoadError(ex);
            return new Channel(options.Endpoint.Authority, ChannelCredentials.Insecure);
        }
    }
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
            ReadOnlySpan<char> headersSpan = optionHeaders.AsSpan();

            while (!headersSpan.IsEmpty)
            {
                int commaIndex = headersSpan.IndexOf(',');
                ReadOnlySpan<char> pair;
                if (commaIndex == -1)
                {
                    pair = headersSpan;
                    headersSpan = ReadOnlySpan<char>.Empty;
                }
                else
                {
                    pair = headersSpan.Slice(0, commaIndex);
                    headersSpan = headersSpan.Slice(commaIndex + 1);
                }

                int equalIndex = pair.IndexOf('=');
                if (equalIndex == -1)
                {
                    throw new ArgumentException("Headers provided in an invalid format.");
                }

                var key = pair.Slice(0, equalIndex).Trim().ToString();
                var value = pair.Slice(equalIndex + 1).Trim().ToString();
                addHeader(headers, key, value);
            }
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

#pragma warning disable CS0618 // Suppressing gRPC obsolete warning
        if (options.Protocol != OtlpExportProtocol.Grpc && options.Protocol != OtlpExportProtocol.HttpProtobuf)
        {
            throw new NotSupportedException($"Protocol {options.Protocol} is not supported.");
        }

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
#pragma warning restore CS0618 // Suppressing gRPC obsolete warning
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
                            [typeof(string)],
                            modifiers: null);
                        if (createClientMethod != null)
                        {
                            HttpClient? client = (HttpClient?)createClientMethod.Invoke(httpClientFactory, [httpClientName]);

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
