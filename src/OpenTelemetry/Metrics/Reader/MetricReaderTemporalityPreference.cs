// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Defines the behavior of a <see cref="MetricReader" />
/// with respect to <see cref="AggregationTemporality" />.
/// </summary>
public enum MetricReaderTemporalityPreference
{
    /// <summary>
    /// All aggregations are performed using cumulative temporality.
    /// </summary>
    Cumulative = 1,

    /// <summary>
    /// All measurements that are monotonic in nature are aggregated using delta temporality.
    /// Aggregations of non-monotonic measurements use cumulative temporality.
    /// </summary>
    Delta = 2,
}
