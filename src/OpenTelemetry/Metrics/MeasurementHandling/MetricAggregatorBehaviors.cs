// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

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
