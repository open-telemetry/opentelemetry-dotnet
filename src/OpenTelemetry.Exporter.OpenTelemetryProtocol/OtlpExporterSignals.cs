// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Exporter;

[Flags]
public enum OtlpExporterSignals
{
    All = Logs | Metrics | Traces,
    None = 0b0,
    Logs = 0b1,
    Metrics = 0b10,
    Traces = 0b100,
}
