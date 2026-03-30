// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpExportClientTests
{
    private const int MessageSizeLimit = 32 * 1024;

    [Fact]
    public void TryGetResponseBody_NullHttpResponse_ReturnsNull()
    {
        // Arrange
        HttpResponseMessage? httpResponse = null;
        var cancellationToken = CancellationToken.None;

        // Act
        var actual = OtlpExportClient.TryGetResponseBody(httpResponse, cancellationToken);

        // Assert
        Assert.Null(actual);
    }

    [Fact]
    public void TryGetResponseBody_HttpResponseWithoutContent_ReturnsNull()
    {
        // Arrange
        using var httpResponse = new HttpResponseMessage() { Content = null };
        var cancellationToken = CancellationToken.None;

        // Act
        var actual = OtlpExportClient.TryGetResponseBody(httpResponse, cancellationToken);

        // Assert
#if NETFRAMEWORK
        Assert.Null(actual);
#else
        Assert.Equal(string.Empty, actual);
#endif
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(256)]
    [InlineData(1024)]
    [InlineData((30 * 1024) - 1)]
    [InlineData(30 * 1024)]
    public void TryGetResponseBody_SmallContent_ReturnsFullContent(int length)
    {
        // Arrange
        var expected = new string('A', length);
        var cancellationToken = CancellationToken.None;

        using var httpResponse = new HttpResponseMessage()
        {
            Content = new StringContent(expected),
        };

        // Act
        var actual = OtlpExportClient.TryGetResponseBody(httpResponse, cancellationToken);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1024)]
    [InlineData(2048)]
    public void TryGetResponseBody_ContentExceedsLimit_ReturnsTruncatedContent(int excess)
    {
        // Arrange
        var content = new string('C', MessageSizeLimit + excess);
        var cancellationToken = CancellationToken.None;

        using var httpResponse = new HttpResponseMessage
        {
            Content = new StringContent(content),
        };

        // Act
        var actual = OtlpExportClient.TryGetResponseBody(httpResponse, cancellationToken);

        // Assert
        Assert.NotNull(actual);
        Assert.Equal(MessageSizeLimit, actual.Length);
        Assert.Equal(new string('C', MessageSizeLimit), actual);
    }

    [Fact]
    public void TryGetResponseBody_EmptyContent_ReturnsEmptyString()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        using var httpResponse = new HttpResponseMessage
        {
            Content = new StringContent(string.Empty),
        };

        // Act
        var actual = OtlpExportClient.TryGetResponseBody(httpResponse, cancellationToken);

        // Assert
        Assert.Equal(string.Empty, actual);
    }

    [Fact]
    public void TryGetResponseBody_ExceptionDuringRead_ReturnsNull()
    {
        // Arrange
        var cancellationToken = CancellationToken.None;

        using var httpResponse = new HttpResponseMessage
        {
            Content = new ThrowingHttpContent(),
        };

        // Act
        var actual = OtlpExportClient.TryGetResponseBody(httpResponse, cancellationToken);

        // Assert
        Assert.Null(actual);
    }

    private sealed class ThrowingHttpContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => throw new InvalidOperationException("Test exception");

#if NET
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Test exception");
#endif

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
