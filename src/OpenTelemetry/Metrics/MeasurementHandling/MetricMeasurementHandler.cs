// <copyright file="MetricMeasurementHandler.cs" company="OpenTelemetry Authors">
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

internal abstract class MetricMeasurementHandler
{
    public static Dictionary<MetricBehaviors, MetricMeasurementHandler> Definitions { get; } = new()
    {
        [MetricBehaviors.Sum | MetricBehaviors.Cumulative] = new SumAndGaugeMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.CumulativeSum>(),
        [MetricBehaviors.Sum | MetricBehaviors.Cumulative | MetricBehaviors.OfferExemplar] = new SumAndGaugeMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.CumulativeSumWithExemplar>(),
        [MetricBehaviors.Sum | MetricBehaviors.Delta] = new SumAndGaugeMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.DeltaSum>(),
        [MetricBehaviors.Sum | MetricBehaviors.Delta | MetricBehaviors.OfferExemplar] = new SumAndGaugeMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.DeltaSumWithExemplar>(),

        [MetricBehaviors.Gauge] = new SumAndGaugeMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.Gauge>(),
        [MetricBehaviors.Gauge | MetricBehaviors.OfferExemplar] = new SumAndGaugeMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.GaugeWithExemplar>(),

        [MetricBehaviors.Histogram] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.Histogram>(),
        [MetricBehaviors.Histogram | MetricBehaviors.OfferExemplar] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.HistogramWithExemplar>(),
        [MetricBehaviors.Histogram | MetricBehaviors.HistogramRecordMinMax] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.HistogramWithMinMax>(),
        [MetricBehaviors.Histogram | MetricBehaviors.HistogramRecordMinMax | MetricBehaviors.OfferExemplar] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.HistogramWithMinMaxAndExemplar>(),

        [MetricBehaviors.Histogram | MetricBehaviors.HistogramWithoutBuckets] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.HistogramWithoutBuckets>(),
        [MetricBehaviors.Histogram | MetricBehaviors.HistogramWithoutBuckets | MetricBehaviors.OfferExemplar] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.HistogramWithoutBucketsAndWithExemplar>(),
        [MetricBehaviors.Histogram | MetricBehaviors.HistogramWithoutBuckets | MetricBehaviors.HistogramRecordMinMax] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.HistogramWithoutBucketsAndWithMinMax>(),
        [MetricBehaviors.Histogram | MetricBehaviors.HistogramWithoutBuckets | MetricBehaviors.HistogramRecordMinMax | MetricBehaviors.OfferExemplar] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.HistogramWithoutBucketsAndWithMinMaxAndExemplar>(),

        [MetricBehaviors.Histogram | MetricBehaviors.HistogramWithExponentialBuckets] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.HistogramWithExponentialBuckets>(),
        [MetricBehaviors.Histogram | MetricBehaviors.HistogramWithExponentialBuckets | MetricBehaviors.OfferExemplar] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.HistogramWithExponentialBucketsAndExemplar>(),
        [MetricBehaviors.Histogram | MetricBehaviors.HistogramWithExponentialBuckets | MetricBehaviors.HistogramRecordMinMax] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.HistogramWithExponentialBucketsAndMinMax>(),
        [MetricBehaviors.Histogram | MetricBehaviors.HistogramWithExponentialBuckets | MetricBehaviors.HistogramRecordMinMax | MetricBehaviors.OfferExemplar] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.HistogramWithExponentialBucketsAndMinMaxAndExemplar>(),
    };

    public abstract void RecordMeasurement(
        ref MetricPoint metricPoint,
        long value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        bool isSampled);

    public abstract void RecordMeasurement(
        ref MetricPoint metricPoint,
        double value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        bool isSampled);
}
