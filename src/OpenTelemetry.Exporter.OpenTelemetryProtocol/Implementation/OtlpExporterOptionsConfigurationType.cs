// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

[Flags]
internal enum OtlpExporterOptionsConfigurationType
{
#pragma warning disable SA1602 // Enumeration items should be documented
    Default,
    Logs,
    Metrics,
    Traces,
#pragma warning restore SA1602 // Enumeration items should be documented
}
