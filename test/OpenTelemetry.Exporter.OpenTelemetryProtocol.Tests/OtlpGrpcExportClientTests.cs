// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET
using System.Net.Http;
#endif
using System.Buffers.Binary;
using System.Diagnostics.Tracing;
using System.IO.Compression;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient.Grpc;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.ExportClient;

[Collection(EventSourceCollectionDefinition.Name)]
public class OtlpGrpcExportClientTests
{
    private const int GrpcHeaderSize = 5;

    [Fact]
    public void SendExportRequest_ResponseExceedingMaxResponseSizeBytes_IsDiscardedAndNotRetryable()
    {
        const int ResponseDiscardedEventId = 40;
        const int MaxResponseSizeBytes = 4096;

        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Error);

        using var testHandler = new OversizedResponseHttpMessageHandler(MaxResponseSizeBytes + GrpcHeaderSize + 1, "application/grpc");
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpGrpcExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4317"),
                MaxResponseSizeBytes = MaxResponseSizeBytes,
            },
            httpClient,
            string.Empty);

        var payload = "payload"u8.ToArray();
        var response = exportClient.SendExportRequest(
            BuildGrpcFrame(payload),
            GrpcHeaderSize + payload.Length,
            DateTime.MaxValue,
            TestContext.Current.CancellationToken);

        Assert.False(response.Success);
        Assert.IsType<ResponseSizeLimitExceededException>(response.Exception);

        // The specification requires an over-sized response to be not retryable.
        Assert.False(OtlpRetry.IsRetryable(Assert.IsType<ExportClientGrpcResponse>(response)));

        Assert.Single(listener.Messages, e => e.EventId == ResponseDiscardedEventId);
    }

    [Fact]
    public void SendExportRequest_ErrorResponse_LogsResponseBody()
    {
        const int HttpRequestFailedEventId = 17;
        const string ResponseBody = "no such service";

        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Error);

        using var testHandler = new StatusCodeHttpMessageHandler(System.Net.HttpStatusCode.NotFound, System.Text.Encoding.UTF8.GetBytes(ResponseBody));
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpGrpcExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4317"),
            },
            httpClient,
            string.Empty);

        var payload = "hello world"u8.ToArray();
        var response = exportClient.SendExportRequest(payload, payload.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(response.Success);

        var grpcResponse = Assert.IsType<ExportClientGrpcResponse>(response);
        Assert.Null(grpcResponse.Status);

        var exception = Assert.IsType<HttpRequestException>(response.Exception);
        Assert.Contains("404", exception.Message, StringComparison.Ordinal);
#if NET
        Assert.Equal(System.Net.HttpStatusCode.NotFound, exception.StatusCode);
#endif

        var logged = Assert.Single(listener.Messages, e => e.EventId == HttpRequestFailedEventId);
        Assert.Equal(ResponseBody, logged.Payload![1]);
    }

    [Fact]
    public void SendExportRequest_ResponseAtMaxResponseSizeBytesBoundary_IsNotDiscardedForSize()
    {
        const int MaxResponseSizeBytes = 4096;

        using var testHandler = new OversizedResponseHttpMessageHandler(MaxResponseSizeBytes + GrpcHeaderSize, "application/grpc");
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpGrpcExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4317"),
                MaxResponseSizeBytes = MaxResponseSizeBytes,
            },
            httpClient,
            string.Empty);

        var payload = "payload"u8.ToArray();
        var response = exportClient.SendExportRequest(
            BuildGrpcFrame(payload),
            GrpcHeaderSize + payload.Length,
            DateTime.MaxValue,
            TestContext.Current.CancellationToken);

        Assert.IsNotType<ResponseSizeLimitExceededException>(response.Exception);
    }

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

        exportClient.SendExportRequest(
            buffer,
            buffer.Length,
            DateTime.UtcNow.AddSeconds(10),
            TestContext.Current.CancellationToken);

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

        exportClient.SendExportRequest(
            buffer,
            buffer.Length,
            DateTime.UtcNow.AddSeconds(10),
            TestContext.Current.CancellationToken);

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

        exportClient.SendExportRequest(
            buffer,
            buffer.Length,
            DateTime.UtcNow.AddSeconds(10),
            TestContext.Current.CancellationToken);

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

#if NET11_0_OR_GREATER
    [Fact]
    public void SendExportRequest_GrpcExport_ZstdCompression_PayloadDecompressesToOriginalProtobuf()
    {
        const int CompressedGrpcPayloadEventId = 37;

        var protobufPayload = "grpc test payload for compression"u8.ToArray();
        var buffer = BuildGrpcFrame(protobufPayload);

        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Verbose);
        using var testHandler = new TestGrpcMessageHandler();
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = CreateZstdExportClient(httpClient);

        var response = exportClient.SendExportRequest(
            buffer,
            buffer.Length,
            DateTime.UtcNow.AddSeconds(10),
            TestContext.Current.CancellationToken);

        Assert.True(response.Success);

        Assert.NotNull(testHandler.CapturedRequestBytes);
        var content = testHandler.CapturedRequestBytes;

        Assert.Equal(1, content[0]);

        var compressedLength = (int)BinaryPrimitives.ReadUInt32BigEndian(content.AsSpan(1, 4));
        Assert.Equal(content.Length - GrpcHeaderSize, compressedLength);

        using (var input = new MemoryStream(content, GrpcHeaderSize, compressedLength))
        using (var zstd = new ZstandardStream(input, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            zstd.CopyTo(output);
            Assert.Equal(protobufPayload, output.ToArray());
        }

        Assert.NotNull(testHandler.CapturedRequestHeaders);
        Assert.Contains(
            testHandler.CapturedRequestHeaders,
            h => h.Key == "grpc-encoding" && h.Value.Contains("zstd"));

        var compressedEvent = Assert.Single(listener.Messages, e => e.EventId == CompressedGrpcPayloadEventId);

        Assert.NotNull(compressedEvent.Payload);
        Assert.Equal("zstd", compressedEvent.Payload[0]);
        Assert.Equal((long)buffer.Length, compressedEvent.Payload[1]);
        Assert.Equal((long)content.Length, compressedEvent.Payload[2]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("identity,gzip")]
    [InlineData("identity, gzip, zstdx")]
    public void SendExportRequest_GrpcExport_ZstdCompression_Unimplemented_RetriesWithGzipAndThenUsesGzip(string? acceptEncoding)
    {
        const int ZstdCompressionRejectedEventId = 47;

        var buffer = BuildGrpcFrame("grpc test payload for compression"u8.ToArray());

        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Warning);
        using var testHandler = new GrpcEncodingMessageHandler(encoding => encoding == "zstd" ? StatusCode.Unimplemented : StatusCode.OK, acceptEncoding is null ? null : [acceptEncoding]);
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = CreateZstdExportClient(httpClient);

        var response = exportClient.SendExportRequest(buffer, buffer.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(response.Success);
        Assert.Equal(["zstd", "gzip"], testHandler.GrpcEncodings);
        Assert.Contains(listener.Messages, e => e.EventId == ZstdCompressionRejectedEventId);
        Assert.Equal(OtlpExportCompression.GZip, exportClient.Compression);

        response = exportClient.SendExportRequest(buffer, buffer.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(response.Success);
        Assert.Equal(["zstd", "gzip", "gzip"], testHandler.GrpcEncodings);
    }

    [Theory]
    [InlineData("zstd")]
    [InlineData("identity,gzip,zstd")]
    [InlineData("identity, ZSTD ,gzip")]
    public void SendExportRequest_GrpcExport_ZstdCompression_UnimplementedWhileZstdIsAdvertised_DoesNotRetryWithGzip(string acceptEncoding)
    {
        var buffer = BuildGrpcFrame("grpc test payload for compression"u8.ToArray());

        // The receiver supports zstd, so the error is unrelated to the compression used.
        using var testHandler = new GrpcEncodingMessageHandler(_ => StatusCode.Unimplemented, [acceptEncoding]);
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = CreateZstdExportClient(httpClient);

        var response = exportClient.SendExportRequest(buffer, buffer.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(response.Success);
        Assert.Equal(["zstd"], testHandler.GrpcEncodings);
        Assert.Equal(OtlpExportCompression.Zstd, exportClient.Compression);
    }

    [Theory]
    [InlineData("identity", "gzip", true)]
    [InlineData("identity", "zstd", false)]
    [InlineData("zstd", "gzip", false)]
    public void SendExportRequest_GrpcExport_ZstdCompression_UnimplementedWithRepeatedAcceptEncodingFields_RetriesWithGzipOnlyIfZstdIsNotAdvertised(
        string firstEncoding,
        string secondEncoding,
        bool retriesWithGzip)
    {
        var buffer = BuildGrpcFrame("grpc test payload for compression"u8.ToArray());

        // The header is sent as two fields, which must be treated as one list rather than as an error.
        using var testHandler = new GrpcEncodingMessageHandler(
            encoding => encoding == "zstd" ? StatusCode.Unimplemented : StatusCode.OK,
            [firstEncoding, secondEncoding]);
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = CreateZstdExportClient(httpClient);

        var response = exportClient.SendExportRequest(buffer, buffer.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);

        if (retriesWithGzip)
        {
            Assert.True(response.Success);
            Assert.Equal(["zstd", "gzip"], testHandler.GrpcEncodings);
            Assert.Equal(OtlpExportCompression.GZip, exportClient.Compression);
        }
        else
        {
            var grpcResponse = Assert.IsType<ExportClientGrpcResponse>(response);
            Assert.False(grpcResponse.Success);

            // Not the retryable Unavailable status that an exception reading the header would produce
            Assert.Equal(StatusCode.Unimplemented, grpcResponse.Status?.StatusCode);
            Assert.Equal(["zstd"], testHandler.GrpcEncodings);
            Assert.Equal(OtlpExportCompression.Zstd, exportClient.Compression);
        }
    }

    [Theory]
    [InlineData((int)StatusCode.InvalidArgument)]
    [InlineData((int)StatusCode.Unavailable)]
    public void SendExportRequest_GrpcExport_ZstdCompression_OtherFailure_DoesNotRetryWithGzip(int statusCode)
    {
        var buffer = BuildGrpcFrame("grpc test payload for compression"u8.ToArray());

        using var testHandler = new GrpcEncodingMessageHandler(_ => (StatusCode)statusCode);
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = CreateZstdExportClient(httpClient);

        var response = exportClient.SendExportRequest(buffer, buffer.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(response.Success);
        Assert.Equal(["zstd"], testHandler.GrpcEncodings);
        Assert.Equal(OtlpExportCompression.Zstd, exportClient.Compression);
    }
#endif

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

    [Fact]
    public void SendExportRequest_OversizedResponseBody_IsNotFullyBufferedIntoMemory()
    {
        const long OversizedResponseBytes = 16 * 1024 * 1024;

        long configuredLimit;
        using (var defaultClient = new OtlpExporterOptions().HttpClientFactory())
        {
            configuredLimit = defaultClient.MaxResponseContentBufferSize;
        }

        Assert.True(
            configuredLimit < OversizedResponseBytes,
            $"The exporter's client does not cap response buffering ({configuredLimit} bytes).");

        var buffer = BuildGrpcFrame("grpc test payload"u8.ToArray());

        using var testHandler = new OversizedResponseHandler(OversizedResponseBytes);
        using var httpClient = new HttpClient(testHandler, disposeHandler: false)
        {
            MaxResponseContentBufferSize = configuredLimit,
        };

        var exportClient = new OtlpGrpcExportClient(
            new OtlpExporterOptions { Endpoint = new Uri("http://localhost:4317") },
            httpClient,
            string.Empty);

        var response = exportClient.SendExportRequest(
            buffer,
            buffer.Length,
            DateTime.UtcNow.AddMinutes(5),
            TestContext.Current.CancellationToken);

        Assert.True(
            testHandler.BytesProduced <= configuredLimit + (256 * 1024),
            $"Expected buffering to stop near the {configuredLimit} byte cap but {testHandler.BytesProduced} bytes were read.");

        Assert.False(response.Success);
    }

    [Fact]
    public void SendExportRequest_UnexpectedException_ReturnsRetryableUnavailableResponse()
    {
        // A non-HTTP exception from a user-supplied DelegatingHandler (e.g. an auth handler
        // that throws InvalidOperationException on a failed token refresh) must be returned as
        // Unavailable so that any blob written to persistent storage is retained for retry.
        var exception = new InvalidOperationException("Custom handler failure.");

        var response = SendExportRequestThatThrows(exception);

        Assert.False(response.Success);
        Assert.NotNull(response.Status);
        Assert.Equal(StatusCode.Unavailable, response.Status.Value.StatusCode);
        Assert.True(OtlpRetry.IsRetryable(response), "An unexpected exception must produce a retryable failure response.");
    }

    [Fact]
    public void SendExportRequest_TimeoutWithCallerCancellationRequested_Propagates()
    {
        // A TaskCanceledException with an inner TimeoutException must still propagate when
        // the caller's token is also signaled. Without the cancellationToken guard in the
        // when clause, the timeout catch fires first and swallows the caller cancellation.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var buffer = BuildGrpcFrame("grpc test payload"u8.ToArray());

        using var testHandler = new ThrowingHttpMessageHandler(
            new TaskCanceledException("The request timed out.", new TimeoutException()));
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpGrpcExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4317"),
                Compression = OtlpExportCompression.None,
            },
            httpClient,
            string.Empty);

        Assert.ThrowsAny<OperationCanceledException>(() =>
            exportClient.SendExportRequest(buffer, buffer.Length, DateTime.UtcNow.AddSeconds(10), cts.Token));
    }

    [Fact]
    public void SendExportRequest_CallerCancellationRequested_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var buffer = BuildGrpcFrame("grpc test payload"u8.ToArray());

        using var testHandler = new ThrowingHttpMessageHandler(new OperationCanceledException(cts.Token));
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpGrpcExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4317"),
                Compression = OtlpExportCompression.None,
            },
            httpClient,
            string.Empty);

        Assert.ThrowsAny<OperationCanceledException>(() =>
            exportClient.SendExportRequest(buffer, buffer.Length, DateTime.UtcNow.AddSeconds(10), cts.Token));
    }

#if NET
    [Fact]
    public void SendExportRequest_Http2NotNegotiated_ReplacesHttpClientCreatedByExporter()
    {
        var buffer = BuildGrpcFrame("grpc test payload"u8.ToArray());

        // Stands in for an HttpClient that no longer attempts HTTP/2 with the endpoint.
        using var testHandler = new ThrowingHttpMessageHandler(
            new HttpRequestException(HttpRequestError.VersionNegotiationError, "HTTP/2 could not be negotiated."));
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpGrpcExportClient(
            new OtlpExporterOptions { Endpoint = new Uri("http://localhost:4317") },
            httpClient,
            string.Empty);

        var response = exportClient.SendExportRequest(buffer, buffer.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);

        var grpcResponse = Assert.IsType<ExportClientGrpcResponse>(response);
        Assert.False(grpcResponse.Success);
        Assert.IsType<HttpRequestException>(grpcResponse.Exception);

        // The replacement is created in the background, and the original is disposed of once it has been replaced.
        Assert.True(
            SpinWait.SpinUntil(
                () =>
                {
                    try
                    {
                        httpClient.CancelPendingRequests();
                        return false;
                    }
                    catch (ObjectDisposedException)
                    {
                        return true;
                    }
                },
                TimeSpan.FromSeconds(30)),
            "The HttpClient was not replaced.");

        using var replacement = exportClient.HttpClient;

        Assert.NotSame(httpClient, replacement);

        // The replacement is used by later requests and by shutdown.
        Assert.True(exportClient.Shutdown(1000));
    }

    [Fact]
    public void Shutdown_HttpClientDisposed_DoesNotThrow()
    {
        using var testHandler = new ThrowingHttpMessageHandler(new InvalidOperationException());
        var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpGrpcExportClient(
            new OtlpExporterOptions { Endpoint = new Uri("http://localhost:4317") },
            httpClient,
            string.Empty);

        // Stands in for the HttpClient being replaced and disposed of by another thread.
        httpClient.Dispose();

        Assert.True(exportClient.Shutdown(1000));
    }

    [Theory]
    [InlineData(HttpRequestError.VersionNegotiationError, true)]
    [InlineData(HttpRequestError.ConnectionError, false)]
    public void SendExportRequest_RequestFailed_KeepsHttpClient(HttpRequestError error, bool customHttpClientFactory)
    {
        var buffer = BuildGrpcFrame("grpc test payload"u8.ToArray());

        using var testHandler = new ThrowingHttpMessageHandler(new HttpRequestException(error, "The request failed."));
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var options = new OtlpExporterOptions { Endpoint = new Uri("http://localhost:4317") };

        if (customHttpClientFactory)
        {
            // An HttpClient supplied by the application is not the exporter's to replace.
            options.HttpClientFactory = () => httpClient;
        }

        var exportClient = new OtlpGrpcExportClient(options, httpClient, string.Empty);

        var response = exportClient.SendExportRequest(buffer, buffer.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(Assert.IsType<ExportClientGrpcResponse>(response).Success);
        Assert.Same(httpClient, exportClient.HttpClient);

        // The HttpClient has not been disposed of.
        httpClient.CancelPendingRequests();
    }

    [Fact]
    public void SendExportRequest_Http2NotNegotiatedAndReplacementCannotBeCreated_KeepsHttpClient()
    {
        var buffer = BuildGrpcFrame("grpc test payload"u8.ToArray());

        using var testHandler = new ThrowingHttpMessageHandler(
            new HttpRequestException(HttpRequestError.VersionNegotiationError, "HTTP/2 could not be negotiated."));
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        // The default factory throws because the certificate file does not exist.
        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri("https://localhost:4317"),
            MtlsOptions = new OtlpMtlsOptions { CaCertificatePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()) },
        };

        var exportClient = new OtlpGrpcExportClient(options, httpClient, string.Empty);

        const int SecureHttpClientCreationFailedEventId = 36;

        // The replacement is created on another thread, so the listener must not filter by the activity of this one.
        using var listener = new EventSignalListener(SecureHttpClientCreationFailedEventId);

        var response = exportClient.SendExportRequest(buffer, buffer.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);

        // The failure of the request is reported, not that of creating the replacement.
        var grpcResponse = Assert.IsType<ExportClientGrpcResponse>(response);
        Assert.False(grpcResponse.Success);
        Assert.IsType<HttpRequestException>(grpcResponse.Exception);

        // The replacement is created in the background, so wait for the attempt to fail.
        Assert.True(
            listener.Wait(TimeSpan.FromSeconds(30)),
            "The replacement HttpClient was not attempted to be created.");

        Assert.Same(httpClient, exportClient.HttpClient);

        // The HttpClient has not been disposed of.
        httpClient.CancelPendingRequests();
    }
#endif

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

#if NET11_0_OR_GREATER
    private static OtlpGrpcExportClient CreateZstdExportClient(HttpClient httpClient) =>
        new(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4317"),
                Compression = OtlpExportCompression.Zstd,
            },
            httpClient,
            string.Empty);
#endif

    private static byte[] Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        gzip.CopyTo(output);

        return output.ToArray();
    }

#if NET
    private sealed class EventSignalListener : EventListener
    {
        private readonly int eventId;
        private readonly ManualResetEventSlim signal = new(false);

        public EventSignalListener(int eventId)
        {
            this.eventId = eventId;

            this.EnableEvents(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Error);
        }

        public bool Wait(TimeSpan timeout) => this.signal.Wait(timeout);

        public override void Dispose()
        {
            base.Dispose();
            this.signal.Dispose();
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventId == this.eventId)
            {
                try
                {
                    this.signal.Set();
                }
                catch (ObjectDisposedException)
                {
                    // The test has finished.
                }
            }
        }
    }

#endif

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

    private sealed class StatusCodeHttpMessageHandler(System.Net.HttpStatusCode statusCode, byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(this.CreateResponse(request));

#if NET
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => this.CreateResponse(request);
#endif

        private HttpResponseMessage CreateResponse(HttpRequestMessage request)
            => new(statusCode)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(body),
            };
    }

    private sealed class OversizedResponseHandler(long totalBytes) : HttpMessageHandler
    {
        private readonly GeneratedStream stream = new(totalBytes);

        public long BytesProduced => this.stream.BytesProduced;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(this.CreateResponse(request));

#if NET
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => this.CreateResponse(request);
#endif

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.stream.Dispose();
            }

            base.Dispose(disposing);
        }

        private HttpResponseMessage CreateResponse(HttpRequestMessage request)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new StreamContent(this.stream),
            };

            response.Content.Headers.TryAddWithoutValidation("Content-Type", "application/grpc");

            return response;
        }

        private sealed class GeneratedStream(long totalBytes) : Stream
        {
            public long BytesProduced { get; private set; }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var remaining = totalBytes - this.BytesProduced;
                if (remaining <= 0)
                {
                    return 0;
                }

                var read = (int)Math.Min(count, remaining);
                Array.Clear(buffer, offset, read);
                this.BytesProduced += read;

                return read;
            }

            public override void Flush()
            {
                // No-op
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }

#if NET11_0_OR_GREATER
    private sealed class GrpcEncodingMessageHandler(Func<string?, StatusCode> getStatusCode, string[]? acceptEncodings = null) : HttpMessageHandler
    {
        public List<string?> GrpcEncodings { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var encoding = request.Headers.TryGetValues("grpc-encoding", out var values) ? values.SingleOrDefault() : null;

            this.GrpcEncodings.Add(encoding);

            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                RequestMessage = request,
                Content = new ByteArrayContent([]),
            };

            response.Headers.Add("grpc-status", ((int)getStatusCode(encoding)).ToString(System.Globalization.CultureInfo.InvariantCulture));

            if (acceptEncodings != null)
            {
                // Each value is sent as a separate field
                foreach (var acceptEncoding in acceptEncodings)
                {
                    response.Headers.Add("grpc-accept-encoding", acceptEncoding);
                }
            }

            return Task.FromResult(response);
        }
    }
#endif

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
