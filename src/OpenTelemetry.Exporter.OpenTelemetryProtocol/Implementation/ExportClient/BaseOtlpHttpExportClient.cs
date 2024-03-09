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
    }

    internal HttpClient HttpClient { get; }

    internal Uri Endpoint { get; set; }

    internal IReadOnlyDictionary<string, string> Headers { get; }

    /// <inheritdoc/>
    public ExportClientResponse SendExportRequest(TRequest request, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        // `HttpClient.Timeout.TotalMilliseconds` would be populated with the correct timeout value for both the exporter configuration cases:
        // 1. User provides their own HttpClient. This case is straightforward as the user wants to use their `HttpClient` and thereby the same client's timeout value.
        // 2. If the user configures timeout via the exporter options, then the timeout set for the `HttpClient` initialized by the exporter will be set to user provided value.
        // DateTime deadline = DateTime.UtcNow.AddMilliseconds(this.HttpClient.Timeout.TotalMilliseconds);
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
#if NET6_0_OR_GREATER
        return this.HttpClient.Send(request, cancellationToken);
#else
        return this.HttpClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult();
#endif
    }
}
