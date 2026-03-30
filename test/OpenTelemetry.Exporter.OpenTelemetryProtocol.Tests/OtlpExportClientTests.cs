// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net;
using System.Text;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests;

public class OtlpExportClientTests
{
    private const int MessageSizeLimit = 4 * 1024 * 1024;

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
    public void TryGetResponseBody_HttpResponseWithoutContent_ReturnsCorrectResult()
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

        using var httpResponse = new HttpResponseMessage()
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

        using var httpResponse = new HttpResponseMessage()
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

        using var httpResponse = new HttpResponseMessage()
        {
            Content = new ThrowingHttpContent(),
        };

        // Act
        var actual = OtlpExportClient.TryGetResponseBody(httpResponse, cancellationToken);

        // Assert
        Assert.Null(actual);
    }

    [Fact]
    public void TryGetResponseBody_CancellationTokenSignalled_ReturnsNull()
    {
        // Arrange
        var cancellationToken = new CancellationToken(canceled: true);

        using var httpResponse = new HttpResponseMessage()
        {
            Content = new StringContent("foo"),
        };

        // Act
        var actual = OtlpExportClient.TryGetResponseBody(httpResponse, cancellationToken);

        // Assert
        Assert.Null(actual);
    }

    [Fact]
    public void TryGetResponseBody_NonSeekableStream_ReturnsContent()
    {
        // Arrange
        var expected = "non-seekable response body";
        var cancellationToken = CancellationToken.None;

        using var httpResponse = new HttpResponseMessage()
        {
            Content = new NonSeekableStreamContent(Encoding.UTF8.GetBytes(expected)),
        };

        // Act
        var actual = OtlpExportClient.TryGetResponseBody(httpResponse, cancellationToken);

        // Assert
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryGetResponseBody_NonUtf8Charset_ReturnsCorrectlyDecodedContent()
    {
        // Arrange
        var expected = "iso-8859-1 response body: caf\u00e9";
        var cancellationToken = CancellationToken.None;
        var iso8859 = Encoding.GetEncoding("iso-8859-1");

        using var httpResponse = new HttpResponseMessage()
        {
            Content = new StringContent(expected, iso8859),
        };

        // Act
        var actual = OtlpExportClient.TryGetResponseBody(httpResponse, cancellationToken);

        // Assert
        Assert.Equal(expected, actual);
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

    private sealed class NonSeekableStreamContent : HttpContent
    {
        private readonly byte[] data;

        public NonSeekableStreamContent(byte[] data)
        {
            this.data = data;
            this.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain") { CharSet = "utf-8" };
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(this.data, 0, this.data.Length);

#if NET
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
            => stream.WriteAsync(this.data, cancellationToken).AsTask();

        protected override Stream CreateContentReadStream(CancellationToken cancellationToken)
            => new NonSeekableStream(new MemoryStream(this.data));
#endif

        protected override Task<Stream> CreateContentReadStreamAsync()
            => Task.FromResult<Stream>(new NonSeekableStream(new MemoryStream(this.data)));

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }

    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
