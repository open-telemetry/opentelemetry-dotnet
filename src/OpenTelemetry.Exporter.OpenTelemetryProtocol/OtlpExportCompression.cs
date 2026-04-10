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
    Gzip = 1,
}
