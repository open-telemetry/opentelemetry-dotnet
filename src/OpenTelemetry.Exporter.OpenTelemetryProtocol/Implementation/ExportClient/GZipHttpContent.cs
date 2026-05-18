// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.IO.Compression;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using System.Net.Http.Headers;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

internal sealed class GZipHttpContent : HttpContent
{
#if NET
    private readonly ReadOnlyMemory<byte> buffer;
#else
    private readonly byte[] buffer;
    private readonly int contentLength;
#endif

    public GZipHttpContent(byte[] buffer, int contentLength, MediaTypeHeaderValue mediaType)
    {
#if NET
        this.buffer = buffer.AsMemory(0, contentLength);
#else
        this.buffer = buffer;
        this.contentLength = contentLength;
#endif
        this.Headers.ContentType = mediaType;
        this.Headers.ContentEncoding.Add("gzip");
    }

    protected override bool TryComputeLength(out long length)
    {
        length = -1;
        return false;
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
#if NET
        await this.SerializeToStreamAsync(stream, context, CancellationToken.None).ConfigureAwait(false);
#else
        using var gzip = CreateGZipStream(stream);
#if NETSTANDARD2_1_OR_GREATER
        await gzip.WriteAsync(this.buffer.AsMemory(0, this.contentLength)).ConfigureAwait(false);
#else
        await gzip.WriteAsync(this.buffer, 0, this.contentLength).ConfigureAwait(false);
#endif
        await gzip.FlushAsync().ConfigureAwait(false);
#endif
    }

#if NET
    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        var gzip = CreateGZipStream(stream);
        await using (gzip.ConfigureAwait(false))
        {
            await gzip.WriteAsync(this.buffer, cancellationToken).ConfigureAwait(false);
            await gzip.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    protected override void SerializeToStream(Stream stream, TransportContext? context, CancellationToken cancellationToken)
    {
        using var gzip = CreateGZipStream(stream);
        gzip.Write(this.buffer.Span);
        gzip.Flush();
    }
#endif

    private static GZipStream CreateGZipStream(Stream stream)
        => new(stream, CompressionLevel.Fastest, leaveOpen: true);
}
