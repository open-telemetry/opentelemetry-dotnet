// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Http.Headers;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;
#if NET8_0_OR_GREATER
using Grpc.Core;
#endif

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>Base class for sending OTLP export request over gRPC.</summary>
internal sealed class OtlpGrpcExportClient : OtlpExportClient
{
    public const string GrpcStatusDetailsHeader = "grpc-status-details-bin";
    private static readonly ExportClientHttpResponse SuccessExportResponse = new(success: true, deadlineUtc: default, response: null, exception: null);
    private static readonly MediaTypeHeaderValue MediaHeaderValue = new("application/grpc");

    private static readonly ExportClientGrpcResponse DefaultExceptionExportClientGrpcResponse
        = new(
            success: false,
            deadlineUtc: default,
            exception: null,
            status: null,
            grpcStatusDetailsHeader: null);

#if NET8_0_OR_GREATER
    private readonly ChannelBase? secureChannel;
    private readonly bool useMtls;
#endif

    public OtlpGrpcExportClient(OtlpExporterOptions options, HttpClient httpClient, string signalPath)
        : base(options, httpClient, signalPath)
    {
#if NET8_0_OR_GREATER
        // Determine if we should use mTLS based on certificate configuration and endpoint
        this.useMtls = options.Endpoint.Scheme == Uri.UriSchemeHttps &&
            (!string.IsNullOrEmpty(options.CertificateFilePath) ||
            (!string.IsNullOrEmpty(options.ClientCertificateFilePath) && !string.IsNullOrEmpty(options.ClientKeyFilePath)));

        // Create a secure channel if mTLS is enabled
        if (this.useMtls)
        {
            try
            {
                this.secureChannel = options.CreateSecureChannel();
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.MTlsCertificateLoadError(ex);
                this.secureChannel = null;
                this.useMtls = false;
            }
        }
#endif
    }

    internal override MediaTypeHeaderValue MediaTypeHeader => MediaHeaderValue;

    internal override bool RequireHttp2 => true;

    /// <inheritdoc/>
    public override ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        // Use the secure channel for mTLS if available
        if (this.useMtls && this.secureChannel != null)
        {
            try
            {
                // Create a deadline for the gRPC call
                var deadline = DateTime.UtcNow.AddMilliseconds(this.Options.TimeoutMilliseconds);

                // Create a gRPC client call
                var call = new CallOptions(deadline: deadline, cancellationToken: cancellationToken);

                // Create a gRPC method
                var method = new Method<byte[], byte[]>(
                    MethodType.Unary,
                    this.ExportServicePath,
                    "ProtoMethod",
                    Marshallers.Create(x => x, x => x));

                // Call the gRPC service
                var response = this.secureChannel.CreateCallInvoker().BlockingUnaryCall(method, null, call, buffer);

                OpenTelemetryProtocolExporterEventSource.Log.ExportSuccess(this.Endpoint.ToString(), "mTLS gRPC export completed successfully.");
                return SuccessExportResponse;
            }
            catch (RpcException ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.ExportFailure(this.Endpoint, "mTLS gRPC export failed.", new Status(ex.StatusCode, ex.Message));

                return new ExportClientGrpcResponse(
                    success: false,
                    deadlineUtc: deadlineUtc,
                    exception: ex,
                    status: new Status(ex.StatusCode, ex.Message),
                    grpcStatusDetailsHeader: null);
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);
                return DefaultExceptionExportClientGrpcResponse;
            }
        }
#endif

        // Fallback to HTTP-based gRPC
        try
        {
            using var httpRequest = this.CreateHttpRequest(buffer, contentLength);
            using var httpResponse = this.SendHttpRequest(httpRequest, cancellationToken);

            httpResponse.EnsureSuccessStatusCode();

            var trailingHeaders = httpResponse.TrailingHeaders();
            Status status = GrpcProtocolHelpers.GetResponseStatus(httpResponse, trailingHeaders);

            if (status.Detail.Equals(Status.NoReplyDetailMessage))
            {
#if NET
                using var responseStream = httpResponse.Content.ReadAsStream(cancellationToken);
#else
                using var responseStream = httpResponse.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
#endif
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

            OpenTelemetryProtocolExporterEventSource.Log.ExportFailure(this.Endpoint, "Export failed due to unexpected status code.", status);

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

    /// <inheritdoc/>
    public override bool Shutdown(int timeoutMilliseconds)
    {
#if NET8_0_OR_GREATER
        if (this.secureChannel != null)
        {
            try
            {
                this.secureChannel.ShutdownAsync().Wait(timeoutMilliseconds);
            }
            catch
            {
                // Ignore shutdown errors
            }
        }
#endif
        return base.Shutdown(timeoutMilliseconds);
    }

    private static bool IsTransientNetworkError(HttpRequestException ex)
    {
        return ex.InnerException is System.Net.Sockets.SocketException socketEx
            && (socketEx.SocketErrorCode == System.Net.Sockets.SocketError.TimedOut
                || socketEx.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset
                || socketEx.SocketErrorCode == System.Net.Sockets.SocketError.HostUnreachable);
    }
}
