// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>Base class for sending OTLP export request over HTTP.</summary>
/// <typeparam name="TRequest">Type of export request.</typeparam>
internal abstract class BaseOtlpHttpExportClient<TRequest> : IExportClient<TRequest>
{
    private static readonly ExportClientHttpResponse SuccessExportResponse = new ExportClientHttpResponse(success: true, deadlineUtc: default, response: null, exception: null);
#if NET
    private readonly bool synchronousSendSupportedByCurrentPlatform;
#endif

    protected BaseOtlpHttpExportClient(OtlpExporterOptions options, HttpClient httpClient, string signalPath)
    {
        Guard.ThrowIfNull(options);
        Guard.ThrowIfNull(httpClient);
        Guard.ThrowIfNull(signalPath);
        Guard.ThrowIfInvalidTimeout(options.TimeoutMilliseconds);

        Uri exporterEndpoint = options.AppendSignalPathToEndpoint
            ? options.Endpoint.AppendPathIfNotPresent(signalPath)
            : options.Endpoint;
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

    /// <inheritdoc/>
    public ExportClientResponse SendExportRequest(TRequest request, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = this.CreateHttpRequest(request);

            using var httpResponse = this.SendHttpRequest(httpRequest, cancellationToken);

            try
            {
                httpResponse.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException ex)
            {
                return new ExportClientHttpResponse(success: false, deadlineUtc: deadlineUtc, response: httpResponse, ex);
            }

            // We do not need to return back response and deadline for successful response so using cached value.
            return SuccessExportResponse;
        }
        catch (HttpRequestException ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);

            return new ExportClientHttpResponse(success: false, deadlineUtc: deadlineUtc, response: null, exception: ex);
        }
    }

    /// <inheritdoc/>
    public bool Shutdown(int timeoutMilliseconds)
    {
        this.HttpClient.CancelPendingRequests();
        return true;
    }

    protected abstract HttpContent CreateHttpContent(TRequest exportRequest);

    protected HttpRequestMessage CreateHttpRequest(TRequest exportRequest)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, this.Endpoint);
        foreach (var header in this.Headers)
        {
            request.Headers.Add(header.Key, header.Value);
        }

        request.Content = this.CreateHttpContent(exportRequest);

        return request;
    }

    protected HttpResponseMessage SendHttpRequest(HttpRequestMessage request, CancellationToken cancellationToken)
    {
#if NET
        return this.synchronousSendSupportedByCurrentPlatform
        ? this.HttpClient.Send(request, cancellationToken)
        : this.HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult();
#else
        return this.HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult();
#endif
    }
}
