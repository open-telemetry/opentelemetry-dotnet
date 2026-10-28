// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

/// <summary>
/// Compression methods supported by the OTLP exporter.
/// </summary>
/// <remarks>
/// Specification: <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md"/>.
/// </remarks>
public enum OtlpExportCompression
{
    /// <summary>
    /// No compression.
    /// </summary>
    None = 0,

    /// <summary>
    /// Compress with GZip.
    /// </summary>
    GZip = 1,

    /// <summary>
    /// Compress with Zstandard (zstd).
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>Zstandard compression is only available when running on .NET 11
    /// or later, and is not available in the browser or on WASI. Where it is
    /// not available, <see cref="GZip"/> is used instead.</item>
    /// <item>If the receiver rejects a Zstandard-compressed request, the
    /// request is sent again once using <see cref="GZip"/>. If that succeeds,
    /// <see cref="GZip"/> is used for all subsequent requests.</item>
    /// </list>
    /// </remarks>
    Zstd = 2,
}
