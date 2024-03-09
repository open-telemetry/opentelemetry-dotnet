// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

/// <summary>
/// Contains spec environment variable key definitions for OpenTelemetry Protocol (OTLP) exporter.
/// </summary>
/// <remarks>
/// Specification: <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md"/>.
/// </remarks>
internal static class OtlpExporterSpecEnvVarKeyDefinitions
{
    public const string DefaultEndpointEnvVarName = "OTEL_EXPORTER_OTLP_ENDPOINT";
    public const string DefaultHeadersEnvVarName = "OTEL_EXPORTER_OTLP_HEADERS";
    public const string DefaultTimeoutEnvVarName = "OTEL_EXPORTER_OTLP_TIMEOUT";
    public const string DefaultProtocolEnvVarName = "OTEL_EXPORTER_OTLP_PROTOCOL";

    public const string MetricsTemporalityPreferenceEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE";
}
