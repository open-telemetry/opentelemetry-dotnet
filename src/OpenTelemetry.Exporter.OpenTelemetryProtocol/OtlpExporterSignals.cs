// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Exporter;

[Flags]
internal enum OtlpExporterSignals
{
#pragma warning disable SA1602 // Enumeration items should be documented
    All = Logs | Metrics | Traces,
    None = 0b0,
    Logs = 0b1,
    Metrics = 0b10,
    Traces = 0b100,
#pragma warning restore SA1602 // Enumeration items should be documented
}
