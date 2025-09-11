// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Buffers.Binary;
using System.IO.Compression;
using System.Net.Http.Headers;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>Base class for sending OTLP export request over gRPC.</summary>
internal sealed class OtlpGrpcExportClient : OtlpExportClient
{
    public const string GrpcStatusDetailsHeader = "grpc-status-details-bin";
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

    public OtlpGrpcExportClient(OtlpExporterOptions options, HttpClient httpClient, string signalPath)
        : base(options, httpClient, signalPath)
    {
    }

    internal override MediaTypeHeaderValue MediaTypeHeader => MediaHeaderValue;

    internal override bool RequireHttp2 => true;

    protected override string? ContentEncodingHeader => null;

    /// <inheritdoc/>
    public override ExportClientResponse SendExportRequest(byte[] buffer, int contentLength, DateTime deadlineUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            // A gRPC message consists of 3 parts:
            // byte 0     - Compression flag (0 = not compressed, 1 = compressed).
            // bytes 1-4  - Message length in big-endian format (length of the serialized data only).
            // bytes 5+   - Protobuf-encoded payload.
            Span<byte> data = new Span<byte>(buffer, 1, 4);
            var dataLength = contentLength - GrpcMessageHeaderSize;
            BinaryPrimitives.WriteUInt32BigEndian(data, (uint)dataLength);
            buffer[0] = this.CompressionEnabled ? (byte)1 : (byte)0;

            using var httpRequest = this.CreateHttpRequest(buffer, contentLength);

            // TE is required by some servers, e.g. C Core.
            // A missing TE header results in servers aborting the gRPC call.
            httpRequest.Headers.TryAddWithoutValidation("TE", "trailers");

            using var httpResponse = this.SendHttpRequest(httpRequest, cancellationToken);

            httpResponse.EnsureSuccessStatusCode();

            var trailingHeaders = httpResponse.TrailingHeaders();
            Status status = GrpcProtocolHelpers.GetResponseStatus(httpResponse, trailingHeaders);

            if (status.Detail.Equals(Status.NoReplyDetailMessage, StringComparison.Ordinal))
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

    protected override byte[] Compress(byte[] data, int contentLength)
    {
        using var compressedStream = new MemoryStream();
        using (var gzipStream = new GZipStream(compressedStream, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzipStream.Write(data, GrpcMessageHeaderSize, data.Length - GrpcMessageHeaderSize);
        }

        var compressedDataLength = compressedStream.Position;
        var payload = new byte[compressedDataLength + GrpcMessageHeaderSize];
        using var payloadStream = new MemoryStream(payload);
        payloadStream.Position = GrpcMessageHeaderSize;
        compressedStream.WriteTo(payloadStream);

        payload[0] = 1;
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(1, 4), (uint)compressedDataLength);

        return payload;
        }

    private static bool IsTransientNetworkError(HttpRequestException ex)
    {
        return ex.InnerException is System.Net.Sockets.SocketException socketEx
            && (socketEx.SocketErrorCode == System.Net.Sockets.SocketError.TimedOut
                || socketEx.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionReset
                || socketEx.SocketErrorCode == System.Net.Sockets.SocketError.HostUnreachable
                || socketEx.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionRefused);
    }
}
