// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Enumeration used to define the aggregation temporality for a <see
/// cref="Metric"/>.
/// </summary>
#pragma warning disable CA1008 // Enums should have zero value
#pragma warning disable CA1028 // Enum storage should be Int32
public enum AggregationTemporality : byte
#pragma warning restore CA1028 // Enum storage should be Int32
#pragma warning restore CA1008 // Enums should have zero value
{
    /// <summary>
    /// Cumulative.
    /// </summary>
    Cumulative = 0b1,

    /// <summary>
    /// Delta.
    /// </summary>
    Delta = 0b10,
}
