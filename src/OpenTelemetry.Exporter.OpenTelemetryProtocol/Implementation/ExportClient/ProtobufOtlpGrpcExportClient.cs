// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Http.Headers;
using Grpc.Core;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>Base class for sending OTLP export request over gRPC.</summary>
internal sealed class ProtobufOtlpGrpcExportClient : IProtobufExportClient
{
    private static readonly ExportClientHttpResponse SuccessExportResponse = new(success: true, deadlineUtc: default, response: null, exception: null);
    private static readonly MediaTypeHeaderValue MediaHeaderValue = new("application/grpc");
    private static readonly Version Http2RequestVersion = new(2, 0);
#if NET
    private readonly bool synchronousSendSupportedByCurrentPlatform;
#endif

    public ProtobufOtlpGrpcExportClient(OtlpExporterOptions options, HttpClient httpClient, string signalPath)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNull(httpClient);
        Guard.ThrowIfNull(signalPath);
        Guard.ThrowIfInvalidTimeout(options.TimeoutMilliseconds);

        Uri exporterEndpoint = options.Endpoint.AppendPathIfNotPresent(signalPath);
        this.Endpoint = new UriBuilder(exporterEndpoint).Uri;
        this.Headers = options.GetHeaders<Dictionary<string, string>>((d, k, v) => d.Add(k, v));
        this.HttpClient = httpClient;

#if NET
        // See: https://github.com/dotnet/runtime/blob/280f2a0c60ce0378b8db49adc0eecc463d00fe5d/src/libraries/System.Net.Http/src/System/Net/Http/HttpClientHandler.AnyMobile.cs#L767
        this.synchronousSendSupportedByCurrentPlatform = !OperatingSystem.IsAndroid()
            && !OperatingSystem.IsIOS()
            && !OperatingSystem.IsTvOS()
            && !OperatingSystem.IsBrowser();
#endif
    }

    internal HttpClient HttpClient { get; }

    internal Uri Endpoint { get; set; }

    internal IReadOnlyDictionary<string, string> Headers { get; }

    internal int TimeoutMilliseconds { get; }

    /// <inheritdoc/>
    public ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = this.CreateHttpRequest(buffer, contentLength);

            using var httpResponse = this.SendHttpRequest(httpRequest, cancellationToken);

            try
            {
                httpResponse.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                return new ExportClientHttpResponse(success: false, deadlineUtc: deadlineUtc, response: httpResponse, ex);
            }

            // TODO: Hande retries & failures.
            return SuccessExportResponse;
        }
        catch (RpcException ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);
            return new ExportClientGrpcResponse(success: false, deadlineUtc: deadlineUtc, exception: ex);
        }
    }

    public HttpRequestMessage CreateHttpRequest(byte[] buffer, int contentLength)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, this.Endpoint);
        request.Version = Http2RequestVersion;

#if NET6_0_OR_GREATER
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
#endif

        foreach (var header in this.Headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }

        // TODO: Support compression.

        request.Content = new ByteArrayContent(buffer, 0, contentLength);
        request.Content.Headers.ContentType = MediaHeaderValue;

        return request;
    }

    public HttpResponseMessage SendHttpRequest(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return this.HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public bool Shutdown(int timeoutMilliseconds)
    {
        this.HttpClient.CancelPendingRequests();
        return true;
    }
}
