// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

/// <summary>
/// Represents the different types of signals that can be exported.
/// </summary>
internal enum OtlpSignalType
{
    /// <summary>
    /// Represents trace signals.
    /// </summary>
    Traces = 0,

    /// <summary>
    /// Represents metric signals.
    /// </summary>
    Metrics = 1,

    /// <summary>
    /// Represents log signals.
    /// </summary>
    Logs = 2,
}
