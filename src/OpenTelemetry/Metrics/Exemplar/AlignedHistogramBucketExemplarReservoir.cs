// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// AlignedHistogramBucketExemplarReservoir implementation.
/// </summary>
/// <remarks>
/// Specification: <see
/// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#alignedhistogrambucketexemplarreservoir"/>.
/// </remarks>
internal sealed class AlignedHistogramBucketExemplarReservoir : FixedSizeExemplarReservoir
{
    public AlignedHistogramBucketExemplarReservoir(int numberOfBuckets)
        : base(numberOfBuckets + 1)
    {
    }

    public override void Offer(in ExemplarMeasurement<long> measurement)
    {
        Debug.Fail("AlignedHistogramBucketExemplarReservoir shouldn't be used with long values");
    }

    public override void Offer(in ExemplarMeasurement<double> measurement)
    {
        Debug.Assert(
            measurement.ExplicitBucketHistogramBucketIndex != -1,
            "ExplicitBucketHistogramBucketIndex was -1");

        this.UpdateExemplar(measurement.ExplicitBucketHistogramBucketIndex, in measurement);
    }
}
