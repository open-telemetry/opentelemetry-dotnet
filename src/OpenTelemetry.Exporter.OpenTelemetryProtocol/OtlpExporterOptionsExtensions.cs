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

    public static IReadOnlyDictionary<string, string> GetHeaders(this OtlpExporterOptions options)
    {
        var optionHeaders = options.Headers;
        var headers = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(optionHeaders))
        {
            // According to the specification, URL-encoded headers must be supported.
            optionHeaders = Uri.UnescapeDataString(optionHeaders);
            ReadOnlySpan<char> headersSpan = optionHeaders.AsSpan();

            var nextEqualIndex = headersSpan.IndexOf('=');

            if (nextEqualIndex == -1)
            {
                throw CreateInvalidHeaderFormatException();
            }

            // Skip any leading commas.
            var leadingCommaIndex = headersSpan.Slice(0, nextEqualIndex).LastIndexOf(',');
            if (leadingCommaIndex != -1)
            {
                headersSpan = headersSpan.Slice(leadingCommaIndex + 1);
                nextEqualIndex -= leadingCommaIndex + 1;
            }

            while (!headersSpan.IsEmpty)
            {
                var key = headersSpan.Slice(0, nextEqualIndex).Trim().ToString();

                // HTTP header field-names can not be empty: https://www.rfc-editor.org/rfc/rfc7230#section-3.2
                if (key.Length < 1)
                {
                    throw CreateInvalidHeaderFormatException();
                }

                headersSpan = headersSpan.Slice(nextEqualIndex + 1);

                nextEqualIndex = headersSpan.IndexOf('=');

                string value;
                if (nextEqualIndex == -1)
                {
                    // Everything until the end of the string can be considered the value.
                    value = headersSpan.TrimEnd(',').Trim().ToString();
                    headersSpan = [];
                }
                else
                {
                    // If we have another = we need to backtrack from it
                    // and try to find the last comma and consider that as the delimiter.
                    var potentialValue = headersSpan.Slice(0, nextEqualIndex);
                    var lastComma = potentialValue.LastIndexOf(',');

                    if (lastComma == -1)
                    {
                        throw CreateInvalidHeaderFormatException();
                    }

                    potentialValue = potentialValue.Slice(0, lastComma);

                    value = potentialValue.TrimEnd(',').Trim().ToString();
                    headersSpan = headersSpan.Slice(lastComma + 1);
                    nextEqualIndex -= potentialValue.Length + 1;
                }

                headers.Add(key, value);
            }
        }

        foreach (var header in OtlpExporterOptions.StandardHeaders)
        {
            headers.Add(header.Key, header.Value);
        }

        return headers;
    }

    private static ArgumentException CreateInvalidHeaderFormatException()
        => new("Headers provided in an invalid format. Use: header=value,header=value");

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

#if NET || NETSTANDARD2_1_OR_GREATER
        if (absoluteUri.EndsWith('/'))
#else
        if (absoluteUri.EndsWith("/", StringComparison.Ordinal))
#endif
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
