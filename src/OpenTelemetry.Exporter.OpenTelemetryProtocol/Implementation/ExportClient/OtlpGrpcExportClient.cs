// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Buffers.Binary;
using System.Diagnostics.Tracing;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Sockets;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>Base class for sending OTLP export request over gRPC.</summary>
internal sealed class OtlpGrpcExportClient : OtlpExportClient
{
    public const string GrpcStatusDetailsHeader = "grpc-status-details-bin";

    // A gRPC message frame header is 5 bytes:
    //   byte 0     - Compression flag (0 = not compressed, 1 = compressed).
    //   bytes 1-4  - Message length in big-endian format.
    private const int GrpcMessageHeaderSize = 5;

    private static readonly ExportClientHttpResponse SuccessExportResponse = new(success: true, deadlineUtc: default, response: null, exception: null);
    private static readonly MediaTypeHeaderValue MediaHeaderValue = new("application/grpc");

    private static readonly ExportClientGrpcResponse DefaultExceptionExportClientGrpcResponse
        = new(
            success: false,
            deadlineUtc: default,
            exception: null,
            status: null,
            grpcStatusDetailsHeader: null);

#if !NET
    private static readonly byte[] GrpcFrameHeader = [0, 0, 0, 0, 0];
#endif

    public OtlpGrpcExportClient(OtlpExporterOptions options, HttpClient httpClient, string signalPath)
        : base(options, httpClient, signalPath)
    {
    }

    internal override MediaTypeHeaderValue MediaTypeHeader => MediaHeaderValue;

    internal override bool RequireHttp2 => true;

    // We need the entire response content to ensure that the response trailers are received
    internal override HttpCompletionOption CompletionOption => HttpCompletionOption.ResponseContentRead;
    
#if NET
    // See https://vcsjones.dev/csharp-readonly-span-bytes-static/
    private static ReadOnlySpan<byte> GrpcFrameHeader => [0, 0, 0, 0, 0];
#endif

    /// <inheritdoc/>
    public override ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage? httpResponse = null;

        try
        {
            using var httpRequest = this.CreateHttpRequest(buffer, contentLength);

            // TE is required by some servers, e.g. C Core.
            // A missing TE header results in servers aborting the gRPC call.
            httpRequest.Headers.TryAddWithoutValidation("TE", "trailers");

            if (this.CompressionEnabled)
            {
                httpRequest.Headers.Remove("grpc-encoding");
                httpRequest.Headers.TryAddWithoutValidation("grpc-encoding", "gzip");
            }

            httpResponse = this.SendHttpRequest(httpRequest, cancellationToken);

            httpResponse.EnsureSuccessStatusCode();

            var trailingHeaders = httpResponse.TrailingHeaders();
            var status = GrpcProtocolHelpers.GetResponseStatus(httpResponse, trailingHeaders);

            if (status.Detail.Equals(Status.NoReplyDetailMessage, StringComparison.Ordinal))
            {
#if NET
                using var responseStream = httpResponse.Content.ReadAsStream(cancellationToken);
#else
                using var responseStream = httpResponse.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
#endif
                var firstByte = responseStream.ReadByte();

                if (firstByte == -1)
                {
                    if (status.StatusCode == StatusCode.OK)
                    {
                        status = new Status(StatusCode.Internal, "Failed to deserialize response message.");
                    }

                    OpenTelemetryProtocolExporterEventSource.Log.ResponseDeserializationFailed(this.Endpoint);

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
                OpenTelemetryProtocolExporterEventSource.Log.ExportSuccess(this.Endpoint, "Export completed successfully.");
                return SuccessExportResponse;
            }

            string? grpcStatusDetailsHeader = null;
            if (status.StatusCode is StatusCode.ResourceExhausted or StatusCode.Unavailable)
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
            if (OpenTelemetryProtocolExporterEventSource.Log.IsEnabled(EventLevel.Error, EventKeywords.All))
            {
                var response = TryGetResponseBody(httpResponse, cancellationToken);
                OpenTelemetryProtocolExporterEventSource.Log.HttpRequestFailed(this.Endpoint, response, ex);
            }

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
        finally
        {
            httpResponse?.Dispose();
        }
    }

    protected override HttpContent CreateHttpContent(byte[] buffer, int contentLength)
    {
        if (!this.CompressionEnabled)
        {
            return base.CreateHttpContent(buffer, contentLength);
        }

        // Build a gzip-compressed gRPC message frame:
        //   byte 0     - Compression flag = 1 (gzip).
        //   bytes 1-4  - Compressed payload length in big-endian format.
        //   bytes 5+   - Gzip-compressed protobuf payload.
        var compressedStream = new MemoryStream();

        // Reserve space for the gRPC frame header.
#if NET
        compressedStream.Write(GrpcFrameHeader);
#else
        compressedStream.Write(GrpcFrameHeader, 0, GrpcFrameHeader.Length);
#endif

        using (var gzipStream = new GZipStream(compressedStream, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzipStream.Write(buffer, GrpcMessageHeaderSize, contentLength - GrpcMessageHeaderSize);
        }

        var compressedPayloadLength = (uint)(compressedStream.Length - GrpcMessageHeaderSize);

        // Write the gRPC frame header: compression flag + big-endian payload length.
        compressedStream.Position = 0;
        compressedStream.WriteByte(1);

        var lengthBytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, compressedPayloadLength);
        compressedStream.Write(lengthBytes, 0, 4);

        compressedStream.Position = 0;

        var content = new StreamContent(compressedStream);
        content.Headers.ContentType = this.MediaTypeHeader;
        return content;
    }

    private static bool IsTransientNetworkError(HttpRequestException ex) =>
        ex.InnerException is SocketException { SocketErrorCode: SocketError.TimedOut or SocketError.ConnectionReset or SocketError.HostUnreachable or SocketError.ConnectionRefused };
}
