// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Enumeration used to define the aggregation temporality for a <see
/// cref="Metric"/>.
/// </summary>
public enum AggregationTemporality : byte
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
