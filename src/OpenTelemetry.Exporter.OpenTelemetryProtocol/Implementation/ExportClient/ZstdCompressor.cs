// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET11_0_OR_GREATER

using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.Versioning;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

/// <summary>
/// Compresses OTLP payloads with Zstandard, reusing a single native encoder between exports.
/// </summary>
/// <remarks>
/// See https://github.com/open-telemetry/opentelemetry-specification/pull/5321.
/// </remarks>
internal sealed class ZstdCompressor
{
    /// <summary>
    /// The largest window an encoder should use (8 MiB), as recommended for HTTP
    /// interoperability by RFC 9659 section 3.
    /// </summary>
    internal const int MaxWindowLog2 = 23;

    private ZstandardEncoder? cachedEncoder;

    [UnsupportedOSPlatformGuard("browser")]
    [UnsupportedOSPlatformGuard("wasi")]
    internal static bool IsSupported => !OperatingSystem.IsBrowser() && !OperatingSystem.IsWasi();

    public void Compress(ReadOnlySpan<byte> source, Stream destination)
    {
        Debug.Assert(IsSupported, "Zstandard compression is not supported on this platform.");

        var encoder = this.RentEncoder();

        try
        {
            using var zstd = CreateStream(destination, encoder, source.Length);
            zstd.Write(source);
        }
        catch
        {
            // The encoder's state is unknown, so do not reuse it.
            encoder.Dispose();
            throw;
        }

        this.ReturnEncoder(encoder);
    }

    public async Task CompressAsync(ReadOnlyMemory<byte> source, Stream destination, CancellationToken cancellationToken)
    {
        Debug.Assert(IsSupported, "Zstandard compression is not supported on this platform.");

        var encoder = this.RentEncoder();

        try
        {
            var zstd = CreateStream(destination, encoder, source.Length);
            await using (zstd.ConfigureAwait(false))
            {
                await zstd.WriteAsync(source, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            encoder.Dispose();
            throw;
        }

        this.ReturnEncoder(encoder);
    }

    public void Shutdown()
    {
        Debug.Assert(IsSupported, "Zstandard compression is not supported on this platform.");

        Interlocked.Exchange(ref this.cachedEncoder, null)?.Dispose();
    }

    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("wasi")]
    private static ZstandardStream CreateStream(Stream destination, ZstandardEncoder encoder, int sourceLength)
    {
        var zstd = new ZstandardStream(destination, encoder, leaveOpen: true);

        // Pledging the size up front writes a single frame that records the content size, and lets
        // the encoder size its window (and so its memory use) to the payload rather than MaxWindowLog2.
        zstd.SetSourceLength(sourceLength);

        return zstd;
    }

    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("wasi")]
    private ZstandardEncoder RentEncoder()
        => Interlocked.Exchange(ref this.cachedEncoder, null) ?? new ZstandardEncoder(ZstandardCompressionOptions.DefaultQuality, MaxWindowLog2);

    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("wasi")]
    private void ReturnEncoder(ZstandardEncoder encoder)
    {
        encoder.Reset();

        if (Interlocked.CompareExchange(ref this.cachedEncoder, encoder, null) != null)
        {
            // Another export returned an encoder first.
            encoder.Dispose();
        }
    }
}

#endif
