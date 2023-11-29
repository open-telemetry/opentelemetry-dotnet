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
        [MetricBehaviors.Histogram | MetricBehaviors.HistogramRecordMinMax] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.HistogramWitMinMax>(),
        [MetricBehaviors.Histogram | MetricBehaviors.HistogramRecordMinMax | MetricBehaviors.OfferExemplar] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.HistogramWitMinMaxAndExemplar>(),

        [MetricBehaviors.HistogramWithBuckets] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.HistogramWithBuckets>(),
        [MetricBehaviors.HistogramWithBuckets | MetricBehaviors.OfferExemplar] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.HistogramWithBucketsAndExemplar>(),
        [MetricBehaviors.HistogramWithBuckets | MetricBehaviors.HistogramRecordMinMax] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.HistogramWithBucketsAndMinMax>(),
        [MetricBehaviors.HistogramWithBuckets | MetricBehaviors.HistogramRecordMinMax | MetricBehaviors.OfferExemplar] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.HistogramWithBucketsAndMinMaxAndExemplar>(),

        [MetricBehaviors.ExponentialHistogram] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.ExponentialHistogram>(),
        [MetricBehaviors.ExponentialHistogram | MetricBehaviors.OfferExemplar] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.ExponentialHistogramWithExemplar>(),
        [MetricBehaviors.ExponentialHistogram | MetricBehaviors.HistogramRecordMinMax] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.ExponentialHistogramWitMinMax>(),
        [MetricBehaviors.ExponentialHistogram | MetricBehaviors.HistogramRecordMinMax | MetricBehaviors.OfferExemplar] = new HistogramMetricMeasurementHandler<MetricMeasurementHandlerSpecifications.ExponentialHistogramWitMinMaxAndExemplar>(),
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
