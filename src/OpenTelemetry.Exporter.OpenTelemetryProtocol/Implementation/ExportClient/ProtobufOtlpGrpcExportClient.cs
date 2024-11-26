// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Http.Headers;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>Base class for sending OTLP export request over gRPC.</summary>
internal sealed class ProtobufOtlpGrpcExportClient : IProtobufExportClient
{
    public const string GrpcStatusDetailsHeader = "grpc-status-details-bin";
    private static readonly ExportClientHttpResponse SuccessExportResponse = new(success: true, deadlineUtc: default, response: null, exception: null);
    private static readonly MediaTypeHeaderValue MediaHeaderValue = new("application/grpc");
    private static readonly Version Http2RequestVersion = new(2, 0);

    private static readonly ExportClientGrpcResponse DefaultExceptionExportClientGrpcResponse
        = new(
            success: false,
            deadlineUtc: default,
            exception: null,
            status: null,
            grpcStatusDetailsHeader: null);

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
            catch (HttpRequestException)
            {
                throw;
            }

            var trailingHeaders = httpResponse.TrailingHeaders();
            Status status = GrpcProtocolHelpers.GetResponseStatus(httpResponse, trailingHeaders);

            if (status.Detail.Equals(Status.NoReplyDetailMessage))
            {
                using var responseStream = httpResponse.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                int firstByte = responseStream.ReadByte();

                if (firstByte == -1)
                {
                    if (status.StatusCode == StatusCode.OK)
                    {
                        status = new Status(StatusCode.Internal, "Failed to deserialize response message.");
                    }

                    OpenTelemetryProtocolExporterEventSource.Log.ResponseDeserializationFailed(this.Endpoint.ToString());

                    return new ExportClientGrpcResponse(
                        success: false,
                        deadlineUtc: deadlineUtc,
                        exception: null,
                        status: status,
                        grpcStatusDetailsHeader: null);
                }

                // Note: Trailing headers might not be fully available until the
                // response stream is consumed. gRPC often sends critical
                // information like error details or final statuses in trailing
                // headers which can only be reliably accessed after reading
                // the response body.
                trailingHeaders = httpResponse.TrailingHeaders();
                status = GrpcProtocolHelpers.GetResponseStatus(httpResponse, trailingHeaders);
            }

            if (status.StatusCode == StatusCode.OK)
            {
                OpenTelemetryProtocolExporterEventSource.Log.ExportSuccess(this.Endpoint.ToString(), "Export completed successfully.");
                return SuccessExportResponse;
            }

            string? grpcStatusDetailsHeader = null;
            if (status.StatusCode == StatusCode.ResourceExhausted || status.StatusCode == StatusCode.Unavailable)
            {
                grpcStatusDetailsHeader = GrpcProtocolHelpers.GetHeaderValue(trailingHeaders, GrpcStatusDetailsHeader);
            }

            OpenTelemetryProtocolExporterEventSource.Log.ExportFailure(this.Endpoint.ToString(), "Export failed due to unexpected status code.");

            return new ExportClientGrpcResponse(
                success: false,
                deadlineUtc: deadlineUtc,
                exception: null,
                status: status,
                grpcStatusDetailsHeader: grpcStatusDetailsHeader);
        }
        catch (HttpRequestException ex) when (ex.InnerException is TimeoutException || IsTransientNetworkError(ex))
        {
            // Handle transient HTTP errors (retryable)
            OpenTelemetryProtocolExporterEventSource.Log.TransientHttpError(this.Endpoint, ex);
            return new ExportClientGrpcResponse(
                success: false,
                deadlineUtc: deadlineUtc,
                exception: ex,
                status: new Status(StatusCode.Unavailable, "Transient HTTP error - retryable"),
                grpcStatusDetailsHeader: null);
        }
        catch (HttpRequestException ex)
        {
            // Handle non-retryable HTTP errors.
            OpenTelemetryProtocolExporterEventSource.Log.HttpRequestFailed(this.Endpoint, ex);
            return new ExportClientGrpcResponse(
                success: false,
                deadlineUtc: deadlineUtc,
                exception: ex,
                status: null,
                grpcStatusDetailsHeader: null);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Handle unexpected cancellation.
            OpenTelemetryProtocolExporterEventSource.Log.OperationUnexpectedlyCanceled(this.Endpoint, ex);
            return new ExportClientGrpcResponse(
                success: false,
                deadlineUtc: deadlineUtc,
                exception: ex,
                status: new Status(StatusCode.Cancelled, "Operation was canceled unexpectedly."),
                grpcStatusDetailsHeader: null);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            // Handle TaskCanceledException caused by TimeoutException.
            OpenTelemetryProtocolExporterEventSource.Log.RequestTimedOut(this.Endpoint, ex);
            return new ExportClientGrpcResponse(
                success: false,
                deadlineUtc: deadlineUtc,
                exception: ex,
                status: new Status(StatusCode.DeadlineExceeded, "Request timed out."),
                grpcStatusDetailsHeader: null);
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);
            return DefaultExceptionExportClientGrpcResponse;
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

    private static bool IsTransientNetworkError(HttpRequestException ex)
    {
        return ex.InnerException is System.Net.Sockets.SocketException socketEx &&
               (socketEx.SocketErrorCode == System.Net.Sockets.SocketError.TimedOut ||
                socketEx.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset ||
                socketEx.SocketErrorCode == System.Net.Sockets.SocketError.HostUnreachable);
    }
}
