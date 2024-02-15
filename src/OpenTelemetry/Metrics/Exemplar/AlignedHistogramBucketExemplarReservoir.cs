// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// The AlignedHistogramBucketExemplarReservoir implementation.
/// </summary>
/// <remarks>
/// Specification: <see
/// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#alignedhistogrambucketexemplarreservoir"/>.
/// </remarks>
internal sealed class AlignedHistogramBucketExemplarReservoir : FixedSizeExemplarReservoir
{
    public AlignedHistogramBucketExemplarReservoir(int numberOfBuckets)
        : base(numberOfBuckets)
    {
    }

    public override void Offer(in ExemplarMeasurement<long> measurement)
    {
        Debug.Assert(
            measurement.ExplicitBucketHistogramBucketIndex != -1,
            "ExplicitBucketHistogramBucketIndex was -1");

        this.UpdateExemplar(
            measurement.ExplicitBucketHistogramBucketIndex,
            in measurement);
    }

    public override void Offer(in ExemplarMeasurement<double> measurement)
    {
        Debug.Assert(
            measurement.ExplicitBucketHistogramBucketIndex != -1,
            "ExplicitBucketHistogramBucketIndex was -1");

        this.UpdateExemplar(
            measurement.ExplicitBucketHistogramBucketIndex,
            in measurement);
    }
}
