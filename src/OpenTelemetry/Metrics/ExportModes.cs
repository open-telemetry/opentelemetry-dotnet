// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Describes the mode of a metric exporter.
/// </summary>
[Flags]
#pragma warning disable CA1028 // Enum storage should be Int32
public enum ExportModes : byte
#pragma warning restore CA1028 // Enum storage should be Int32
{
    /*
    0 0 0 0 0 0 0 0
    | | | | | | | |
    | | | | | | | +--- Push
    | | | | | | +----- Pull
    | | | | | +------- (reserved)
    | | | | +--------- (reserved)
    | | | +----------- (reserved)
    | | +------------- (reserved)
    | +--------------- (reserved)
    +----------------- (reserved)
    */

    /// <summary>
    /// Push.
    /// </summary>
    Push = 0b1,

    /// <summary>
    /// Pull.
    /// </summary>
    Pull = 0b10,
}
