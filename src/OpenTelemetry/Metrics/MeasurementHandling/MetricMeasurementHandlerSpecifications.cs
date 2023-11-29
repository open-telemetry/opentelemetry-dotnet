// <copyright file="MetricMeasurementHandlerSpecifications.cs" company="OpenTelemetry Authors">
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

internal static class MetricMeasurementHandlerSpecifications
{
    public struct CumulativeSum
    {
    }

    public struct CumulativeSumWithExemplar : IOfferExemplarMetricBehavior
    {
    }

    public struct DeltaSum : IDeltaMetricBehavior
    {
    }

    public struct DeltaSumWithExemplar : IDeltaMetricBehavior, IOfferExemplarMetricBehavior
    {
    }

    public struct Gauge
    {
    }

    public struct GaugeWithExemplar : IOfferExemplarMetricBehavior
    {
    }

    public struct Histogram
    {
    }

    public struct HistogramWithExemplar : IOfferExemplarMetricBehavior
    {
    }

    public struct HistogramWithBuckets : IHistogramBucketsMetricBehavior
    {
    }

    public struct HistogramWithBucketsAndExemplar : IHistogramBucketsMetricBehavior, IOfferExemplarMetricBehavior
    {
    }

    public struct HistogramWithBucketsAndMinMax : IHistogramBucketsMetricBehavior, IHistogramRecordMinMaxMetricBehavior
    {
    }

    public struct HistogramWithBucketsAndMinMaxAndExemplar : IHistogramBucketsMetricBehavior, IHistogramRecordMinMaxMetricBehavior, IOfferExemplarMetricBehavior
    {
    }

    public struct HistogramWitMinMax : IHistogramRecordMinMaxMetricBehavior
    {
    }

    public struct HistogramWitMinMaxAndExemplar : IHistogramRecordMinMaxMetricBehavior, IOfferExemplarMetricBehavior
    {
    }

    public struct ExponentialHistogram : IExponentialHistogramMetricBehavior
    {
    }

    public struct ExponentialHistogramWithExemplar : IExponentialHistogramMetricBehavior, IOfferExemplarMetricBehavior
    {
    }

    public struct ExponentialHistogramWitMinMax : IExponentialHistogramMetricBehavior, IHistogramRecordMinMaxMetricBehavior
    {
    }

    public struct ExponentialHistogramWitMinMaxAndExemplar : IExponentialHistogramMetricBehavior, IHistogramRecordMinMaxMetricBehavior, IOfferExemplarMetricBehavior
    {
    }
}
