// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Transmission;

/// <summary>
/// Retry strategy used during export failures.
/// </summary>
internal enum RetryStrategy
{
    /// <summary>
    /// No Retry.
    /// </summary>
    None = 0,

    /// <summary>
    /// Retry by buffering telemetry in memory.
    /// </summary>
    InMemory = 1,

    /// <summary>
    /// Retry by buffering telemetry in storage.
    /// </summary>
    Storage = 2,
}
