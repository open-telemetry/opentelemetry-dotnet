// <copyright file="MetricPointBehaviorDefinitions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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
