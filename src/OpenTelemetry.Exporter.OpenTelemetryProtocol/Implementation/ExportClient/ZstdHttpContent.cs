// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET11_0_OR_GREATER

using System.Net;
using System.Net.Http.Headers;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

internal sealed class ZstdHttpContent : HttpContent
{
    private readonly ReadOnlyMemory<byte> buffer;
    private readonly ZstdCompressor compressor;

    public ZstdHttpContent(byte[] buffer, int contentLength, MediaTypeHeaderValue mediaType, ZstdCompressor compressor)
    {
        this.buffer = buffer.AsMemory(0, contentLength);
        this.compressor = compressor;
        this.Headers.ContentType = mediaType;
        this.Headers.ContentEncoding.Add("zstd");
    }

    protected override bool TryComputeLength(out long length)
    {
        length = -1;
        return false;
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => this.SerializeToStreamAsync(stream, context, CancellationToken.None);

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        => this.compressor.CompressAsync(this.buffer, stream, cancellationToken);

    protected override void SerializeToStream(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        => this.compressor.Compress(this.buffer.Span, stream);
}

#endif
