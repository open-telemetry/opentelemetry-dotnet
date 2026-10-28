// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
#if NET11_0_OR_GREATER
using System.Buffers.Binary;
#endif
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

[Collection(EnvVarsCollectionDefinition.Name)]
public class OtlpHttpExportClientTests
{
    [Fact]
    public void SendExportRequest_ResponseExceedingMaxResponseSizeBytes_IsDiscardedAndNotRetryable()
    {
        const int ResponseDiscardedEventId = 40;
        const int MaxResponseSizeBytes = 4096;

        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Error);

        using var testHandler = new OversizedResponseHttpMessageHandler(MaxResponseSizeBytes + 1, "application/x-protobuf");
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpHttpExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4318"),
                Protocol = OtlpExportProtocol.HttpProtobuf,
                MaxResponseSizeBytes = MaxResponseSizeBytes,
            },
            httpClient,
            string.Empty);

        var response = exportClient.SendExportRequest(
            "hello world"u8.ToArray(),
            11,
            DateTime.MaxValue,
            TestContext.Current.CancellationToken);

        Assert.False(response.Success);
        Assert.IsType<ResponseSizeLimitExceededException>(response.Exception);

        // The specification requires an over-sized response to be not retryable.
        Assert.False(OtlpRetry.IsRetryable(Assert.IsType<ExportClientHttpResponse>(response)));

        Assert.Single(listener.Messages, e => e.EventId == ResponseDiscardedEventId);
    }

    [Fact]
    public void SendExportRequest_ResponseAtMaxResponseSizeBytes_IsAccepted()
    {
        const int MaxResponseSizeBytes = 4096;

        using var testHandler = new OversizedResponseHttpMessageHandler(MaxResponseSizeBytes, "application/x-protobuf");
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpHttpExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4318"),
                Protocol = OtlpExportProtocol.HttpProtobuf,
                MaxResponseSizeBytes = MaxResponseSizeBytes,
            },
            httpClient,
            string.Empty);

        // The limit is inclusive, so a response of exactly this size is accepted.
        Assert.True(
            exportClient.SendExportRequest(
                "hello world"u8.ToArray(),
                11,
                DateTime.MaxValue,
                TestContext.Current.CancellationToken).Success);
    }

    [Theory]
    [InlineData(null, null, "http://localhost:4318/signal/path")]
    [InlineData(null, "http://from.otel.exporter.env.var", "http://from.otel.exporter.env.var/signal/path")]
    [InlineData("https://custom.host", null, "https://custom.host")]
    [InlineData("http://custom.host:44318/custom/path", null, "http://custom.host:44318/custom/path")]
    [InlineData("https://custom.host", "http://from.otel.exporter.env.var", "https://custom.host")]
    public void ValidateOtlpHttpExportClientEndpoint(string? optionEndpoint, string? endpointEnvVar, string expectedExporterEndpoint)
    {
        try
        {
            Environment.SetEnvironmentVariable(OtlpSpecConfigDefinitions.DefaultEndpointEnvVarName, endpointEnvVar);

            OtlpExporterOptions options = new() { Protocol = OtlpExportProtocol.HttpProtobuf };

            if (optionEndpoint != null)
            {
                options.Endpoint = new Uri(optionEndpoint);
            }

            using var httpClient = new HttpClient();
            var exporterClient = new OtlpHttpExportClient(options, httpClient, "signal/path");
            Assert.Equal(new Uri(expectedExporterEndpoint), exporterClient.Endpoint);
        }
        finally
        {
            Environment.SetEnvironmentVariable(OtlpSpecConfigDefinitions.DefaultEndpointEnvVarName, null);
        }
    }

    [Fact]
    public void SendExportRequest_HttpExport_NoCompression_ContentMatchesOriginalBuffer()
    {
        var payload = "hello world"u8.ToArray();

        using var testHandler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpHttpExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4318"),
                Protocol = OtlpExportProtocol.HttpProtobuf,
                Compression = OtlpExportCompression.None,
            },
            httpClient,
            string.Empty);

        exportClient.SendExportRequest(
            payload,
            payload.Length,
            DateTime.UtcNow.AddSeconds(10),
            TestContext.Current.CancellationToken);

        Assert.Equal(payload, testHandler.HttpRequestContent);

        var request = testHandler.HttpRequestMessage;
        Assert.NotNull(request?.Content);
        Assert.DoesNotContain(request.Content.Headers, h => h.Key == "Content-Encoding");
    }

    [Fact]
    public void SendExportRequest_WithGzipCompression_IsCompressed()
    {
        var payload = "00000000000000000000000000000000"u8.ToArray();

        using var testHandler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpHttpExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4318"),
                Protocol = OtlpExportProtocol.HttpProtobuf,
                Compression = OtlpExportCompression.GZip,
            },
            httpClient,
            string.Empty);

        exportClient.SendExportRequest(
            payload,
            payload.Length,
            DateTime.UtcNow.AddSeconds(10),
            TestContext.Current.CancellationToken);

        var content = testHandler.HttpRequestContent;

        Assert.NotNull(content);
        Assert.NotEmpty(content);
        Assert.True(content.Length < payload.Length, "The payload was not compressed.");

        byte[] decompressed;

        using (var input = new MemoryStream(content))
        using (var gzip = new GZipStream(input, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            gzip.CopyTo(output);
            decompressed = output.ToArray();
        }

        Assert.NotEmpty(decompressed);
        Assert.Equal(payload, decompressed);

        var request = testHandler.HttpRequestMessage;

        Assert.NotNull(request);
        Assert.NotNull(request.Content);
        Assert.Contains(request.Content.Headers, h => h.Key == "Content-Encoding" && h.Value.Contains("gzip"));

#if NETFRAMEWORK
        // Without chunked transfer, HttpClientHandler buffers the whole compressed body before sending it.
        Assert.True(request.Headers.TransferEncodingChunked);
#endif
    }

#if NET11_0_OR_GREATER
    [Fact]
    public void SendExportRequest_WithZstdCompression_IsCompressed()
    {
        var payload = "00000000000000000000000000000000"u8.ToArray();

        using var testHandler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = CreateZstdExportClient(httpClient);

        var response = exportClient.SendExportRequest(
            payload,
            payload.Length,
            DateTime.UtcNow.AddSeconds(10),
            TestContext.Current.CancellationToken);

        Assert.True(response.Success);

        var content = testHandler.HttpRequestContent;

        Assert.NotNull(content);
        Assert.True(content.Length < payload.Length, "The payload was not compressed.");
        Assert.Equal(payload, DecompressZstd(content));
        AssertZstdFrameIsInteroperable(content);

        var request = testHandler.HttpRequestMessage;

        Assert.NotNull(request);
        Assert.NotNull(request.Content);
        Assert.Contains(request.Content.Headers, h => h.Key == "Content-Encoding" && h.Value.Contains("zstd"));
    }

    [Fact]
    public void SendExportRequest_WithZstdCompression_LargePayloadWindowDoesNotExceed8MiB()
    {
        // Larger than the 8 MiB window, so the frame cannot be single-segment.
        var payload = new byte[(9 * 1024 * 1024) + 1];
        System.Security.Cryptography.RandomNumberGenerator.Fill(payload);

        using var testHandler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = CreateZstdExportClient(httpClient);

        exportClient.SendExportRequest(
            payload,
            payload.Length,
            DateTime.UtcNow.AddSeconds(30),
            TestContext.Current.CancellationToken);

        var content = testHandler.HttpRequestContent;

        Assert.NotNull(content);
        Assert.Equal(payload, DecompressZstd(content));

        var windowSize = AssertZstdFrameIsInteroperable(content);
        Assert.True(windowSize <= 8 * 1024 * 1024, $"The window size of {windowSize} bytes exceeds 8 MiB.");
    }

    [Theory]
    [InlineData(HttpStatusCode.UnsupportedMediaType)]
    [InlineData(HttpStatusCode.BadRequest)]
    public void SendExportRequest_WithZstdCompression_RejectedByReceiver_RetriesWithGzipAndThenUsesGzip(HttpStatusCode rejectionStatusCode)
    {
        const int ZstdCompressionRejectedEventId = 45;

        var payload = "00000000000000000000000000000000"u8.ToArray();

        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Warning);
        using var testHandler = new ContentEncodingHttpMessageHandler(encoding => encoding == "zstd" ? rejectionStatusCode : HttpStatusCode.OK);
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = CreateZstdExportClient(httpClient);

        var response = exportClient.SendExportRequest(payload, payload.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(response.Success);
        Assert.Equal(["zstd", "gzip"], testHandler.ContentEncodings);
        Assert.Contains(listener.Messages, e => e.EventId == ZstdCompressionRejectedEventId);
        Assert.Equal(OtlpExportCompression.GZip, exportClient.Compression);

        response = exportClient.SendExportRequest(payload, payload.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);

        Assert.True(response.Success);
        Assert.Equal(["zstd", "gzip", "gzip"], testHandler.ContentEncodings);
    }

    [Fact]
    public void SendExportRequest_WithZstdCompression_RejectedForOtherReason_KeepsUsingZstd()
    {
        var payload = "00000000000000000000000000000000"u8.ToArray();

        using var testHandler = new ContentEncodingHttpMessageHandler(_ => HttpStatusCode.BadRequest);
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = CreateZstdExportClient(httpClient);

        var response = exportClient.SendExportRequest(payload, payload.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);

        var httpResponse = Assert.IsType<ExportClientHttpResponse>(response);
        Assert.False(httpResponse.Success);
        Assert.Equal(HttpStatusCode.BadRequest, httpResponse.StatusCode);
        Assert.Equal(["zstd", "gzip"], testHandler.ContentEncodings);
        Assert.Equal(OtlpExportCompression.Zstd, exportClient.Compression);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public void SendExportRequest_WithZstdCompression_OtherFailure_DoesNotRetryWithGzip(HttpStatusCode statusCode)
    {
        var payload = "00000000000000000000000000000000"u8.ToArray();

        using var testHandler = new ContentEncodingHttpMessageHandler(_ => statusCode);
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = CreateZstdExportClient(httpClient);

        var response = exportClient.SendExportRequest(payload, payload.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(response.Success);
        Assert.Equal(["zstd"], testHandler.ContentEncodings);
    }
#else
    [Fact]
    public void SendExportRequest_WithZstdCompression_UsesGzipWhenZstdIsNotSupported()
    {
        const int ZstdCompressionNotSupportedEventId = 44;

        var payload = "00000000000000000000000000000000"u8.ToArray();

        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Warning);
        using var testHandler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpHttpExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4318"),
                Protocol = OtlpExportProtocol.HttpProtobuf,
                Compression = OtlpExportCompression.Zstd,
            },
            httpClient,
            string.Empty);

        exportClient.SendExportRequest(
            payload,
            payload.Length,
            DateTime.UtcNow.AddSeconds(10),
            TestContext.Current.CancellationToken);

        Assert.Equal(OtlpExportCompression.GZip, exportClient.Compression);
        Assert.Contains(listener.Messages, e => e.EventId == ZstdCompressionNotSupportedEventId);

        var request = testHandler.HttpRequestMessage;

        Assert.NotNull(request);
        Assert.NotNull(request.Content);
        Assert.Contains(request.Content.Headers, h => h.Key == "Content-Encoding" && h.Value.Contains("gzip"));
    }
#endif

    [Fact]
    public void SendExportRequest_UnexpectedException_ReturnsRetryableFailureResponse()
    {
        // A non-HTTP exception from a user-supplied DelegatingHandler (e.g. an auth handler
        // that throws InvalidOperationException on a failed token refresh) must be caught and
        // returned as a retryable failure rather than propagating out of SendExportRequest,
        // which would kill the persistent-storage retry thread.
        var exception = new InvalidOperationException("Auth handler failure.");

        var response = SendExportRequestThatThrows(exception);

        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.NotNull(response.Exception);
        var httpResponse = Assert.IsType<ExportClientHttpResponse>(response);
        Assert.True(OtlpRetry.IsRetryable(httpResponse), "An unexpected exception must produce a retryable failure response.");
    }

    [Fact]
    public void SendExportRequest_Timeout_ReturnsRetryableFailureResponse()
    {
        // A TaskCanceledException whose InnerException is a TimeoutException is
        // what HttpClient throws when HttpClient.Timeout fires. This must be
        // surfaced as a failed (retryable) response rather than propagating out
        // of SendExportRequest, which would bypass the retry handler.
        var exception = new TaskCanceledException("The request timed out.", new TimeoutException());

        var response = SendExportRequestThatThrows(exception);

        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.NotNull(response.Exception);
        Assert.True(RetryHelper.ShouldRetryRequest(response), "A timeout should be retryable.");
    }

    [Fact]
    public void SendExportRequest_UnexpectedCancellation_ReturnsRetryableFailureResponse()
    {
        // An OperationCanceledException that is not the result of the caller's
        // cancellation token being signaled should also be surfaced as a failed
        // (retryable) response.
        var exception = new OperationCanceledException("The operation was canceled.");

        var response = SendExportRequestThatThrows(exception);

        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.NotNull(response.Exception);
        Assert.True(RetryHelper.ShouldRetryRequest(response), "An unexpected cancellation should be retryable.");
    }

    [Fact]
    public void SendExportRequest_TimeoutWithCallerCancellationRequested_Propagates()
    {
        // A TaskCanceledException with an inner TimeoutException must still propagate when
        // the caller's token is also signaled. Without the cancellationToken guard in the
        // when clause, the timeout catch fires first and swallows the caller cancellation.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var payload = "hello world"u8.ToArray();

        using var testHandler = new ThrowingHttpMessageHandler(
            new TaskCanceledException("The request timed out.", new TimeoutException()));
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpHttpExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4318"),
                Protocol = OtlpExportProtocol.HttpProtobuf,
                Compression = OtlpExportCompression.None,
            },
            httpClient,
            string.Empty);

        // Use ThrowsAny to accept TaskCanceledException (a subtype of OperationCanceledException),
        // which HttpClient may produce when wrapping a timeout that coincides with caller cancellation.
        Assert.ThrowsAny<OperationCanceledException>(() =>
            exportClient.SendExportRequest(payload, payload.Length, DateTime.UtcNow.AddSeconds(10), cts.Token));
    }

    [Fact]
    public void SendExportRequest_CallerCancellationRequested_Propagates()
    {
        // OperationCanceledException thrown when the caller's token is signaled must propagate
        // rather than being swallowed and converted into a retryable response.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var payload = "hello world"u8.ToArray();

        using var testHandler = new ThrowingHttpMessageHandler(new OperationCanceledException(cts.Token));
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpHttpExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4318"),
                Protocol = OtlpExportProtocol.HttpProtobuf,
                Compression = OtlpExportCompression.None,
            },
            httpClient,
            string.Empty);

        Assert.ThrowsAny<OperationCanceledException>(() =>
            exportClient.SendExportRequest(payload, payload.Length, DateTime.UtcNow.AddSeconds(10), cts.Token));
    }

    [Fact]
    public void SendExportRequest_MalformedAuthorizationHeader_DoesNotLeakSecretIntoDiagnosticLog()
    {
        const int FailedToReachCollectorEventId = 2;
        const string FakeSecret = "s3cr3t-token-value";

        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Error);

        using var testHandler = new ThrowingHttpMessageHandler(new InvalidOperationException("unused"));
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpHttpExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4318"),
                Protocol = OtlpExportProtocol.HttpProtobuf,
                Headers = $"Authorization=Api-Key: {FakeSecret}",
            },
            httpClient,
            string.Empty);

        var payload = "hello world"u8.ToArray();
        var response = exportClient.SendExportRequest(payload, payload.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(response.Success);

        var logged = Assert.Single(listener.Messages, e => e.EventId == FailedToReachCollectorEventId);

        var loggedException = (string)logged.Payload![1]!;
        Assert.DoesNotContain(FakeSecret, loggedException, StringComparison.Ordinal);
        Assert.Contains("Authorization", loggedException, StringComparison.Ordinal);
    }

    [Fact]
    public void SendExportRequest_ErrorResponse_LogsResponseBody()
    {
        const int HttpRequestFailedEventId = 17;
        const string ResponseBody = "{\"error\":\"invalid payload\"}";

        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Error);

        using var testHandler = new StatusCodeHttpMessageHandler(HttpStatusCode.BadRequest, System.Text.Encoding.UTF8.GetBytes(ResponseBody));
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpHttpExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4318"),
                Protocol = OtlpExportProtocol.HttpProtobuf,
            },
            httpClient,
            string.Empty);

        var payload = "hello world"u8.ToArray();
        var response = exportClient.SendExportRequest(payload, payload.Length, DateTime.UtcNow.AddSeconds(10), TestContext.Current.CancellationToken);

        Assert.False(response.Success);
        Assert.Equal(HttpStatusCode.BadRequest, Assert.IsType<ExportClientHttpResponse>(response).StatusCode);

        var exception = Assert.IsType<HttpRequestException>(response.Exception);
        Assert.Contains("400", exception.Message, StringComparison.Ordinal);
#if NET
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
#endif

        var logged = Assert.Single(listener.Messages, e => e.EventId == HttpRequestFailedEventId);
        Assert.Equal(ResponseBody, logged.Payload![1]);
    }

    [Fact]
    public async Task SendExportRequest_ErrorResponseBodyNeverCompletes_ReturnsAtDeadline()
    {
        const int HttpRequestFailedEventId = 17;

        // The deadline also bounds sending the request, so it must leave time to connect and
        // receive the response headers when the thread pool is busy with tests running in parallel.
        var deadline = TimeSpan.FromSeconds(5);

        // Generous enough for a slow CI machine, but far short of how long the server holds the connection.
        var tolerance = TimeSpan.FromSeconds(10);
        var serverHold = TimeSpan.FromSeconds(60);

        // The response body is only read when it is going to be logged.
        using var listener = new TestEventListener(OpenTelemetryProtocolExporterEventSource.Log, EventLevel.Error);

        using var exported = new ManualResetEventSlim();

        var server = new TcpListener(IPAddress.Loopback, 0);
        Task? serverTask = null;

        try
        {
            server.Start();

            // Announce more content than is sent, then keep the connection open.
            serverTask = Task.Run(
                () =>
                {
                    using var connection = server.AcceptTcpClient();
                    using var stream = connection.GetStream();

                    _ = stream.Read(new byte[4096], 0, 4096);

                    var headersAndPartialBody = "HTTP/1.1 400 Bad Request\r\nContent-Length: 100\r\n\r\nxxx"u8.ToArray();
                    stream.Write(headersAndPartialBody, 0, headersAndPartialBody.Length);

                    exported.Wait(serverHold);
                },
                TestContext.Current.CancellationToken);

            using var httpClient = new HttpClient();

            var exportClient = new OtlpHttpExportClient(
                new OtlpExporterOptions
                {
                    Endpoint = new Uri($"http://{server.LocalEndpoint}"),
                    Protocol = OtlpExportProtocol.HttpProtobuf,
                },
                httpClient,
                string.Empty);

            var payload = "hello world"u8.ToArray();

            var stopwatch = Stopwatch.StartNew();
            var response = exportClient.SendExportRequest(payload, payload.Length, DateTime.UtcNow.Add(deadline), TestContext.Current.CancellationToken);
            stopwatch.Stop();

            Assert.False(response.Success);
            Assert.Equal(HttpStatusCode.BadRequest, Assert.IsType<ExportClientHttpResponse>(response).StatusCode);

            // The export must give up at the deadline rather than wait for the server to close the connection.
            Assert.True(stopwatch.Elapsed < deadline + tolerance, $"Export took {stopwatch.Elapsed} against a deadline of {deadline}.");

            // The failure is still logged, just without the body that never arrived.
            var logged = Assert.Single(listener.Messages, e => e.EventId == HttpRequestFailedEventId);
            Assert.Equal(string.Empty, logged.Payload![1]);
        }
        finally
        {
            exported.Set();
#if NET
            server.Dispose();
#else
            server.Stop();
#endif

            if (serverTask is not null)
            {
                // Let the server stop waiting on the event before it is disposed.
                await Task.WhenAny(serverTask);
            }
        }
    }

    private static ExportClientResponse SendExportRequestThatThrows(Exception exception)
    {
        var payload = "hello world"u8.ToArray();

        using var testHandler = new ThrowingHttpMessageHandler(exception);
        using var httpClient = new HttpClient(testHandler, disposeHandler: false);

        var exportClient = new OtlpHttpExportClient(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4318"),
                Protocol = OtlpExportProtocol.HttpProtobuf,
                Compression = OtlpExportCompression.None,
            },
            httpClient,
            string.Empty);

        return exportClient.SendExportRequest(payload, payload.Length, DateTime.UtcNow.AddSeconds(10));
    }

#if NET11_0_OR_GREATER
    private static OtlpHttpExportClient CreateZstdExportClient(HttpClient httpClient) =>
        new(
            new OtlpExporterOptions
            {
                Endpoint = new Uri("http://localhost:4318"),
                Protocol = OtlpExportProtocol.HttpProtobuf,
                Compression = OtlpExportCompression.Zstd,
            },
            httpClient,
            string.Empty);

    private static byte[] DecompressZstd(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var zstd = new ZstandardStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();

        zstd.CopyTo(output);

        return output.ToArray();
    }

    /// <summary>
    /// Asserts the content is a standard Zstandard frame that does not use a dictionary.
    /// </summary>
    /// <param name="frame">The Zstandard frame.</param>
    /// <returns>The window size the frame requires a decoder to use.</returns>
    private static long AssertZstdFrameIsInteroperable(byte[] frame)
    {
        // See https://www.rfc-editor.org/rfc/rfc8878#section-3.1.1.
        Assert.Equal(0xFD2FB528u, BinaryPrimitives.ReadUInt32LittleEndian(frame));

        var descriptor = frame[4];
        var dictionaryIdFlag = descriptor & 0b11;
        var singleSegment = (descriptor & 0b0010_0000) != 0;
        var contentSizeFlag = descriptor >> 6;

        Assert.Equal(0, dictionaryIdFlag);

        if (singleSegment)
        {
            // The window size is the content size.
            return contentSizeFlag switch
            {
                0 => frame[5],
                1 => BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(5)) + 256,
                2 => BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(5)),
                _ => (long)BinaryPrimitives.ReadUInt64LittleEndian(frame.AsSpan(5)),
            };
        }

        var windowDescriptor = frame[5];
        var windowBase = 1L << (10 + (windowDescriptor >> 3));

        return windowBase + ((windowBase / 8) * (windowDescriptor & 0b111));
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

#if NET11_0_OR_GREATER
    private sealed class ContentEncodingHttpMessageHandler(Func<string?, HttpStatusCode> getStatusCode) : HttpMessageHandler
    {
        public List<string?> ContentEncodings { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(this.Send(request, cancellationToken));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var encoding = request.Content?.Headers.ContentEncoding.SingleOrDefault();

            this.ContentEncodings.Add(encoding);

            return new(getStatusCode(encoding))
            {
                RequestMessage = request,
                Content = new ByteArrayContent([]),
            };
        }
    }
#endif

    private sealed class StatusCodeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode statusCode;
        private readonly byte[] body;

        public StatusCodeHttpMessageHandler(HttpStatusCode statusCode, byte[] body)
        {
            this.statusCode = statusCode;
            this.body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(this.CreateResponse(request));

#if NET
        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
            => this.CreateResponse(request);
#endif

        private HttpResponseMessage CreateResponse(HttpRequestMessage request)
            => new(this.statusCode)
            {
                RequestMessage = request,
                Content = new ByteArrayContent(this.body),
            };
    }
}
