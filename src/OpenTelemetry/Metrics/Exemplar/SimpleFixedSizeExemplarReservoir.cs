// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// The SimpleFixedSizeExemplarReservoir implementation.
/// </summary>
internal sealed class SimpleFixedSizeExemplarReservoir : FixedSizeExemplarReservoir
{
    private readonly Random random = new();

    private int measurementsSeen;

    public SimpleFixedSizeExemplarReservoir(int poolSize)
        : base(poolSize)
    {
    }

    public override void Offer(in ExemplarMeasurement<long> measurement)
    {
        this.Offer(in measurement);
    }

    public override void Offer(in ExemplarMeasurement<double> measurement)
    {
        this.Offer(in measurement);
    }

    protected override void OnCollected()
    {
        // Reset internal state irrespective of temporality.
        // This ensures incoming measurements have fair chance
        // of making it to the reservoir.
        Interlocked.Exchange(ref this.measurementsSeen, 0);
    }

    private void Offer<T>(in ExemplarMeasurement<T> measurement)
        where T : struct
    {
        var measurementNumber = Interlocked.Increment(ref this.measurementsSeen) - 1;

        if (measurementNumber < this.Capacity)
        {
            this.UpdateExemplar(measurementNumber, in measurement);
        }
        else
        {
            var index = this.random.Next(0, measurementNumber);
            if (index < this.Capacity)
            {
                this.UpdateExemplar(index, in measurement);
            }
        }
    }
}
