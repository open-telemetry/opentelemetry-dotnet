// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// The SimpleExemplarReservoir implementation.
/// </summary>
internal sealed class SimpleFixedSizeExemplarReservoir : FixedSizeExemplarReservoir
{
    private int measurementsSeen;

    public SimpleFixedSizeExemplarReservoir(int poolSize)
        : base(poolSize)
    {
        this.measurementsSeen = -1;
    }

    public override void Offer(in ExemplarMeasurement<long> measurement)
    {
        this.Offer(in measurement);
    }

    public override void Offer(in ExemplarMeasurement<double> measurement)
    {
        this.Offer(in measurement);
    }

    protected override void OnCollectionCompleted()
    {
        // Reset internal state irrespective of temporality.
        // This ensures incoming measurements have fair chance
        // of making it to the reservoir.
        this.measurementsSeen = 0;
    }

    private void Offer<T>(in ExemplarMeasurement<T> measurement)
        where T : struct
    {
        var currentMeasurement = Interlocked.Increment(ref this.measurementsSeen);

        if (currentMeasurement < this.ExemplarCount)
        {
            this.UpdateExemplar(currentMeasurement, in measurement);
        }
        else
        {
            int index = ThreadSafeRandom.Next(0, currentMeasurement);
            if (index < this.ExemplarCount)
            {
                this.UpdateExemplar(index, in measurement);
            }
        }
    }
}
