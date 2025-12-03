// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

internal static class DiagnosticDefinitions
{
    public const string ExperimentalApiUrlFormat = "https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/diagnostics/experimental-apis/README.md#{0}";

    public const string LoggerProviderExperimentalApi = "OTEL1000";
    public const string LogsBridgeExperimentalApi = "OTEL1001";
    public const string ExemplarReservoirExperimentalApi = "OTEL1004";
    public const string AlwaysRecordSamplerExperimentalApi = "OTEL1005";

    /* Definitions which have been released stable:
    public const string ExemplarExperimentalApi = "OTEL1002";
    public const string CardinalityLimitExperimentalApi = "OTEL1003";
     */
}
