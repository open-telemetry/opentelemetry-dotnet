// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET
using System.Net.Http;
#endif
using System.Buffers.Binary;
using System.IO.Compression;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.ExportClient;

public class OtlpGrpcExportClientTests
{
    private const int GrpcHeaderSize = 5;

    [Fact]
    public void SendExportRequest_GrpcExport_NoCompression_ContentMatchesOriginalPayload()
    {
        var protobufPayload = "grpc test payload"u8.ToArray();
        var buffer = BuildGrpcFrame(protobufPayload);

        using var testHandler = new TestGrpcMessageHandler();
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpGrpcExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4317"),
                Compression = OtlpExportCompression.None,
            },
            httpClient,
            string.Empty);

        exportClient.SendExportRequest(buffer, buffer.Length, DateTime.UtcNow.AddSeconds(10));

        Assert.NotNull(testHandler.CapturedRequestBytes);
        var content = testHandler.CapturedRequestBytes;
        Assert.True(content.Length >= GrpcHeaderSize, "No content was written.");

        Assert.Equal(0, content[0]);

        var declaredLength = (int)BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(1, 4));
        Assert.Equal(protobufPayload.Length, declaredLength);
        Assert.Equal(protobufPayload, content.AsSpan(GrpcHeaderSize, protobufPayload.Length).ToArray());

        Assert.NotNull(testHandler.CapturedRequestHeaders);
        Assert.DoesNotContain(testHandler.CapturedRequestHeaders, h => h.Key == "grpc-encoding");
    }

    [Fact]
    public void SendExportRequest_GrpcExport_GzipCompression_FrameLengthMatchesCompressedPayload()
    {
        var protobufPayload = "grpc test payload for compression"u8.ToArray();
        var buffer = BuildGrpcFrame(protobufPayload);

        using var testHandler = new TestGrpcMessageHandler();
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpGrpcExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4317"),
                Compression = OtlpExportCompression.GZip,
            },
            httpClient,
            string.Empty);

        exportClient.SendExportRequest(buffer, buffer.Length, DateTime.UtcNow.AddSeconds(10));

        Assert.NotNull(testHandler.CapturedRequestBytes);
        var content = testHandler.CapturedRequestBytes;
        Assert.True(content.Length >= GrpcHeaderSize, "No content was written.");

        Assert.Equal(1, testHandler.CapturedRequestBytes[0]);

        var compressedLength = (int)BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(1, 4));
        Assert.Equal(content.Length - GrpcHeaderSize, compressedLength);
    }

    [Fact]
    public void SendExportRequest_GrpcExport_GzipCompression_PayloadDecompressesToOriginalProtobuf()
    {
        var protobufPayload = "grpc test payload for compression"u8.ToArray();
        var buffer = BuildGrpcFrame(protobufPayload);

        using var testHandler = new TestGrpcMessageHandler();
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpGrpcExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4317"),
                Compression = OtlpExportCompression.GZip,
            },
            httpClient,
            string.Empty);

        exportClient.SendExportRequest(buffer, buffer.Length, DateTime.UtcNow.AddSeconds(10));

        Assert.NotNull(testHandler.CapturedRequestBytes);
        var content = testHandler.CapturedRequestBytes;
        var compressedLength = (int)BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(1, 4));
        var decompressed = Decompress(content.AsSpan(GrpcHeaderSize, compressedLength).ToArray());
        Assert.Equal(protobufPayload, decompressed);

        Assert.NotNull(testHandler.CapturedRequestHeaders);
        Assert.Contains(
            testHandler.CapturedRequestHeaders,
            h => h.Key == "grpc-encoding" && h.Value.Contains("gzip"));
    }

    [Fact]
    public void SendExportRequest_Timeout_ReturnsRetryableDeadlineExceededResponse()
    {
        var exception = new TaskCanceledException("The request timed out.", new TimeoutException());

        var response = SendExportRequestThatThrows(exception);

        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.NotNull(response.Exception);
        Assert.NotNull(response.Status);
        Assert.Equal(StatusCode.DeadlineExceeded, response.Status.Value.StatusCode);
        Assert.True(RetryHelper.ShouldRetryRequest(response), "A timeout should be retryable.");
    }

    [Fact]
    public void SendExportRequest_UnexpectedCancellation_ReturnsRetryableCancelledResponse()
    {
        var exception = new OperationCanceledException("The operation was canceled.");

        var response = SendExportRequestThatThrows(exception);

        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.NotNull(response.Exception);
        Assert.NotNull(response.Status);
        Assert.Equal(StatusCode.Cancelled, response.Status.Value.StatusCode);
        Assert.True(RetryHelper.ShouldRetryRequest(response), "An unexpected cancellation should be retryable.");
    }

    private static ExportClientGrpcResponse SendExportRequestThatThrows(Exception exception)
    {
        var buffer = BuildGrpcFrame("grpc test payload"u8.ToArray());

        using var testHandler = new ThrowingHttpMessageHandler(exception);
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpGrpcExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4317"),
                Compression = OtlpExportCompression.None,
            },
            httpClient,
            string.Empty);

        var response = exportClient.SendExportRequest(buffer, buffer.Length, DateTime.UtcNow.AddSeconds(10));

        return Assert.IsType<ExportClientGrpcResponse>(response);
    }

    private static byte[] BuildGrpcFrame(byte[] protobufPayload)
    {
        var frame = new byte[GrpcHeaderSize + protobufPayload.Length];
        frame[0] = 0;

        BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(1, 4), (uint)protobufPayload.Length);

        protobufPayload.CopyTo(frame, GrpcHeaderSize);

        return frame;
    }

    private static byte[] Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        gzip.CopyTo(output);

        return output.ToArray();
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Exception exception;

        public ThrowingHttpMessageHandler(Exception exception)
        {
            this.exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw this.exception;

#if NET
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw this.exception;
#endif
    }

    private sealed class TestGrpcMessageHandler : HttpMessageHandler
    {
        public byte[]? CapturedRequestBytes { get; private set; }

        public List<KeyValuePair<string, IEnumerable<string>>>? CapturedRequestHeaders { get; private set; }

#if NET
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.CapturedRequestHeaders = [.. request.Headers];

            using var stream = request.Content!.ReadAsStream(cancellationToken);
            using var memoryStream = new MemoryStream();

            stream.CopyTo(memoryStream);

            this.CapturedRequestBytes = memoryStream.ToArray();

            return CreateResponse(request);
        }
#endif

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.CapturedRequestHeaders = [.. request.Headers];

#if NET
            using var stream = await request.Content!.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
            using var stream = await request.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
            using var memoryStream = new MemoryStream();

#if NET
            await stream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
#else
            await stream.CopyToAsync(memoryStream).ConfigureAwait(false);
#endif

            this.CapturedRequestBytes = memoryStream.ToArray();

            return CreateResponse(request);
        }

        private static HttpResponseMessage CreateResponse(HttpRequestMessage request)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent([]),
            };

            response.Headers.Add("grpc-status", "0");

            return response;
        }
    }
}
