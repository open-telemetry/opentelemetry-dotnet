// <copyright file="IMetricMeasurementHandler.cs" company="OpenTelemetry Authors">
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
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Metrics;

#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1201 // Elements should appear in the correct order

[Flags]
internal enum MetricAggregatorBehaviors
{
#pragma warning disable SA1602 // Enumeration items should be documented
    None = 0,

    CumulativeTemporality = 1,
    DeltaTemporality = 1 << 1,
    EmitOverflowAttribute = 1 << 2,
    FilterTags = 1 << 3,
    SampleMeasurementAndOfferExemplar = 1 << 4,
    ReclaimMetricPoints = 1 << 5,
#pragma warning restore SA1602 // Enumeration items should be documented
}

internal static class MetricAggregatorBehaviorDefinitions
{
    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.CumulativeTemporality)]
    public struct CumulativeTemporality
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.CumulativeTemporality | MetricAggregatorBehaviors.EmitOverflowAttribute)]
    public struct CumulativeTemporalityWithEmitOverflow
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.CumulativeTemporality | MetricAggregatorBehaviors.EmitOverflowAttribute | MetricAggregatorBehaviors.FilterTags)]
    public struct CumulativeTemporalityWithEmitOverflowAndTagFiltering
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.CumulativeTemporality | MetricAggregatorBehaviors.EmitOverflowAttribute | MetricAggregatorBehaviors.FilterTags | MetricAggregatorBehaviors.SampleMeasurementAndOfferExemplar)]
    public struct CumulativeTemporalityWithEmitOverflowAndTagFilteringAndMeasurementSampling
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.CumulativeTemporality | MetricAggregatorBehaviors.EmitOverflowAttribute | MetricAggregatorBehaviors.SampleMeasurementAndOfferExemplar)]
    public struct CumulativeTemporalityWithEmitOverflowAndMeasurementSampling
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.CumulativeTemporality | MetricAggregatorBehaviors.FilterTags)]
    public struct CumulativeTemporalityWithTagFiltering
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.CumulativeTemporality | MetricAggregatorBehaviors.FilterTags | MetricAggregatorBehaviors.SampleMeasurementAndOfferExemplar)]
    public struct CumulativeTemporalityWithTagFilteringAndMeasurementSampling
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.CumulativeTemporality | MetricAggregatorBehaviors.SampleMeasurementAndOfferExemplar)]
    public struct CumulativeTemporalityWithMeasurementSampling
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.DeltaTemporality)]
    public struct DeltaTemporality
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.DeltaTemporality | MetricAggregatorBehaviors.EmitOverflowAttribute)]
    public struct DeltaTemporalityWithEmitOverflow
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.DeltaTemporality | MetricAggregatorBehaviors.EmitOverflowAttribute | MetricAggregatorBehaviors.FilterTags)]
    public struct DeltaTemporalityWithEmitOverflowAndTagFiltering
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.DeltaTemporality | MetricAggregatorBehaviors.EmitOverflowAttribute | MetricAggregatorBehaviors.FilterTags | MetricAggregatorBehaviors.SampleMeasurementAndOfferExemplar)]
    public struct DeltaTemporalityWithEmitOverflowAndTagFilteringAndMeasurementSampling
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.DeltaTemporality | MetricAggregatorBehaviors.EmitOverflowAttribute | MetricAggregatorBehaviors.FilterTags | MetricAggregatorBehaviors.SampleMeasurementAndOfferExemplar | MetricAggregatorBehaviors.ReclaimMetricPoints)]
    public struct DeltaTemporalityWithEmitOverflowAndTagFilteringAndMeasurementSamplingAndMetricPointReclaim
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.DeltaTemporality | MetricAggregatorBehaviors.EmitOverflowAttribute | MetricAggregatorBehaviors.SampleMeasurementAndOfferExemplar)]
    public struct DeltaTemporalityWithEmitOverflowAndMeasurementSampling
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.DeltaTemporality | MetricAggregatorBehaviors.EmitOverflowAttribute | MetricAggregatorBehaviors.SampleMeasurementAndOfferExemplar | MetricAggregatorBehaviors.ReclaimMetricPoints)]
    public struct DeltaTemporalityWithEmitOverflowAndMeasurementSamplingAndMetricPointReclaim
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.DeltaTemporality | MetricAggregatorBehaviors.FilterTags)]
    public struct DeltaTemporalityWithTagFiltering
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.DeltaTemporality | MetricAggregatorBehaviors.FilterTags | MetricAggregatorBehaviors.SampleMeasurementAndOfferExemplar)]
    public struct DeltaTemporalityWithTagFilteringAndMeasurementSampling
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.DeltaTemporality | MetricAggregatorBehaviors.FilterTags | MetricAggregatorBehaviors.SampleMeasurementAndOfferExemplar | MetricAggregatorBehaviors.ReclaimMetricPoints)]
    public struct DeltaTemporalityWithTagFilteringAndMeasurementSamplingAndMetricPointReclaim
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.DeltaTemporality | MetricAggregatorBehaviors.SampleMeasurementAndOfferExemplar)]
    public struct DeltaTemporalityWithMeasurementSampling
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.DeltaTemporality | MetricAggregatorBehaviors.SampleMeasurementAndOfferExemplar | MetricAggregatorBehaviors.ReclaimMetricPoints)]
    public struct DeltaTemporalityWithMeasurementSamplingAndMetricPointReclaim
    {
    }

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.DeltaTemporality | MetricAggregatorBehaviors.ReclaimMetricPoints)]
    public struct DeltaTemporalityWithMetricPointReclaim
    {
    }
}

internal static class MetricMeasurementHandlerHelper
{
    private static readonly Dictionary<(MetricAggregatorBehaviors, MetricPointBehaviors), IMetricMeasurementHandler> Definitions = BuildDefinitions();

    public static bool TryFindMeasurementHandlerForBehaviors(
        MetricAggregatorBehaviors metricAggregatorBehaviors,
        MetricPointBehaviors metricPointBehaviors,
        [NotNullWhen(true)] out IMetricMeasurementHandler? metricMeasurementHandler)
        => Definitions.TryGetValue((metricAggregatorBehaviors, metricPointBehaviors), out metricMeasurementHandler);

    public static MetricAggregatorBehaviors GetMetricAggregatorBehaviors<TAggregatorBehavior>()
    {
        var metricAggregatorBehaviorsAttribute = typeof(TAggregatorBehavior).GetCustomAttribute<MetricAggregatorBehaviorsAttribute>()
            ?? throw new InvalidOperationException($"Type '{typeof(TAggregatorBehavior)}' is not decorated with {nameof(MetricAggregatorBehaviorsAttribute)}.");

        return metricAggregatorBehaviorsAttribute.MetricAggregatorBehaviors;
    }

    public static bool IsAggregatorBehaviorDefined<TAggregatorBehavior>(MetricAggregatorBehaviors metricAggregatorBehaviors)
    {
        return GetMetricAggregatorBehaviors<TAggregatorBehavior>().HasFlag(metricAggregatorBehaviors);
    }

    public static MetricPointBehaviors GetMetricPointBehaviors<TMetricPointBehavior>()
    {
        var metricPointBehaviorsAttribute = typeof(TMetricPointBehavior).GetCustomAttribute<MetricPointBehaviorsAttribute>()
            ?? throw new InvalidOperationException($"Type '{typeof(TMetricPointBehavior)}' is not decorated with {nameof(MetricPointBehaviorsAttribute)}.");

        return metricPointBehaviorsAttribute.MetricPointBehaviors;
    }

    public static bool IsMetricPointBehaviorDefined<TMetricPointBehavior>(MetricPointBehaviors metricPointBehaviors)
    {
        return GetMetricPointBehaviors<TMetricPointBehavior>().HasFlag(metricPointBehaviors);
    }

    private static Dictionary<(MetricAggregatorBehaviors MetricAggregatorBehaviors, MetricPointBehaviors MetricPointBehaviors), IMetricMeasurementHandler> BuildDefinitions()
    {
        Dictionary<(MetricAggregatorBehaviors, MetricPointBehaviors), IMetricMeasurementHandler> definitions = new();

        AddPermutations<MetricPointBehaviorDefinitions.CumulativeSumLong>();
        AddPermutations<MetricPointBehaviorDefinitions.CumulativeSumDouble>();

        AddPermutations<MetricPointBehaviorDefinitions.DeltaSumLong>();
        AddPermutations<MetricPointBehaviorDefinitions.DeltaSumDouble>();

        AddPermutations<MetricPointBehaviorDefinitions.GaugeLong>();
        AddPermutations<MetricPointBehaviorDefinitions.GaugeDouble>();

        AddPermutations<MetricPointBehaviorDefinitions.Histogram>();
        AddPermutations<MetricPointBehaviorDefinitions.HistogramWithMinMax>();
        AddPermutations<MetricPointBehaviorDefinitions.HistogramWithoutBuckets>();
        AddPermutations<MetricPointBehaviorDefinitions.HistogramWithoutBucketsAndWithMinMax>();
        AddPermutations<MetricPointBehaviorDefinitions.HistogramWithExponentialBuckets>();
        AddPermutations<MetricPointBehaviorDefinitions.HistogramWithExponentialBucketsAndMinMax>();

        return definitions;

        void AddPermutations<TMetricPointBehavior>()
            where TMetricPointBehavior : struct
        {
            /* CumulativeTemporality */
            AddPermutation<MetricAggregatorBehaviorDefinitions.CumulativeTemporality>();

            AddPermutation<MetricAggregatorBehaviorDefinitions.CumulativeTemporalityWithEmitOverflow>();
            AddPermutation<MetricAggregatorBehaviorDefinitions.CumulativeTemporalityWithEmitOverflowAndTagFiltering>();
            AddPermutation<MetricAggregatorBehaviorDefinitions.CumulativeTemporalityWithEmitOverflowAndMeasurementSampling>();
            AddPermutation<MetricAggregatorBehaviorDefinitions.CumulativeTemporalityWithEmitOverflowAndTagFilteringAndMeasurementSampling>();

            AddPermutation<MetricAggregatorBehaviorDefinitions.CumulativeTemporalityWithTagFiltering>();
            AddPermutation<MetricAggregatorBehaviorDefinitions.CumulativeTemporalityWithTagFilteringAndMeasurementSampling>();

            AddPermutation<MetricAggregatorBehaviorDefinitions.CumulativeTemporalityWithMeasurementSampling>();

            /* DeltaTemporality */
            AddPermutation<MetricAggregatorBehaviorDefinitions.DeltaTemporality>();

            AddPermutation<MetricAggregatorBehaviorDefinitions.DeltaTemporalityWithEmitOverflow>();
            AddPermutation<MetricAggregatorBehaviorDefinitions.DeltaTemporalityWithEmitOverflowAndTagFiltering>();
            AddPermutation<MetricAggregatorBehaviorDefinitions.DeltaTemporalityWithEmitOverflowAndMeasurementSampling>();
            AddPermutation<MetricAggregatorBehaviorDefinitions.DeltaTemporalityWithEmitOverflowAndTagFilteringAndMeasurementSampling>();
            AddPermutation<MetricAggregatorBehaviorDefinitions.DeltaTemporalityWithEmitOverflowAndMeasurementSamplingAndMetricPointReclaim>();
            AddPermutation<MetricAggregatorBehaviorDefinitions.DeltaTemporalityWithEmitOverflowAndTagFilteringAndMeasurementSamplingAndMetricPointReclaim>();

            AddPermutation<MetricAggregatorBehaviorDefinitions.DeltaTemporalityWithTagFiltering>();
            AddPermutation<MetricAggregatorBehaviorDefinitions.DeltaTemporalityWithTagFilteringAndMeasurementSampling>();
            AddPermutation<MetricAggregatorBehaviorDefinitions.DeltaTemporalityWithTagFilteringAndMeasurementSamplingAndMetricPointReclaim>();

            AddPermutation<MetricAggregatorBehaviorDefinitions.DeltaTemporalityWithMeasurementSampling>();
            AddPermutation<MetricAggregatorBehaviorDefinitions.DeltaTemporalityWithMeasurementSamplingAndMetricPointReclaim>();

            AddPermutation<MetricAggregatorBehaviorDefinitions.DeltaTemporalityWithMetricPointReclaim>();

            void AddPermutation<TAggregatorBehavior>()
                where TAggregatorBehavior : struct
            {
                var metricAggregatorBehaviors = GetMetricAggregatorBehaviors<TAggregatorBehavior>();

                var metricPointBehaviors = GetMetricPointBehaviors<TMetricPointBehavior>();

                definitions.Add((metricAggregatorBehaviors, metricPointBehaviors), new MetricMeasurementHandler<TAggregatorBehavior, TMetricPointBehavior>());
            }
        }
    }
}

[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
internal sealed class MetricAggregatorBehaviorsAttribute : Attribute
{
    public MetricAggregatorBehaviorsAttribute(MetricAggregatorBehaviors metricAggregatorBehaviors)
    {
        this.MetricAggregatorBehaviors = metricAggregatorBehaviors;
    }

    public MetricAggregatorBehaviors MetricAggregatorBehaviors { get; }
}

[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
internal sealed class MetricPointBehaviorsAttribute : Attribute
{
    public MetricPointBehaviorsAttribute(MetricPointBehaviors metricPointBehaviors)
    {
        this.MetricPointBehaviors = metricPointBehaviors;
    }

    public MetricPointBehaviors MetricPointBehaviors { get; }
}

internal interface IMetricMeasurementHandler
{
    void RecordMeasurement<T>(
        AggregatorStore aggregatorStore,
        T value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags);

    void RecordMeasurementOnMetricPoint<T>(
        AggregatorStore aggregatorStore,
        ref MetricPoint metricPoint,
        T value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags);
}

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
        throw new NotSupportedException($"Measurements of type '{typeof(T)}' are not supported.");
    }
}
