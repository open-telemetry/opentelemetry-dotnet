// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

[Flags]
public enum ConsoleExporterOutputTargets
{
    /// <summary>
    /// Output to the Console (stdout).
    /// </summary>
    Console = 0b1,

    /// <summary>
    /// Output to the Debug trace.
    /// </summary>
    Debug = 0b10,
}
