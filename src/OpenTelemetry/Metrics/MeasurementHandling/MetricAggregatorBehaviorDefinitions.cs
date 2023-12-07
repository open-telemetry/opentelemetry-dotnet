// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
