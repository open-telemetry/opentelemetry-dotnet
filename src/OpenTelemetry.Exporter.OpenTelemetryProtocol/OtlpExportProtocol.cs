// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

/// <summary>
/// Supported by OTLP exporter protocol types according to the specification https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md.
/// </summary>
#pragma warning disable CA1028 // Enum storage should be Int32
public enum OtlpExportProtocol : byte
#pragma warning restore CA1028 // Enum storage should be Int32
{
    /// <summary>
    /// OTLP over gRPC (corresponds to 'grpc' Protocol configuration option). Used as default.
    /// </summary>
#if NETFRAMEWORK || NETSTANDARD2_0
    [Obsolete("CAUTION: OTLP/gRPC is no longer supported for .NET Framework or .NET Standard targets without supplying a properly configured HttpClientFactory. It is strongly encouraged that you migrate to using OTLP/HTTPPROTOBUF.")]
#endif
    Grpc = 0,

    /// <summary>
    /// OTLP over HTTP with protobuf payloads (corresponds to 'http/protobuf' Protocol configuration option).
    /// </summary>
    HttpProtobuf = 1,
}
