// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable SA1649 // File name should match first type name

namespace OpenTelemetry.Metrics;

[Flags]
internal enum MetricPointBehaviors
{
#pragma warning disable SA1602 // Enumeration items should be documented
    None = 0,
    Long = 1,
    Double = 1 << 1,
    Counter = 1 << 2,
    Gauge = 1 << 3,
    CumulativeAggregation = 1 << 4,
    DeltaAggregation = 1 << 5,
    HistogramAggregation = 1 << 6,
    HistogramRecordMinMax = 1 << 7,
    HistogramWithoutBuckets = 1 << 8,
    HistogramWithExponentialBuckets = 1 << 9,
#pragma warning restore SA1602 // Enumeration items should be documented
}
