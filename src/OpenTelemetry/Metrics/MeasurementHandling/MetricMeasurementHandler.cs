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

    private static readonly bool IsLong = MetricMeasurementHandlerHelper.IsMetricPointBehaviorDefined<TMetricPointBehavior>(MetricPointBehaviors.Long);
    private static readonly bool IsDouble = MetricMeasurementHandlerHelper.IsMetricPointBehaviorDefined<TMetricPointBehavior>(MetricPointBehaviors.Double);

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
            Debug.Assert(metricPoint.OptionalComponents != null, "metricPoint.OptionalComponents was null");

            metricPoint.OptionalComponents!.AcquireLock();
        }

        if (typeof(T) == typeof(long))
        {
            if (IsCounter || IsGauge)
            {
                RecordMeasurementOnLongCounterOrGaugeMetricPoint(ref metricPoint, (long)(object)value!);
            }
            else if (IsHistogramAggregation)
            {
                double doubleValue = (long)(object)value!;

                if (IsHistogramWithExponentialBuckets)
                {
                    RecordMeasurementOnExponentialHistogramMetricPoint(ref metricPoint, doubleValue);
                }
                else
                {
                    RecordMeasurementOnHistogramMetricPoint(ref metricPoint, doubleValue, ref histogramBucketIndex);
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
                RecordMeasurementOnDoubleCounterOrGaugeMetricPoint(ref metricPoint, (double)(object)value!);
            }
            else if (IsHistogramAggregation)
            {
                if (IsHistogramWithExponentialBuckets)
                {
                    RecordMeasurementOnExponentialHistogramMetricPoint(ref metricPoint, (double)(object)value!);
                }
                else
                {
                    RecordMeasurementOnHistogramMetricPoint(ref metricPoint, (double)(object)value!, ref histogramBucketIndex);
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

    public int CollectMeasurements(AggregatorStore aggregatorStore)
    {
        aggregatorStore.BatchSize = 0;

        if (IsDeltaTemporality && ReclaimMetricPoints)
        {
            // Index = 0 is reserved for the case where no dimensions are provided.
            ref var metricPointWithNoTags = ref aggregatorStore.GetMetricPoint(0);
            if (metricPointWithNoTags.MetricPointStatus != MetricPointStatus.NoCollectPending)
            {
                this.CollectMeasurementsOnMetricPoint(ref metricPointWithNoTags);

                aggregatorStore.CurrentMetricPointBatch[aggregatorStore.BatchSize] = 0;
                aggregatorStore.BatchSize++;
            }

            int startIndexForReclaimableMetricPoints = 1;

            if (EmitOverflowAttribute)
            {
                startIndexForReclaimableMetricPoints = 2; // Index 0 and 1 are reserved for no tags and overflow

                // TakeSnapshot for the MetricPoint for overflow
                ref var metricPointForOverflow = ref aggregatorStore.GetMetricPoint(1);
                if (metricPointForOverflow.MetricPointStatus != MetricPointStatus.NoCollectPending)
                {
                    this.CollectMeasurementsOnMetricPoint(ref metricPointForOverflow);

                    aggregatorStore.CurrentMetricPointBatch[aggregatorStore.BatchSize] = 1;
                    aggregatorStore.BatchSize++;
                }
            }

            for (int i = startIndexForReclaimableMetricPoints; i < aggregatorStore.MaxMetricPoints; i++)
            {
                ref var metricPoint = ref aggregatorStore.GetMetricPoint(i);

                if (metricPoint.MetricPointStatus == MetricPointStatus.NoCollectPending)
                {
                    aggregatorStore.TryReclaimMetricPoint(ref metricPoint, i);
                    continue;
                }

                this.CollectMeasurementsOnMetricPoint(ref metricPoint);

                aggregatorStore.CurrentMetricPointBatch[aggregatorStore.BatchSize] = i;
                aggregatorStore.BatchSize++;
            }
        }
        else
        {
            var indexSnapshot = Math.Min(aggregatorStore.MetricPointIndex, aggregatorStore.MaxMetricPoints - 1);

            for (int i = 0; i <= indexSnapshot; i++)
            {
                ref var metricPoint = ref aggregatorStore.GetMetricPoint(i);
                if (IsDeltaTemporality)
                {
                    if (metricPoint.MetricPointStatus == MetricPointStatus.NoCollectPending)
                    {
                        continue;
                    }
                }
                else
                {
                    Debug.Assert(IsCumulativeTemporality, "IsCumulativeTemporality was false.");

                    if (!metricPoint.IsInitialized)
                    {
                        continue;
                    }
                }

                this.CollectMeasurementsOnMetricPoint(ref metricPoint);

                aggregatorStore.CurrentMetricPointBatch[aggregatorStore.BatchSize] = i;
                aggregatorStore.BatchSize++;
            }
        }

        if (IsDeltaTemporality)
        {
            if (aggregatorStore.EndTimeInclusive != default)
            {
                aggregatorStore.StartTimeExclusive = aggregatorStore.EndTimeInclusive;
            }
        }

        aggregatorStore.EndTimeInclusive = DateTimeOffset.UtcNow;

        return aggregatorStore.BatchSize;
    }

    public void CollectMeasurementsOnMetricPoint(ref MetricPoint metricPoint)
    {
        if (LockRequired)
        {
            Debug.Assert(metricPoint.OptionalComponents != null, "metricPoint.OptionalComponents was null");

            metricPoint.OptionalComponents!.AcquireLock();
        }

        if (IsCounter)
        {
            CollectMeasurementsOnCounterMetricPoint(ref metricPoint);
        }
        else if (IsGauge)
        {
            CollectMeasurementsOnGaugeMetricPoint(ref metricPoint);
        }
        else if (IsHistogramAggregation)
        {
            if (IsHistogramWithExponentialBuckets)
            {
                CollectMeasurementsOnExponentialHistogramMetricPoint(ref metricPoint);
            }
            else
            {
                CollectMeasurementsOnHistogramMetricPoint(ref metricPoint);
            }

            metricPoint.MetricPointStatus = MetricPointStatus.NoCollectPending;
        }
        else
        {
            ThrowCollectionNotSupportedException();
        }

        if (SampleMeasurementAndOfferExemplar
            /* TODO: Enable exemplars for exponential histograms */
            && !IsHistogramWithExponentialBuckets)
        {
            metricPoint.OptionalComponents!.Exemplars
                = metricPoint.OptionalComponents.ExemplarReservoir?.Collect(metricPoint.Tags, reset: IsDeltaTemporality);
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

    private static void RecordMeasurementOnLongCounterOrGaugeMetricPoint(ref MetricPoint metricPoint, long value)
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

    private static void RecordMeasurementOnDoubleCounterOrGaugeMetricPoint(ref MetricPoint metricPoint, double value)
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

    private static void RecordMeasurementOnHistogramMetricPoint(ref MetricPoint metricPoint, double value, ref int bucketIndex)
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

    private static void RecordMeasurementOnExponentialHistogramMetricPoint(ref MetricPoint metricPoint, double value)
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

    private static void CollectMeasurementsOnCounterMetricPoint(ref MetricPoint metricPoint)
    {
        if (IsLong)
        {
            if (IsDeltaTemporality)
            {
                long initValue;
                if (!LockRequired)
                {
                    initValue = Interlocked.Read(ref metricPoint.RunningValue.AsLong);
                }
                else
                {
                    initValue = metricPoint.RunningValue.AsLong;
                }

                metricPoint.SnapshotValue.AsLong = initValue - metricPoint.DeltaLastValue.AsLong;
                metricPoint.DeltaLastValue.AsLong = initValue;
                metricPoint.MetricPointStatus = MetricPointStatus.NoCollectPending;

                if (!LockRequired)
                {
                    // Check again if value got updated, if yes reset status.
                    // This ensures no Updates get Lost.
                    if (initValue != Interlocked.Read(ref metricPoint.RunningValue.AsLong))
                    {
                        metricPoint.MetricPointStatus = MetricPointStatus.CollectPending;
                    }
                }
            }
            else
            {
                Debug.Assert(IsCumulativeTemporality, "IsCumulativeTemporality was null");

                if (!LockRequired)
                {
                    metricPoint.SnapshotValue.AsLong = Interlocked.Read(ref metricPoint.RunningValue.AsLong);
                }
                else
                {
                    metricPoint.SnapshotValue.AsLong = metricPoint.RunningValue.AsLong;
                }
            }
        }
        else if (IsDouble)
        {
            if (IsDeltaTemporality)
            {
                double initValue;
                if (!LockRequired)
                {
                    // TODO:
                    // Is this thread-safe way to read double?
                    // As long as the value is not -ve infinity,
                    // the exchange (to 0.0) will never occur,
                    // but we get the original value atomically.
                    initValue = Interlocked.CompareExchange(ref metricPoint.RunningValue.AsDouble, 0.0, double.NegativeInfinity);
                }
                else
                {
                    initValue = metricPoint.RunningValue.AsDouble;
                }

                metricPoint.SnapshotValue.AsDouble = initValue - metricPoint.DeltaLastValue.AsDouble;
                metricPoint.DeltaLastValue.AsDouble = initValue;
                metricPoint.MetricPointStatus = MetricPointStatus.NoCollectPending;

                if (!LockRequired)
                {
                    // Check again if value got updated, if yes reset status.
                    // This ensures no Updates get Lost.
                    if (initValue != Interlocked.CompareExchange(ref metricPoint.RunningValue.AsDouble, 0.0, double.NegativeInfinity))
                    {
                        metricPoint.MetricPointStatus = MetricPointStatus.CollectPending;
                    }
                }
            }
            else
            {
                Debug.Assert(IsCumulativeTemporality, "IsCumulativeTemporality was null");

                if (!LockRequired)
                {
                    // TODO:
                    // Is this thread-safe way to read double?
                    // As long as the value is not -ve infinity,
                    // the exchange (to 0.0) will never occur,
                    // but we get the original value atomically.
                    metricPoint.SnapshotValue.AsDouble = Interlocked.CompareExchange(ref metricPoint.RunningValue.AsDouble, 0.0, double.NegativeInfinity);
                }
                else
                {
                    metricPoint.SnapshotValue.AsDouble = metricPoint.RunningValue.AsDouble;
                }
            }
        }
        else
        {
            ThrowCollectionNotSupportedException();
        }
    }

    private static void CollectMeasurementsOnGaugeMetricPoint(ref MetricPoint metricPoint)
    {
        if (IsLong)
        {
            if (!LockRequired)
            {
                metricPoint.SnapshotValue.AsLong = Interlocked.Read(ref metricPoint.RunningValue.AsLong);
            }
            else
            {
                metricPoint.SnapshotValue.AsLong = metricPoint.RunningValue.AsLong;
            }

            metricPoint.MetricPointStatus = MetricPointStatus.NoCollectPending;

            if (!LockRequired)
            {
                // Check again if value got updated, if yes reset status.
                // This ensures no Updates get Lost.
                if (metricPoint.SnapshotValue.AsLong != Interlocked.Read(ref metricPoint.RunningValue.AsLong))
                {
                    metricPoint.MetricPointStatus = MetricPointStatus.CollectPending;
                }
            }
        }
        else if (IsDouble)
        {
            if (!LockRequired)
            {
                // TODO:
                // Is this thread-safe way to read double?
                // As long as the value is not -ve infinity,
                // the exchange (to 0.0) will never occur,
                // but we get the original value atomically.
                metricPoint.SnapshotValue.AsDouble = Interlocked.CompareExchange(ref metricPoint.RunningValue.AsDouble, 0.0, double.NegativeInfinity);
            }
            else
            {
                metricPoint.SnapshotValue.AsDouble = metricPoint.RunningValue.AsDouble;
            }

            metricPoint.MetricPointStatus = MetricPointStatus.NoCollectPending;

            if (!LockRequired)
            {
                // Check again if value got updated, if yes reset status.
                // This ensures no Updates get Lost.
                if (metricPoint.SnapshotValue.AsDouble != Interlocked.CompareExchange(ref metricPoint.RunningValue.AsDouble, 0.0, double.NegativeInfinity))
                {
                    metricPoint.MetricPointStatus = MetricPointStatus.CollectPending;
                }
            }
        }
        else
        {
            ThrowCollectionNotSupportedException();
        }
    }

    private static void CollectMeasurementsOnHistogramMetricPoint(ref MetricPoint metricPoint)
    {
        Debug.Assert(metricPoint.OptionalComponents!.HistogramBuckets != null, "HistogramBuckets was null");

        var histogramBuckets = metricPoint.OptionalComponents!.HistogramBuckets!;

        metricPoint.SnapshotValue.AsLong = metricPoint.RunningValue.AsLong;
        histogramBuckets.SnapshotSum = histogramBuckets.RunningSum;

        if (IsDeltaTemporality)
        {
            metricPoint.RunningValue.AsLong = 0;
            histogramBuckets.RunningSum = 0;
        }

        if (HistogramRecordMinMax)
        {
            histogramBuckets.SnapshotMin = histogramBuckets.RunningMin;
            histogramBuckets.SnapshotMax = histogramBuckets.RunningMax;

            if (IsDeltaTemporality)
            {
                histogramBuckets.RunningMin = double.PositiveInfinity;
                histogramBuckets.RunningMax = double.NegativeInfinity;
            }
        }

        if (!IsHistogramWithoutBuckets)
        {
            Debug.Assert(histogramBuckets.RunningBucketCounts != null, "histogramBuckets.RunningBucketCounts was null");

            for (int i = 0; i < histogramBuckets.RunningBucketCounts!.Length; i++)
            {
                ref long count = ref histogramBuckets.RunningBucketCounts[i];

                histogramBuckets.SnapshotBucketCounts[i] = count;

                if (IsDeltaTemporality)
                {
                    count = 0;
                }
            }
        }
    }

    private static void CollectMeasurementsOnExponentialHistogramMetricPoint(ref MetricPoint metricPoint)
    {
        Debug.Assert(metricPoint.OptionalComponents!.Base2ExponentialBucketHistogram != null, "Base2ExponentialBucketHistogram was null");

        var histogram = metricPoint.OptionalComponents!.Base2ExponentialBucketHistogram!;

        metricPoint.SnapshotValue.AsLong = metricPoint.RunningValue.AsLong;

        histogram.SnapshotSum = histogram.RunningSum;
        histogram.Snapshot();

        if (IsDeltaTemporality)
        {
            metricPoint.RunningValue.AsLong = 0;
            histogram.RunningSum = 0;
            histogram.Reset();
        }

        if (HistogramRecordMinMax)
        {
            histogram.SnapshotMin = histogram.RunningMin;
            histogram.SnapshotMax = histogram.RunningMax;

            if (IsDeltaTemporality)
            {
                histogram.RunningMin = double.PositiveInfinity;
                histogram.RunningMax = double.NegativeInfinity;
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

    [DoesNotReturn]
    private static void ThrowCollectionNotSupportedException()
    {
        throw new NotSupportedException($"Collection is not supported with '{typeof(TAggregatorBehavior)}' and '{typeof(TMetricPointBehavior)}' behaviors.");
    }
}
