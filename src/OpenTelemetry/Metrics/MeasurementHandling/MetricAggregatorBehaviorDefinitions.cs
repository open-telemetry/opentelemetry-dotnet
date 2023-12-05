// <copyright file="MetricAggregatorBehaviorDefinitions.cs" company="OpenTelemetry Authors">
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

    [MetricAggregatorBehaviors(MetricAggregatorBehaviors.DeltaTemporality | MetricAggregatorBehaviors.EmitOverflowAttribute | MetricAggregatorBehaviors.ReclaimMetricPoints)]
    public struct DeltaTemporalityWithEmitOverflowAndMetricPointReclaim
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
