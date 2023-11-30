// <copyright file="HistogramMetricMeasurementHandler.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

internal sealed class HistogramMetricMeasurementHandler<T> : MetricMeasurementHandler
    where T : struct // <- Note: T must be a struct to have specialized code generated based on the type
{
    // Note: These flags are designed so that code which is not needed can be elided by the JIT
    private static readonly bool IsExponential = typeof(IHistogramWithExponentialBucketsMetricBehavior).IsAssignableFrom(typeof(T));
    private static readonly bool HasBuckets = !typeof(IHistogramWithoutBucketsMetricBehavior).IsAssignableFrom(typeof(T));
    private static readonly bool RecordMinMax = typeof(IHistogramRecordMinMaxMetricBehavior).IsAssignableFrom(typeof(T));
    private static readonly bool OfferExemplar = typeof(IOfferExemplarMetricBehavior).IsAssignableFrom(typeof(T));

    public override void RecordMeasurement(
        ref MetricPoint metricPoint,
        long value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        bool isSampled)
    {
        this.RecordMeasurement(ref metricPoint, (double)value, tags, isSampled);
    }

    public override void RecordMeasurement(
        ref MetricPoint metricPoint,
        double value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        bool isSampled)
    {
        Debug.Assert(metricPoint.OptionalComponents != null, "mpComponents was null");

        if (IsExponential)
        {
            RecordMeasurementExponentialHistogram(ref metricPoint, value, tags, isSampled);
        }
        else
        {
            RecordMeasurementHistogram(ref metricPoint, value, tags, isSampled);
        }
    }

    private static void RecordMeasurementHistogram(
        ref MetricPoint metricPoint,
        double value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        bool isSampled)
    {
        Debug.Assert(metricPoint.OptionalComponents!.HistogramBuckets != null, "HistogramBuckets was null");

        var histogramBuckets = metricPoint.OptionalComponents!.HistogramBuckets!;

        int bucketIndex;
        if (HasBuckets)
        {
            bucketIndex = histogramBuckets!.FindBucketIndex(value);
        }
        else
        {
            bucketIndex = 0;
        }

        MetricPoint.AcquireLock(ref histogramBuckets.IsCriticalSectionOccupied);

        unchecked
        {
            metricPoint.RunningValue.AsLong++;
            histogramBuckets.RunningSum += value;
            if (HasBuckets)
            {
                Debug.Assert(histogramBuckets.RunningBucketCounts != null, "histogramBuckets.RunningBucketCounts was null");

                histogramBuckets.RunningBucketCounts![bucketIndex]++;
            }
        }

        if (OfferExemplar && isSampled)
        {
            Debug.Assert(metricPoint.OptionalComponents.ExemplarReservoir != null, "ExemplarReservoir was null");

            // TODO: Need to ensure that the lock is always released.
            // A custom implementation of `ExemplarReservoir.Offer` might throw an exception.
            metricPoint.OptionalComponents.ExemplarReservoir!.Offer(value, tags, bucketIndex);
        }

        if (RecordMinMax)
        {
            histogramBuckets.RunningMin = Math.Min(histogramBuckets.RunningMin, value);
            histogramBuckets.RunningMax = Math.Max(histogramBuckets.RunningMax, value);
        }

        MetricPoint.ReleaseLock(ref histogramBuckets.IsCriticalSectionOccupied);
    }

    private static void RecordMeasurementExponentialHistogram(
        ref MetricPoint metricPoint,
        double value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        bool isSampled)
    {
        Debug.Assert(metricPoint.OptionalComponents!.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

        if (value >= 0)
        {
            var histogram = metricPoint.OptionalComponents!.Base2ExponentialBucketHistogram;

            MetricPoint.AcquireLock(ref histogram!.IsCriticalSectionOccupied);

            unchecked
            {
                metricPoint.RunningValue.AsLong++;
                histogram.RunningSum += value;
                histogram.Record(value);
            }

            if (OfferExemplar && isSampled)
            {
                Debug.Assert(metricPoint.OptionalComponents.ExemplarReservoir != null, "ExemplarReservoir was null");

                // TODO: Exemplars for exponential histograms will be a follow up PR
            }

            if (RecordMinMax)
            {
                histogram.RunningMin = Math.Min(histogram.RunningMin, value);
                histogram.RunningMax = Math.Max(histogram.RunningMax, value);
            }

            MetricPoint.ReleaseLock(ref histogram.IsCriticalSectionOccupied);
        }
    }
}
