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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Metrics;

internal sealed class MetricMeasurementHandler<TAggregatorBehavior, TMetricPointBehavior> : IMetricMeasurementHandler
    where TAggregatorBehavior : struct // <- Note: T must be a struct to have specialized code generated based on the type
    where TMetricPointBehavior : struct // <- Note: T must be a struct to have specialized code generated based on the type
{
    // Note: These flags are designed so that code which is not needed can be elided by the JIT
    private static readonly bool IsCumulativeTemporality = MetricMeasurementHandlerHelper.IsAggregatorBehaviorDefined<TAggregatorBehavior>(MetricAggregatorBehaviors.CumulativeTemporality);
    private static readonly bool IsDeltaTemporality = MetricMeasurementHandlerHelper.IsAggregatorBehaviorDefined<TAggregatorBehavior>(MetricAggregatorBehaviors.DeltaTemporality);

    private static readonly bool EmitOverflowAttribute = MetricMeasurementHandlerHelper.IsAggregatorBehaviorDefined<TAggregatorBehavior>(MetricAggregatorBehaviors.EmitOverflowAttribute);
    private static readonly bool FilterTags = MetricMeasurementHandlerHelper.IsAggregatorBehaviorDefined<TAggregatorBehavior>(MetricAggregatorBehaviors.FilterTags);
    private static readonly bool SampleMeasurementAndOfferExemplar = MetricMeasurementHandlerHelper.IsAggregatorBehaviorDefined<TAggregatorBehavior>(MetricAggregatorBehaviors.SampleMeasurementAndOfferExemplar);
    private static readonly bool ReclaimMetricPoints = MetricMeasurementHandlerHelper.IsAggregatorBehaviorDefined<TAggregatorBehavior>(MetricAggregatorBehaviors.ReclaimMetricPoints);

    private static readonly bool IsCumulativeAggregation = MetricMeasurementHandlerHelper.IsMetricPointBehaviorDefined<TMetricPointBehavior>(MetricPointBehaviors.CumulativeAggregation);
    private static readonly bool IsDeltaAggregation = MetricMeasurementHandlerHelper.IsMetricPointBehaviorDefined<TMetricPointBehavior>(MetricPointBehaviors.DeltaAggregation);

    private static readonly bool IsCounter = MetricMeasurementHandlerHelper.IsMetricPointBehaviorDefined<TMetricPointBehavior>(MetricPointBehaviors.Counter);
    private static readonly bool IsGauge = MetricMeasurementHandlerHelper.IsMetricPointBehaviorDefined<TMetricPointBehavior>(MetricPointBehaviors.Gauge);

    private static readonly bool IsHistogramAggregation = MetricMeasurementHandlerHelper.IsMetricPointBehaviorDefined<TMetricPointBehavior>(MetricPointBehaviors.HistogramAggregation);
    private static readonly bool HistogramRecordMinMax = MetricMeasurementHandlerHelper.IsMetricPointBehaviorDefined<TMetricPointBehavior>(MetricPointBehaviors.HistogramRecordMinMax);
    private static readonly bool IsHistogramWithoutBuckets = MetricMeasurementHandlerHelper.IsMetricPointBehaviorDefined<TMetricPointBehavior>(MetricPointBehaviors.HistogramWithoutBuckets);
    private static readonly bool IsHistogramWithExponentialBuckets = MetricMeasurementHandlerHelper.IsMetricPointBehaviorDefined<TMetricPointBehavior>(MetricPointBehaviors.HistogramWithExponentialBuckets);

    private static readonly bool LockRequired = IsHistogramAggregation || SampleMeasurementAndOfferExemplar;

    private static readonly IMetricMeasurementHandler? OverflowMetricMeasurementHandler;

    static MetricMeasurementHandler()
    {
        if (EmitOverflowAttribute)
        {
            if (IsCumulativeTemporality)
            {
                OverflowMetricMeasurementHandler = new MetricMeasurementHandler<MetricAggregatorBehaviorDefinitions.CumulativeTemporality, TMetricPointBehavior>();
            }
            else
            {
                Debug.Assert(IsDeltaTemporality, "IsDeltaTemporality was false");

                OverflowMetricMeasurementHandler = new MetricMeasurementHandler<MetricAggregatorBehaviorDefinitions.DeltaTemporality, TMetricPointBehavior>();
            }
        }
    }

    public void RecordMeasurement<T>(
        AggregatorStore aggregatorStore,
        T value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        try
        {
            var index = FilterTags
                ? aggregatorStore.FindMetricPointIndexCustomTag(tags)
                : aggregatorStore.FindMetricPointIndexDefault(tags);

            if (index < 0)
            {
                if (EmitOverflowAttribute)
                {
                    Debug.Assert(OverflowMetricMeasurementHandler != null, "OverflowMetricMeasurementHandler was null");

                    ref var overflowMetricPoint = ref aggregatorStore.DropMeasurementWithOverflowRecording();

                    OverflowMetricMeasurementHandler!.RecordMeasurementOnMetricPoint(
                        aggregatorStore,
                        ref overflowMetricPoint,
                        value,
                        tags);

                    CompleteRecordMeasurement(ref overflowMetricPoint);
                }
                else
                {
                    aggregatorStore.DropMeasurementDefault();
                }

                return;
            }

            ref MetricPoint metricPoint = ref aggregatorStore.GetMetricPoint(index);

            this.RecordMeasurementOnMetricPoint(
                aggregatorStore,
                ref metricPoint,
                value,
                tags);

            CompleteRecordMeasurement(ref metricPoint);
        }
        catch (Exception)
        {
            aggregatorStore.DropMeasurementUnhandledException();
        }
    }

    public void RecordMeasurementOnMetricPoint<T>(
        AggregatorStore aggregatorStore,
        ref MetricPoint metricPoint,
        T value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        bool measurementSampled;
        if (SampleMeasurementAndOfferExemplar
            /* TODO: Enable exemplars for exponential histograms */
            && !IsHistogramWithExponentialBuckets)
        {
            measurementSampled = SampleMeasurement(aggregatorStore, value, tags);
        }
        else
        {
            measurementSampled = false;
        }

        int histogramBucketIndex = 0;

        if (LockRequired)
        {
            metricPoint.OptionalComponents!.AcquireLock();
        }

        if (typeof(T) == typeof(long))
        {
            if (IsCounter || IsGauge)
            {
                UpdateLongCounterOrGaugeMetricPoint(ref metricPoint, (long)(object)value!);
            }
            else if (IsHistogramAggregation)
            {
                double doubleValue = (long)(object)value!;

                if (IsHistogramWithExponentialBuckets)
                {
                    UpdateExponentialHistogramMetricPoint(ref metricPoint, doubleValue);
                }
                else
                {
                    UpdateHistogramMetricPoint(ref metricPoint, doubleValue, ref histogramBucketIndex);
                }
            }
            else
            {
                ThrowMeasurementTypeNotSupportedException<T>();
            }
        }
        else if (typeof(T) == typeof(double))
        {
            if (IsCounter || IsGauge)
            {
                UpdateDoubleCounterOrGaugeMetricPoint(ref metricPoint, (double)(object)value!);
            }
            else if (IsHistogramAggregation)
            {
                if (IsHistogramWithExponentialBuckets)
                {
                    UpdateExponentialHistogramMetricPoint(ref metricPoint, (double)(object)value!);
                }
                else
                {
                    UpdateHistogramMetricPoint(ref metricPoint, (double)(object)value!, ref histogramBucketIndex);
                }
            }
            else
            {
                ThrowMeasurementTypeNotSupportedException<T>();
            }
        }
        else
        {
            ThrowMeasurementTypeNotSupportedException<T>();
        }

        if (SampleMeasurementAndOfferExemplar && measurementSampled)
        {
            OfferExemplar(ref metricPoint, value, tags: FilterTags ? tags : default, histogramBucketIndex);
        }

        if (LockRequired)
        {
            metricPoint.OptionalComponents!.ReleaseLock();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CompleteRecordMeasurement(ref MetricPoint metricPoint)
    {
        metricPoint.MetricPointStatus = MetricPointStatus.CollectPending;

        if (IsDeltaTemporality && ReclaimMetricPoints)
        {
            Interlocked.Decrement(ref metricPoint.ReferenceCount);
        }
    }

    private static bool SampleMeasurement<T>(
        AggregatorStore aggregatorStore,
        T value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (typeof(T) == typeof(long))
        {
            return aggregatorStore.ExemplarFilter.ShouldSample(
                (long)(object)value!,
                tags);
        }
        else if (typeof(T) == typeof(double))
        {
            return aggregatorStore.ExemplarFilter.ShouldSample(
                (double)(object)value!,
                tags);
        }
        else
        {
            ThrowMeasurementTypeNotSupportedException<T>();
            return false;
        }
    }

    private static void UpdateLongCounterOrGaugeMetricPoint(ref MetricPoint metricPoint, long value)
    {
        if (LockRequired)
        {
            if (IsDeltaAggregation)
            {
                unchecked
                {
                    metricPoint.RunningValue.AsLong += value;
                }
            }
            else
            {
                Debug.Assert(IsCumulativeAggregation, "IsCumulativeAggregation was false");

                metricPoint.RunningValue.AsLong = value;
            }
        }
        else if (IsDeltaAggregation)
        {
            Interlocked.Add(ref metricPoint.RunningValue.AsLong, value);
        }
        else
        {
            Debug.Assert(IsCumulativeAggregation, "IsCumulativeAggregation was false");

            Interlocked.Exchange(ref metricPoint.RunningValue.AsLong, value);
        }
    }

    private static void UpdateDoubleCounterOrGaugeMetricPoint(ref MetricPoint metricPoint, double value)
    {
        if (LockRequired)
        {
            if (IsDeltaAggregation)
            {
                unchecked
                {
                    metricPoint.RunningValue.AsDouble += value;
                }
            }
            else
            {
                Debug.Assert(IsCumulativeAggregation, "IsCumulativeAggregation was false");

                metricPoint.RunningValue.AsDouble = value;
            }
        }
        else if (IsDeltaAggregation)
        {
            double initValue, newValue;
            var sw = default(SpinWait);
            while (true)
            {
                initValue = metricPoint.RunningValue.AsDouble;

                unchecked
                {
                    newValue = initValue + value;
                }

                if (initValue == Interlocked.CompareExchange(ref metricPoint.RunningValue.AsDouble, newValue, initValue))
                {
                    break;
                }

                sw.SpinOnce();
            }
        }
        else
        {
            Debug.Assert(IsCumulativeAggregation, "IsCumulativeAggregation was false");

            Interlocked.Exchange(ref metricPoint.RunningValue.AsDouble, value);
        }
    }

    private static void UpdateHistogramMetricPoint(ref MetricPoint metricPoint, double value, ref int bucketIndex)
    {
        Debug.Assert(metricPoint.OptionalComponents!.HistogramBuckets != null, "HistogramBuckets was null");

        var histogramBuckets = metricPoint.OptionalComponents!.HistogramBuckets!;

        if (!IsHistogramWithoutBuckets)
        {
            bucketIndex = histogramBuckets!.FindBucketIndex(value);
        }

        unchecked
        {
            metricPoint.RunningValue.AsLong++;
            histogramBuckets.RunningSum += value;
            if (!IsHistogramWithoutBuckets)
            {
                Debug.Assert(histogramBuckets.RunningBucketCounts != null, "histogramBuckets.RunningBucketCounts was null");

                histogramBuckets.RunningBucketCounts![bucketIndex]++;
            }
        }

        if (HistogramRecordMinMax)
        {
            histogramBuckets.RunningMin = Math.Min(histogramBuckets.RunningMin, value);
            histogramBuckets.RunningMax = Math.Max(histogramBuckets.RunningMax, value);
        }
    }

    private static void UpdateExponentialHistogramMetricPoint(ref MetricPoint metricPoint, double value)
    {
        if (value >= 0)
        {
            Debug.Assert(metricPoint.OptionalComponents!.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

            var histogram = metricPoint.OptionalComponents.Base2ExponentialBucketHistogram!;

            unchecked
            {
                metricPoint.RunningValue.AsLong++;
                histogram.RunningSum += value;
            }

            histogram.Record(value);

            if (HistogramRecordMinMax)
            {
                histogram.RunningMin = Math.Min(histogram.RunningMin, value);
                histogram.RunningMax = Math.Max(histogram.RunningMax, value);
            }
        }
    }

    private static void OfferExemplar<T>(
        ref MetricPoint metricPoint,
        T value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        int bucketIndex)
    {
        if (typeof(T) == typeof(long))
        {
            metricPoint.OptionalComponents!.ExemplarReservoir!.Offer((long)(object)value!, tags, bucketIndex);
        }
        else if (typeof(T) == typeof(double))
        {
            metricPoint.OptionalComponents!.ExemplarReservoir!.Offer((double)(object)value!, tags, bucketIndex);
        }
        else
        {
            ThrowMeasurementTypeNotSupportedException<T>();
        }
    }

    [DoesNotReturn]
    private static void ThrowMeasurementTypeNotSupportedException<T>()
    {
        throw new NotSupportedException($"Measurements of type '{typeof(T)}' are not supported with '{typeof(TAggregatorBehavior)}' and '{typeof(TMetricPointBehavior)}' behaviors.");
    }
}
