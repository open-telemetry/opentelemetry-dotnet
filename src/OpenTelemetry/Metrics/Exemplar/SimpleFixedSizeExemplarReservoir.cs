// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// The SimpleExemplarReservoir implementation.
/// </summary>
internal sealed class SimpleFixedSizeExemplarReservoir : FixedSizeExemplarReservoir
{
    private readonly Random random;

    private int measurementsSeen;

    public SimpleFixedSizeExemplarReservoir(int poolSize)
        : base(poolSize)
    {
        this.measurementsSeen = 0;
        this.random = new Random();
    }

    public override void Offer(in ExemplarMeasurement<long> measurement)
    {
        this.Offer(in measurement);
    }

    public override void Offer(in ExemplarMeasurement<double> measurement)
    {
        this.Offer(in measurement);
    }

    public override ReadOnlyExemplarCollection Collect()
    {
        // Reset internal state irrespective of temporality.
        // This ensures incoming measurements have fair chance
        // of making it to the reservoir.
        this.measurementsSeen = 0;

        return base.Collect();
    }

    private void Offer<T>(in ExemplarMeasurement<T> measurement)
        where T : struct
    {
        if (this.measurementsSeen < this.ExemplarCount)
        {
            this.UpdateExemplar(this.measurementsSeen, in measurement);
        }
        else
        {
            int index;
            lock (this.random)
            {
                index = this.random.Next(0, this.measurementsSeen);
            }

            if (index < this.ExemplarCount)
            {
                this.UpdateExemplar(index, in measurement);
            }
        }

        Interlocked.Increment(ref this.measurementsSeen);
    }
}
