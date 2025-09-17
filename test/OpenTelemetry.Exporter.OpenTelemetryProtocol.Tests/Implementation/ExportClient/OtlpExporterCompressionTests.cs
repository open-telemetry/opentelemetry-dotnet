// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.IO.Compression;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using Xunit;

#if !NET
using System.Net.Http;
#endif

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.ExportClient;

public class OtlpExporterCompressionTests
{
    [Theory]
    [InlineData(OtlpExportCompression.Gzip, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData(OtlpExportCompression.None, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData(OtlpExportCompression.Gzip, "")]
    public void SendExportRequest_SendsCorrectContent_Http(OtlpExportCompression compression, string text)
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

        if (compression == OtlpExportCompression.Gzip)
        {
            Assert.Contains(httpRequest.Content.Headers, h => h.Key == "Content-Encoding" && h.Value.First() == "gzip");

            var content = testHttpHandler.HttpRequestContent;
            Assert.NotNull(content);
            var decompressedStream = Decompress(content);

            Assert.Equal(buffer, decompressedStream.ToArray());
        }
        else
        {
            Assert.DoesNotContain(httpRequest.Content.Headers, h => h.Key == "Content-Encoding");
            Assert.Equal(buffer, testHttpHandler.HttpRequestContent);
        }
    }

    [Theory]
    [InlineData(OtlpExportCompression.Gzip, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData(OtlpExportCompression.None, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData(OtlpExportCompression.Gzip, "")]
    public void SendExportRequest_SendsCorrectContent_Grpc(OtlpExportCompression compression, string text)
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

        if (compression == OtlpExportCompression.Gzip)
        {
            var decompressedStream = Decompress(body);

            Assert.Equal(payload, decompressedStream.ToArray());
        }
        else
        {
            Assert.Equal(payload, body);
        }
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
