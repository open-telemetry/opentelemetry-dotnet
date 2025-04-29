// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Http.Headers;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;

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
    private readonly HttpClient? secureClient;
    private readonly bool useMtls;
    private readonly OtlpExporterOptions options;
    private readonly string signalPath;
    private bool disposed;
#endif

    internal override MediaTypeHeaderValue MediaTypeHeader => MediaHeaderValue;

    internal override bool RequireHttp2 => true;

    public OtlpGrpcExportClient(OtlpExporterOptions options, HttpClient httpClient, string signalPath)
        : base(options, httpClient, signalPath)
    {
#if NET8_0_OR_GREATER
        this.options = options;
        this.signalPath = signalPath;

        // Determine if we should use mTLS based on certificate configuration and endpoint
        this.useMtls = options.Endpoint.Scheme == Uri.UriSchemeHttps &&
            (!string.IsNullOrEmpty(options.CertificateFilePath) ||
            (!string.IsNullOrEmpty(options.ClientCertificateFilePath) && !string.IsNullOrEmpty(options.ClientKeyFilePath)));

        // Create a secure client if mTLS is enabled
        if (this.useMtls)
        {
            try
            {
                var handler = new HttpClientHandler
                {
                    CheckCertificateRevocationList = true,
                };

                if (!string.IsNullOrEmpty(options.CertificateFilePath))
                {
                    using var trustedCertificate = MtlsUtility.LoadCertificateWithValidation(options.CertificateFilePath);
                    handler.ServerCertificateCustomValidationCallback = (_, cert, __, unexpectedErrors) =>
                    {
                        return cert?.Thumbprint == trustedCertificate.Thumbprint;
                    };
                }

                if (!string.IsNullOrEmpty(options.ClientCertificateFilePath) && !string.IsNullOrEmpty(options.ClientKeyFilePath))
                {
                    var clientCertificate = MtlsUtility.LoadCertificateWithValidation(
                        options.ClientCertificateFilePath,
                        options.ClientKeyFilePath);
                    handler.ClientCertificates.Add(clientCertificate);
                }

                this.secureClient = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromMilliseconds(options.TimeoutMilliseconds),
                };
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.MtlsCertificateLoadError(ex);
                this.secureClient = null;
                this.useMtls = false;
            }
        }
#endif
    }

    private static bool IsTransientNetworkError(HttpRequestException ex)
    {
        return ex.InnerException is System.Net.Sockets.SocketException socketEx
            && (socketEx.SocketErrorCode == System.Net.Sockets.SocketError.TimedOut
                || socketEx.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset
                || socketEx.SocketErrorCode == System.Net.Sockets.SocketError.HostUnreachable);
    }

    /// <inheritdoc />
    public override ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
#if NET8_0_OR_GREATER
        if (this.useMtls && this.secureClient != null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, this.Endpoint)
                {
                    Content = new ByteArrayContent(buffer, 0, contentLength)
                    {
                        Headers =
                        {
                            ContentType = this.MediaTypeHeader,
                        },
                    },
                };

                using var response = this.secureClient.SendAsync(request, cancellationToken).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();

                OpenTelemetryProtocolExporterEventSource.Log.ExportSuccess(this.Endpoint.ToString(), "mTLS export completed successfully.");
                return SuccessExportResponse;
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);
                return DefaultExceptionExportClientGrpcResponse;
            }
        }
#endif

        try
        {
            using var httpRequest = this.CreateHttpRequest(buffer, contentLength);
            using var httpResponse = this.SendHttpRequest(httpRequest, cancellationToken);

            httpResponse.EnsureSuccessStatusCode();

            var trailingHeaders = httpResponse.TrailingHeaders();
            var status = GrpcProtocolHelpers.GetResponseStatus(httpResponse, trailingHeaders);

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

    /// <summary>
    /// Shuts down the exporter and cleans up any resources.
    /// </summary>
    /// <param name="timeoutMilliseconds">The maximum time to wait for the shutdown to complete.</param>
    /// <returns>True if shutdown succeeded. False otherwise.</returns>
    public new bool Shutdown(int timeoutMilliseconds)
    {
#if NET8_0_OR_GREATER
        if (this.secureClient != null)
        {
            try
            {
                this.secureClient.Dispose();
            }
            catch
            {
                // Ignore shutdown errors
            }
        }
#endif
        return base.Shutdown(timeoutMilliseconds);
    }

    /// <summary>
    /// Releases all resources used by the current instance.
    /// </summary>
    public void Dispose()
    {
#if NET8_0_OR_GREATER
        this.Dispose(true);
        GC.SuppressFinalize(this);
#endif
    }

#if NET8_0_OR_GREATER
    private void Dispose(bool disposing)
    {
        if (!this.disposed)
        {
            if (disposing)
            {
                this.secureClient?.Dispose();
            }

            this.disposed = true;
        }
    }
#endif
}
