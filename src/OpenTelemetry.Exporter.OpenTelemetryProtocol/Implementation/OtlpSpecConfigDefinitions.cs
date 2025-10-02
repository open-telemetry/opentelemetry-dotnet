// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

/// <summary>
/// Contains spec environment variable key definitions for OpenTelemetry Protocol (OTLP) exporter.
/// </summary>
/// <remarks>
/// Specification: <see href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md"/>.
/// </remarks>
internal static class OtlpSpecConfigDefinitions
{
    public const string EnvVarPrefix = "OTEL_";
    
    public const string DefaultEndpointEnvVarName = "OTEL_EXPORTER_OTLP_ENDPOINT";
    public const string DefaultHeadersEnvVarName = "OTEL_EXPORTER_OTLP_HEADERS";
    public const string DefaultTimeoutEnvVarName = "OTEL_EXPORTER_OTLP_TIMEOUT";
    public const string DefaultProtocolEnvVarName = "OTEL_EXPORTER_OTLP_PROTOCOL";

    public const string LogsEndpointEnvVarName = "OTEL_EXPORTER_OTLP_LOGS_ENDPOINT";
    public const string LogsHeadersEnvVarName = "OTEL_EXPORTER_OTLP_LOGS_HEADERS";
    public const string LogsTimeoutEnvVarName = "OTEL_EXPORTER_OTLP_LOGS_TIMEOUT";
    public const string LogsProtocolEnvVarName = "OTEL_EXPORTER_OTLP_LOGS_PROTOCOL";

    public const string MetricsEndpointEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT";
    public const string MetricsHeadersEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_HEADERS";
    public const string MetricsTimeoutEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_TIMEOUT";
    public const string MetricsProtocolEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_PROTOCOL";
    public const string MetricsTemporalityPreferenceEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_TEMPORALITY_PREFERENCE";

    public const string TracesEndpointEnvVarName = "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT";
    public const string TracesHeadersEnvVarName = "OTEL_EXPORTER_OTLP_TRACES_HEADERS";
    public const string TracesTimeoutEnvVarName = "OTEL_EXPORTER_OTLP_TRACES_TIMEOUT";
    public const string TracesProtocolEnvVarName = "OTEL_EXPORTER_OTLP_TRACES_PROTOCOL";
}
