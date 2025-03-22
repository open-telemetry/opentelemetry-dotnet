// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

internal enum AggregationType
{
    /// <summary>
    /// Calculate SUM from incoming delta measurements.
    /// </summary>
    LongSumIncomingDelta = 0,

    /// <summary>
    /// Calculate SUM from incoming cumulative measurements.
    /// </summary>
    LongSumIncomingCumulative = 1,

    /// <summary>
    /// Calculate SUM from incoming delta measurements.
    /// </summary>
    DoubleSumIncomingDelta = 2,

    /// <summary>
    /// Calculate SUM from incoming cumulative measurements.
    /// </summary>
    DoubleSumIncomingCumulative = 3,

    /// <summary>
    /// Keep LastValue.
    /// </summary>
    LongGauge = 4,

    /// <summary>
    /// Keep LastValue.
    /// </summary>
    DoubleGauge = 5,

    /// <summary>
    /// Histogram with sum, count, buckets.
    /// </summary>
    HistogramWithBuckets = 6,

    /// <summary>
    /// Histogram with sum, count, min, max, buckets.
    /// </summary>
    HistogramWithMinMaxBuckets = 7,

    /// <summary>
    /// Histogram with sum, count.
    /// </summary>
    Histogram = 8,

    /// <summary>
    /// Histogram with sum, count, min, max.
    /// </summary>
    HistogramWithMinMax = 9,

    /// <summary>
    /// Exponential Histogram with sum, count.
    /// </summary>
    Base2ExponentialHistogram = 10,

    /// <summary>
    /// Exponential Histogram with sum, count, min, max.
    /// </summary>
    Base2ExponentialHistogramWithMinMax = 11,
}
