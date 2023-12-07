// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

internal static class MetricPointBehaviorDefinitions
{
    [MetricPointBehaviors(MetricPointBehaviors.CumulativeAggregation | MetricPointBehaviors.Counter | MetricPointBehaviors.Long)]
    public struct CumulativeCounterLong
    {
    }

    [MetricPointBehaviors(MetricPointBehaviors.CumulativeAggregation | MetricPointBehaviors.Counter | MetricPointBehaviors.Double)]
    public struct CumulativeCounterDouble
    {
    }

    [MetricPointBehaviors(MetricPointBehaviors.DeltaAggregation | MetricPointBehaviors.Counter | MetricPointBehaviors.Long)]
    public struct DeltaCounterLong
    {
    }

    [MetricPointBehaviors(MetricPointBehaviors.DeltaAggregation | MetricPointBehaviors.Counter | MetricPointBehaviors.Double)]
    public struct DeltaCounterDouble
    {
    }

    [MetricPointBehaviors(MetricPointBehaviors.CumulativeAggregation | MetricPointBehaviors.Gauge | MetricPointBehaviors.Long)]
    public struct CumulativeGaugeLong
    {
    }

    [MetricPointBehaviors(MetricPointBehaviors.CumulativeAggregation | MetricPointBehaviors.Gauge | MetricPointBehaviors.Double)]
    public struct CumulativeGaugeDouble
    {
    }

    [MetricPointBehaviors(MetricPointBehaviors.HistogramAggregation | MetricPointBehaviors.Double)]
    public struct Histogram
    {
    }

    [MetricPointBehaviors(MetricPointBehaviors.HistogramAggregation | MetricPointBehaviors.Double | MetricPointBehaviors.HistogramRecordMinMax)]
    public struct HistogramWithMinMax
    {
    }

    [MetricPointBehaviors(MetricPointBehaviors.HistogramAggregation | MetricPointBehaviors.Double | MetricPointBehaviors.HistogramWithoutBuckets)]
    public struct HistogramWithoutBuckets
    {
    }

    [MetricPointBehaviors(MetricPointBehaviors.HistogramAggregation | MetricPointBehaviors.Double | MetricPointBehaviors.HistogramWithoutBuckets | MetricPointBehaviors.HistogramRecordMinMax)]
    public struct HistogramWithoutBucketsAndWithMinMax
    {
    }

    [MetricPointBehaviors(MetricPointBehaviors.HistogramAggregation | MetricPointBehaviors.Double | MetricPointBehaviors.HistogramWithExponentialBuckets)]
    public struct HistogramWithExponentialBuckets
    {
    }

    [MetricPointBehaviors(MetricPointBehaviors.HistogramAggregation | MetricPointBehaviors.Double | MetricPointBehaviors.HistogramWithExponentialBuckets | MetricPointBehaviors.HistogramRecordMinMax)]
    public struct HistogramWithExponentialBucketsAndMinMax
    {
    }
}
