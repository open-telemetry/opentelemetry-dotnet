// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.IO.Compression;
using System.Net.Http.Headers;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using Xunit;

#if !NET
using System.Net.Http;
#endif

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.ExportClient;

public class OtlpExporterCompressionTests
{
    [Theory]
    [InlineData("")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void SendExportRequest_SendsCorrectContent_Http_NonCompressed(string text)
    {
        SendExportRequest_Http(OtlpExportCompression.None, text, (requestHeaders, testHttpHandlerContent, buffer) =>
        {
            Assert.DoesNotContain(requestHeaders, h => h.Key == "Content-Encoding");
            Assert.Equal(buffer, testHttpHandlerContent);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void SendExportRequest_SendsCorrectContent_Http_Compressed(string text)
    {
        SendExportRequest_Http(OtlpExportCompression.Gzip, text, (requestHeaders, testHttpHandlerContent, buffer) =>
        {
            Assert.Contains(requestHeaders, h => h.Key == "Content-Encoding" && h.Value.First() == "gzip");

            Assert.NotNull(testHttpHandlerContent);
            var decompressedStream = Decompress(testHttpHandlerContent);

            Assert.Equal(buffer, decompressedStream.ToArray());
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void SendExportRequest_SendsCorrectContent_Grpc_NonCompressed(string text)
    {
        SendExportRequest_Grpc(OtlpExportCompression.None, text, body => body);
    }

    [Theory]
    [InlineData("")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void SendExportRequest_SendsCorrectContent_Grpc_Compressed(string text)
    {
        SendExportRequest_Grpc(OtlpExportCompression.Gzip, text, Decompress);
    }

    private static void SendExportRequest_Grpc(OtlpExportCompression compression, string text, Func<byte[], byte[]?> readBody)
    {
        var payload = System.Text.Encoding.UTF8.GetBytes(text);

        // Arrange
        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri("http://localhost:4317"),
            Compression = compression,
        };

        var buffer = new byte[payload.Length + 5];
        Buffer.BlockCopy(payload, 0, buffer, 5, payload.Length);

        using var testGrpcHandler = new TestGrpcMessageHandler();
        using var httpClient = new HttpClient(testGrpcHandler, false);
        var exportClient = new OtlpGrpcExportClient(options, httpClient, string.Empty);

        var deadlineUtc = DateTime.UtcNow.AddMilliseconds(httpClient.Timeout.TotalMilliseconds);

        // Act
        var result = exportClient.SendExportRequest(buffer, buffer.Length, deadlineUtc);
        var httpRequest = testGrpcHandler.HttpRequestMessage;
        var requestContent = testGrpcHandler.HttpRequestContent;

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(httpRequest);
        Assert.NotNull(requestContent);

        var compressionFlag = requestContent[0];
        var declaredLength = BinaryPrimitives.ReadUInt32BigEndian(requestContent.AsSpan(1, 4));
        var body = requestContent.AsSpan(5, (int)declaredLength).ToArray();

        Assert.Equal(compression == OtlpExportCompression.Gzip ? 1 : 0, compressionFlag);
        Assert.Equal(body.Length, (int)declaredLength);

        Assert.Equal(payload, readBody(body));
    }

    private static void SendExportRequest_Http(OtlpExportCompression compression, string text, Action<HttpContentHeaders, byte[]?, byte[]> assertions)
    {
        var buffer = System.Text.Encoding.UTF8.GetBytes(text);

        // Arrange
        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri("http://localhost:4317"),
            Compression = compression,
        };

        using var testHttpHandler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(testHttpHandler, false);
        var exportClient = new OtlpHttpExportClient(options, httpClient, string.Empty);

        var deadlineUtc = DateTime.UtcNow.AddMilliseconds(httpClient.Timeout.TotalMilliseconds);

        // Act
        var result = exportClient.SendExportRequest(buffer, buffer.Length, deadlineUtc);
        var httpRequest = testHttpHandler.HttpRequestMessage;

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(httpRequest);
        Assert.Equal(HttpMethod.Post, httpRequest.Method);
        Assert.NotNull(httpRequest.Content);

        assertions(httpRequest.Content.Headers, testHttpHandler.HttpRequestContent, buffer);
    }

    private static byte[] Decompress(byte[] data)
    {
        using var compressedStream = new MemoryStream(data);
        using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var decompressedStream = new MemoryStream();
        gzipStream.CopyTo(decompressedStream);
        return decompressedStream.ToArray();
    }
}
