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
    private const int DefaultMeasurementState = -1;
    private int measurementState = DefaultMeasurementState;

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
        Volatile.Write(ref this.measurementState, DefaultMeasurementState);
    }

    private void Offer<T>(in ExemplarMeasurement<T> measurement)
        where T : struct
    {
        var measurementState = Interlocked.Increment(ref this.measurementState);

        if (measurementState < this.Capacity)
        {
            this.UpdateExemplar(measurementState, in measurement);
        }
        else
        {
            int index = ThreadSafeRandom.Next(0, measurementState);
            if (index < this.Capacity)
            {
                this.UpdateExemplar(index, in measurement);
            }
        }
    }
}
