// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

internal abstract class FixedSizeExemplarReservoir : ExemplarReservoir
{
    private Exemplar[] bufferA = Array.Empty<Exemplar>();
    private Exemplar[] bufferB = Array.Empty<Exemplar>();
    private Exemplar[]? activeBuffer;

    protected FixedSizeExemplarReservoir(int exemplarCount)
    {
        this.ExemplarCount = exemplarCount;
    }

    internal int ExemplarCount { get; }

    /// <summary>
    /// Collects all the exemplars accumulated by the Reservoir.
    /// </summary>
    /// <returns><see cref="ReadOnlyExemplarCollection"/>.</returns>
    public override ReadOnlyExemplarCollection Collect()
    {
        var currentBuffer = this.activeBuffer;

        Debug.Assert(currentBuffer != null, "currentBuffer was null");

        this.activeBuffer = null;

        var inactiveBuffer = currentBuffer == this.bufferA
            ? this.bufferB
            : this.bufferA;

        if (this.ResetOnCollect)
        {
            for (int i = 0; i < inactiveBuffer.Length; i++)
            {
                inactiveBuffer[i].Reset();
            }
        }

        this.activeBuffer = inactiveBuffer;

        return new(currentBuffer!);
    }

    internal sealed override void Initialize(AggregatorStore aggregatorStore)
    {
        this.bufferA = new Exemplar[this.ExemplarCount];
        this.bufferB = new Exemplar[this.ExemplarCount];
        this.activeBuffer = this.bufferA;

        for (int i = 0; i < this.ExemplarCount; i++)
        {
            this.bufferA[i].KeyFilter = aggregatorStore.TagKeysInteresting;
            this.bufferB[i].KeyFilter = aggregatorStore.TagKeysInteresting;
        }

        base.Initialize(aggregatorStore);
    }

    protected void UpdateExemplar<T>(int exemplarIndex, in ExemplarMeasurement<T> measurement)
        where T : struct
    {
        var activeBuffer = this.activeBuffer;
        if (activeBuffer == null)
        {
            // Note: This is expected to happen when we race with a collection.
            return;
        }

        ref var exemplar = ref activeBuffer[exemplarIndex];

        exemplar.Timestamp = DateTimeOffset.UtcNow;

        if (typeof(T) == typeof(long))
        {
            exemplar.LongValue = (long)(object)measurement.Value;
        }
        else if (typeof(T) == typeof(double))
        {
            exemplar.DoubleValue = (double)(object)measurement.Value;
        }
        else
        {
            Debug.Fail("Invalid value type");
            exemplar.DoubleValue = Convert.ToDouble(measurement.Value);
        }

        var currentActivity = Activity.Current;
        if (currentActivity != null)
        {
            exemplar.TraceId = currentActivity.TraceId;
            exemplar.SpanId = currentActivity.SpanId;
        }
        else
        {
            exemplar.TraceId = default;
            exemplar.SpanId = default;
        }

        exemplar.StoreFilteredTags(measurement.Tags);
    }
}
