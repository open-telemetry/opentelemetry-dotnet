// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// The SimpleFixedSizeExemplarReservoir implementation.
/// </summary>
/// <remarks>
/// Specification: <see
/// href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#simplefixedsizeexemplarreservoir"/>.
/// </remarks>
internal sealed class SimpleFixedSizeExemplarReservoir : FixedSizeExemplarReservoir
{
    private int measurementsSeen;

    public SimpleFixedSizeExemplarReservoir()
        : this(Environment.ProcessorCount)
    {
    }

    public SimpleFixedSizeExemplarReservoir(int capacity)
        : base(capacity)
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

    protected override void OnReset()
    {
        Interlocked.Exchange(ref this.measurementsSeen, 0);
    }

    private void Offer<T>(in ExemplarMeasurement<T> measurement)
        where T : struct
    {
        var currentMeasurement = Interlocked.Increment(ref this.measurementsSeen) - 1;

        if (currentMeasurement < this.Capacity)
        {
            this.UpdateExemplar(currentMeasurement, in measurement);
        }
        else
        {
            int index = ThreadSafeRandom.Next(0, currentMeasurement);
            if (index < this.Capacity)
            {
                this.UpdateExemplar(index, in measurement);
            }
        }
    }
}
