// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Reflection;
using Grpc.Core;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
#if NETSTANDARD2_1 || NET6_0_OR_GREATER
using Grpc.Net.Client;
#endif
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using LogOtlpCollector = OpenTelemetry.Proto.Collector.Logs.V1;
using MetricsOtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;
using TraceOtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter;

internal static class OtlpExporterOptionsExtensions
{
#if NETSTANDARD2_1 || NET6_0_OR_GREATER
    public static GrpcChannel CreateChannel(this OtlpExporterOptions options)
#else
    public static Channel CreateChannel(this OtlpExporterOptions options)
#endif
    {
        if (options.Endpoint.Scheme != Uri.UriSchemeHttp && options.Endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new NotSupportedException($"Endpoint URI scheme ({options.Endpoint.Scheme}) is not supported. Currently only \"http\" and \"https\" are supported.");
        }

#if NETSTANDARD2_1 || NET6_0_OR_GREATER
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

    public static OtlpExporterTransmissionHandler<TraceOtlpCollector.ExportTraceServiceRequest> GetTraceExportTransmissionHandler(this OtlpExporterOptions options, ExperimentalOptions experimentalOptions)
    {
        var exportClient = GetTraceExportClient(options);

        // `HttpClient.Timeout.TotalMilliseconds` would be populated with the correct timeout value for both the exporter configuration cases:
        // 1. User provides their own HttpClient. This case is straightforward as the user wants to use their `HttpClient` and thereby the same client's timeout value.
        // 2. If the user configures timeout via the exporter options, then the timeout set for the `HttpClient` initialized by the exporter will be set to user provided value.
        double timeoutMilliseconds = exportClient is OtlpHttpTraceExportClient httpTraceExportClient
            ? httpTraceExportClient.HttpClient.Timeout.TotalMilliseconds
            : options.TimeoutMilliseconds;

        return experimentalOptions.EnableInMemoryRetry
            ? new OtlpExporterRetryTransmissionHandler<TraceOtlpCollector.ExportTraceServiceRequest>(exportClient, timeoutMilliseconds)
            : new OtlpExporterTransmissionHandler<TraceOtlpCollector.ExportTraceServiceRequest>(exportClient, timeoutMilliseconds);
    }

    public static OtlpExporterTransmissionHandler<MetricsOtlpCollector.ExportMetricsServiceRequest> GetMetricsExportTransmissionHandler(this OtlpExporterOptions options, ExperimentalOptions experimentalOptions)
    {
        var exportClient = GetMetricsExportClient(options);

        // `HttpClient.Timeout.TotalMilliseconds` would be populated with the correct timeout value for both the exporter configuration cases:
        // 1. User provides their own HttpClient. This case is straightforward as the user wants to use their `HttpClient` and thereby the same client's timeout value.
        // 2. If the user configures timeout via the exporter options, then the timeout set for the `HttpClient` initialized by the exporter will be set to user provided value.
        double timeoutMilliseconds = exportClient is OtlpHttpMetricsExportClient httpMetricsExportClient
            ? httpMetricsExportClient.HttpClient.Timeout.TotalMilliseconds
            : options.TimeoutMilliseconds;

        return experimentalOptions.EnableInMemoryRetry
            ? new OtlpExporterRetryTransmissionHandler<MetricsOtlpCollector.ExportMetricsServiceRequest>(exportClient, timeoutMilliseconds)
            : new OtlpExporterTransmissionHandler<MetricsOtlpCollector.ExportMetricsServiceRequest>(exportClient, timeoutMilliseconds);
    }

    public static OtlpExporterTransmissionHandler<LogOtlpCollector.ExportLogsServiceRequest> GetLogsExportTransmissionHandler(this OtlpExporterOptions options, ExperimentalOptions experimentalOptions)
    {
        var exportClient = GetLogExportClient(options);
        double timeoutMilliseconds = exportClient is OtlpHttpLogExportClient httpLogExportClient
            ? httpLogExportClient.HttpClient.Timeout.TotalMilliseconds
            : options.TimeoutMilliseconds;

        return experimentalOptions.EnableInMemoryRetry
            ? new OtlpExporterRetryTransmissionHandler<LogOtlpCollector.ExportLogsServiceRequest>(exportClient, timeoutMilliseconds)
            : new OtlpExporterTransmissionHandler<LogOtlpCollector.ExportLogsServiceRequest>(exportClient, timeoutMilliseconds);
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

    public static IExportClient<LogOtlpCollector.ExportLogsServiceRequest> GetLogExportClient(this OtlpExporterOptions options) =>
        options.Protocol switch
        {
            OtlpExportProtocol.Grpc => new OtlpGrpcLogExportClient(options),
            OtlpExportProtocol.HttpProtobuf => new OtlpHttpLogExportClient(
                options,
                options.HttpClientFactory?.Invoke() ?? throw new InvalidOperationException("OtlpExporterOptions was missing HttpClientFactory or it returned null.")),
            _ => throw new NotSupportedException($"Protocol {options.Protocol} is not supported."),
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
