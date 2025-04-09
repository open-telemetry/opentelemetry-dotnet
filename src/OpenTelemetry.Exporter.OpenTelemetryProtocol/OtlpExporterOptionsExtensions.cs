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
    // These methods are conditionally compiled for platforms that support gRPC.
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
                string clientCertPem = File.ReadAllText(options.ClientCertificateFile);
                string clientKeyPem = File.ReadAllText(options.ClientKeyFile);
                var keyPair = new KeyCertificatePair(clientCertPem, clientKeyPem);

                string rootCertPem = string.Empty;
                if (!string.IsNullOrEmpty(options.CertificateFile))
                {
                    rootCertPem = File.ReadAllText(options.CertificateFile);
                }

                channelCredentials = new SslCredentials(rootCertPem, keyPair);
            }
            else
            {
                string rootCertPem = string.Empty;
                if (!string.IsNullOrEmpty(options.CertificateFile))
                {
                    rootCertPem = File.ReadAllText(options.CertificateFile);
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

    public static Metadata GetMetadataFromHeaders(this OtlpExporterOptions options) =>
        options.GetHeaders<Metadata>((m, k, v) => m.Add(k, v));
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
#pragma warning restore CS0618
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
                            HttpClient? client = (HttpClient
