// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

/// <summary>
/// Supported compression methods for OTLP exporter according to the specification https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md.
/// </summary>
public enum OtlpExportCompression
{
    /// <summary>
    /// Compression is disabled.
    /// </summary>
    None = 0,

    /// <summary>
    /// Compress with Gzip.
    /// </summary>
    Gzip = 1,
}
