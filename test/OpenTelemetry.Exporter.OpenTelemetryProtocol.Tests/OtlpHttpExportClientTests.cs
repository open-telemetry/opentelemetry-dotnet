// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.IO.Compression;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

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
}
