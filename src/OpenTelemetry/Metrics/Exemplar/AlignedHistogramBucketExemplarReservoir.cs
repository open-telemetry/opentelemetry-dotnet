// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// The AlignedHistogramBucketExemplarReservoir implementation.
/// </summary>
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
