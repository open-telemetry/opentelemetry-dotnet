// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Defines the default histogram aggregation for a <see cref="MetricReader" />.
/// </summary>
internal enum MetricReaderHistogramAggregation
{
    /// <summary>
    /// Explicit bucket histogram aggregation.
    /// </summary>
    ExplicitBucketHistogram = 0,

    /// <summary>
    /// Base2 exponential bucket histogram aggregation.
    /// </summary>
    Base2ExponentialBucketHistogram = 1,
}
