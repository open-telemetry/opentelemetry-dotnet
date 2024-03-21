// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// SimpleFixedSizeExemplarReservoir implementation.
/// </summary>
/// <remarks>
/// Specification: <see
/// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#simplefixedsizeexemplarreservoir"/>.
/// </remarks>
internal sealed class SimpleFixedSizeExemplarReservoir : FixedSizeExemplarReservoir
{
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
        this.measurementsSeen = 0;
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
            int index = ThreadSafeRandom.Next(0, measurementNumber);
            if (index < this.Capacity)
            {
                this.UpdateExemplar(index, in measurement);
            }
        }
    }
}
