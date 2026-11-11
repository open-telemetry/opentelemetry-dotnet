// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Enumeration used to define the aggregation type for a <see cref="Metric"/> stream.
/// </summary>
public enum AggregationKind
{
    /// <summary>
    /// The Sum Aggregation informs the SDK to collect data for the Sum Metric
    /// Point.
    /// </summary>
    Sum = 0,

    /// <summary>
    /// The Gauge Aggregation informs the SDK to collect data for the
    /// Gauge Metric Point.
    /// </summary>
    Gauge = 1,

    /// <summary>
    /// The Explicit Bucket Histogram Aggregation informs the SDK to collect
    /// data for the Histogram Metric Point.
    /// </summary>
    Histogram = 2,

    /// <summary>
    /// The Base2 Exponential Histogram Aggregation informs the SDK to collect
    /// data for the Exponential Histogram Metric Point.
    /// </summary>
    ExponentialHistogram = 3,

    /// <summary>
    /// The Drop Aggregation informs the SDK to ignore/drop all Instrument
    /// Measurements for this Aggregation.
    /// </summary>
    Drop = 4,
}
