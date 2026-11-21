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

#if !NET
    private static readonly byte[] GrpcFrameHeader = [0, 0, 0, 0, 0];
#endif

    public OtlpGrpcExportClient(OtlpExporterOptions options, HttpClient httpClient, string signalPath, OtlpSignalType? signalType = null)
        : base(options, httpClient, signalPath, signalType)
    {
    }

    internal override MediaTypeHeaderValue MediaTypeHeader => MediaHeaderValue;

    internal override long HeaderBytesSize => GrpcMessageHeaderSize;

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
        Stream? responseStream = null;

        // The first byte of the response message frame, if it has already been read.
        var firstByte = -1;

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

            httpResponse = this.SendHttpRequest(httpRequest, deadlineUtc, cancellationToken);

            if (this.IsResponseTooLarge(httpResponse, out var responseTooLarge))
            {
                // Requests with responses that are too large must not be retried
                return new ExportClientGrpcResponse(
                    success: false,
                    deadlineUtc: deadlineUtc,
                    exception: responseTooLarge,
                    status: null,
                    grpcStatusDetailsHeader: null);
            }

            if (!httpResponse.IsSuccessStatusCode)
            {
                // Handle non-retryable HTTP errors.
                var ex = CreateStatusCodeException(httpResponse);

                if (OpenTelemetryProtocolExporterEventSource.Log.IsEnabled(EventLevel.Error, EventKeywords.All))
                {
                    // The body was buffered with the response, as CompletionOption is ResponseContentRead,
                    // so reading it cannot block and does not need to be bounded by the deadline.
                    var response = TryGetResponseBody(httpResponse, Math.Min(this.MaxResponseSizeBytes, MaxLoggedResponseSizeBytes), cancellationToken);
                    OpenTelemetryProtocolExporterEventSource.Log.HttpRequestFailed(this.Endpoint, response, ex);
                }

                return new ExportClientGrpcResponse(
                    success: false,
                    deadlineUtc: deadlineUtc,
                    exception: ex,
                    status: null,
                    grpcStatusDetailsHeader: null);
            }

            var trailingHeaders = httpResponse.TrailingHeaders();
            var status = GrpcProtocolHelpers.GetResponseStatus(httpResponse, trailingHeaders);

            if (string.Equals(status.Detail, Status.NoReplyDetailMessage, StringComparison.Ordinal))
            {
                responseStream = OpenResponseStream(httpResponse, cancellationToken);
                firstByte = responseStream.ReadByte();

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

                if (IsPartialSuccessReportingEnabled())
                {
                    this.TryReportPartialSuccess(httpResponse, ref responseStream, firstByte, cancellationToken);
                }

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
                var response = TryGetResponseBody(httpResponse, Math.Min(this.MaxResponseSizeBytes, MaxLoggedResponseSizeBytes), cancellationToken);
                OpenTelemetryProtocolExporterEventSource.Log.HttpRequestFailed(this.Endpoint, response, ex);
            }

            return new ExportClientGrpcResponse(
                success: false,
                deadlineUtc: deadlineUtc,
                exception: ex,
                status: null,
                grpcStatusDetailsHeader: null);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException && !cancellationToken.IsCancellationRequested)
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Return Unavailable so the response is classified as retryable,
            // preserving any blob written to persistent storage.
            OpenTelemetryProtocolExporterEventSource.Log.FailedToReachCollector(this.Endpoint, ex);
            return new ExportClientGrpcResponse(
                success: false,
                deadlineUtc: deadlineUtc,
                exception: ex,
                status: new Status(StatusCode.Unavailable, "Unexpected error - retryable"),
                grpcStatusDetailsHeader: null);
        }
        finally
        {
            responseStream?.Dispose();
            httpResponse?.Dispose();
        }
    }

    protected override HttpContent CreateHttpContent(byte[] buffer, int contentLength) =>
        this.CompressionEnabled
            ? this.CreateGZipHttpContent(buffer, contentLength)
            : base.CreateHttpContent(buffer, contentLength);

    private static Stream OpenResponseStream(HttpResponseMessage httpResponse, CancellationToken cancellationToken) =>
#if NET
        httpResponse.Content.ReadAsStream(cancellationToken);
#else
        httpResponse.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
#endif

    private static bool TryReadExactly(Stream stream, byte[] buffer, int count)
    {
        var totalRead = 0;

        while (totalRead < count)
        {
            var bytesRead = stream.Read(buffer, totalRead, count - totalRead);

            if (bytesRead == 0)
            {
                return false;
            }

            totalRead += bytesRead;
        }

        return true;
    }

    private static bool IsTransientNetworkError(HttpRequestException ex) =>
        ex.InnerException is SocketException { SocketErrorCode: SocketError.TimedOut or SocketError.ConnectionReset or SocketError.HostUnreachable or SocketError.ConnectionRefused };

    private StreamContent CreateGZipHttpContent(byte[] buffer, int contentLength)
    {
        // Build a gzip-compressed gRPC message frame:
        //   byte 0     - Compression flag = 1 (gzip).
        //   bytes 1-4  - Compressed payload length in big-endian format.
        //   bytes 5+   - Gzip-compressed protobuf payload.
#if NET
        var compressedStream = new PooledBufferStream();
#else
        var compressedStream = new MemoryStream();
#endif

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

        Span<byte> lengthBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(lengthBytes, compressedPayloadLength);

#if NET
        compressedStream.Write(lengthBytes);
#else
        compressedStream.WriteByte(lengthBytes[0]);
        compressedStream.WriteByte(lengthBytes[1]);
        compressedStream.WriteByte(lengthBytes[2]);
        compressedStream.WriteByte(lengthBytes[3]);
#endif

        compressedStream.Position = 0;

        if (OpenTelemetryProtocolExporterEventSource.Log.IsEnabled(EventLevel.Verbose, EventKeywords.All))
        {
            OpenTelemetryProtocolExporterEventSource.Log.CompressedGrpcPayload("gzip", contentLength, compressedStream.Length);
        }

        var content = new StreamContent(compressedStream);
        content.Headers.ContentType = this.MediaTypeHeader;
        return content;
    }

    /// <summary>
    /// Reports the partial success of the message in a successful response, if it has one.
    /// </summary>
    /// <remarks>
    /// The response content is buffered (see <see cref="CompletionOption"/>), so reading
    /// it cannot block. A response that cannot be read is ignored, as the export succeeded.
    /// </remarks>
    /// <param name="httpResponse">The successful response.</param>
    /// <param name="responseStream">The stream of the response content, if it has already been opened.</param>
    /// <param name="firstByte">The first byte of the stream, if it has already been read; otherwise -1.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to use.</param>
    private void TryReportPartialSuccess(HttpResponseMessage httpResponse, ref Stream? responseStream, int firstByte, CancellationToken cancellationToken)
    {
        try
        {
            responseStream ??= OpenResponseStream(httpResponse, cancellationToken);

            var compressionFlag = firstByte >= 0 ? firstByte : responseStream.ReadByte();

            // No message, or a compressed one, which is not requested and so cannot be read.
            if (compressionFlag != 0)
            {
                return;
            }

            var lengthBytes = new byte[GrpcMessageHeaderSize - 1];

            if (!TryReadExactly(responseStream, lengthBytes, lengthBytes.Length))
            {
                return;
            }

            var messageLength = BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);

            if (messageLength == 0)
            {
                return;
            }

            if (messageLength > (uint)this.MaxResponseSizeBytes)
            {
                // Do not buffer a message larger than the limit, even if the response did not declare its size.
                OpenTelemetryProtocolExporterEventSource.Log.ResponseDiscardedDueToSizeLimit(this.Endpoint, messageLength, this.MaxResponseSizeBytes);
                return;
            }

            var message = new byte[messageLength];

            if (TryReadExactly(responseStream, message, message.Length))
            {
                this.ReportPartialSuccess(message, 0, message.Length);
            }
        }
        catch (Exception)
        {
            // The export succeeded, so failing to read the partial success must not fail it.
        }
    }
}
