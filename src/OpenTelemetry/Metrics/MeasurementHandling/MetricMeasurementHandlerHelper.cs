// <copyright file="MetricMeasurementHandlerHelper.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace OpenTelemetry.Metrics;

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

        AddPermutations<MetricPointBehaviorDefinitions.CumulativeCounterLong>();
        AddPermutations<MetricPointBehaviorDefinitions.CumulativeCounterDouble>();

        AddPermutations<MetricPointBehaviorDefinitions.DeltaCounterLong>();
        AddPermutations<MetricPointBehaviorDefinitions.DeltaCounterDouble>();

        AddPermutations<MetricPointBehaviorDefinitions.CumulativeGaugeLong>();
        AddPermutations<MetricPointBehaviorDefinitions.CumulativeGaugeDouble>();

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
            AddPermutation<MetricAggregatorBehaviorDefinitions.DeltaTemporalityWithEmitOverflowAndMetricPointReclaim>();
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
