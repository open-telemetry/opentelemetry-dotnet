// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.IO.Compression;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpHttpExportClientTests
{
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

        exportClient.SendExportRequest(payload, payload.Length, DateTime.UtcNow.AddSeconds(10));

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

        exportClient.SendExportRequest(payload, payload.Length, DateTime.UtcNow.AddSeconds(10));

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
}
