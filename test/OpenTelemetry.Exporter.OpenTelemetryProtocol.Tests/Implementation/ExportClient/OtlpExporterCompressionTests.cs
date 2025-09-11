// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.IO.Compression;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using Xunit;
using System.Buffers.Binary;

#if !NET
using System.Net.Http;
#endif

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests.Implementation.ExportClient;

public class OtlpExporterCompressionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SendExportRequest_SendsCorrectContent_Http(bool compressPayload)
    {
        // Arrange
        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri("http://localhost:4317"),
            CompressPayload = compressPayload,
        };

        using var testHttpHandler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(testHttpHandler, false);
        var exportClient = new OtlpHttpExportClient(options, httpClient, string.Empty);

        var buffer = Enumerable.Repeat((byte)65, 1000).ToArray();
        var deadlineUtc = DateTime.UtcNow.AddMilliseconds(httpClient.Timeout.TotalMilliseconds);

        // Act
        var result = exportClient.SendExportRequest(buffer, buffer.Length, deadlineUtc);
        var httpRequest = testHttpHandler.HttpRequestMessage;

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(httpRequest);
        Assert.Equal(HttpMethod.Post, httpRequest.Method);
        Assert.NotNull(httpRequest.Content);

        if (compressPayload)
        {
            Assert.Contains(httpRequest.Content.Headers, h => h.Key == "Content-Encoding" && h.Value.First() == "gzip");

            var content = testHttpHandler.HttpRequestContent;
            Assert.NotNull(content);
            // using var compressedStream = new MemoryStream(content);
            // using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
            // using var decompressedStream = new MemoryStream();
            // gzipStream.CopyTo(decompressedStream);
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
    [InlineData(true)]
    [InlineData(false)]
    public void SendExportRequest_SendsCorrectContent_Grpc(bool compressPayload)
    {
        // Arrange
        var options = new OtlpExporterOptions
        {
            Endpoint = new Uri("http://localhost:4317"),
            CompressPayload = compressPayload,
        };

        // var originalPayload = Enumerable.Repeat((byte)65, 1000).ToArray();
        // var buffer = new byte[originalPayload.Length + 5];
        // Buffer.BlockCopy(originalPayload, 0, buffer, 5, originalPayload.Length);
        // buffer[0] = compressPayload ? (byte)1 : (byte)0;
        // BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(1, 4), (uint)(buffer.Length - 5));
        var payload = Enumerable.Repeat((byte)65, 1000).ToArray();
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
        Assert.True(requestContent.Length > 5); // gRPC frame must be at least 5 bytes

        var compressionFlag = requestContent[0];
        var declaredLength = BinaryPrimitives.ReadUInt32BigEndian(requestContent.AsSpan(1, 4));
        var body = requestContent.AsSpan(5, (int)declaredLength).ToArray();

        Assert.Equal(compressPayload ? 1 : 0, compressionFlag);
        Assert.Equal(body.Length, (int)declaredLength);

        if (compressPayload)
        {
            // using var bodyStream = new MemoryStream(body);
            // using var gzipStream = new GZipStream(bodyStream, CompressionMode.Decompress);
            // using var decompressedStream = new MemoryStream();
            // gzipStream.CopyTo(decompressedStream);
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
