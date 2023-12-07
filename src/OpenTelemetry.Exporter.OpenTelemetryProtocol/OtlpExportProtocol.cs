// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

/// <summary>
/// Supported by OTLP exporter protocol types according to the specification https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md.
/// </summary>
public enum OtlpExportProtocol : byte
{
    /// <summary>
    /// OTLP over gRPC (corresponds to 'grpc' Protocol configuration option). Used as default.
    /// </summary>
    Grpc = 0,

    /// <summary>
    /// OTLP over HTTP with protobuf payloads (corresponds to 'http/protobuf' Protocol configuration option).
    /// </summary>
    HttpProtobuf = 1,
}
