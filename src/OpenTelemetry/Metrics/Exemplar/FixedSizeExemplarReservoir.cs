// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

internal abstract class FixedSizeExemplarReservoir : ExemplarReservoir
{
    private readonly Exemplar[] bufferA;
    private readonly Exemplar[] bufferB;
    private volatile Exemplar[]? activeBuffer;

    protected FixedSizeExemplarReservoir(int exemplarCount)
    {
        this.bufferA = new Exemplar[exemplarCount];
        this.bufferB = new Exemplar[exemplarCount];
        this.activeBuffer = this.bufferA;
        this.ExemplarCount = exemplarCount;
    }

    internal int ExemplarCount { get; }

    /// <summary>
    /// Collects all the exemplars accumulated by the Reservoir.
    /// </summary>
    /// <returns><see cref="ReadOnlyExemplarCollection"/>.</returns>
    public sealed override ReadOnlyExemplarCollection Collect()
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

        this.OnCollectionCompleted();

        this.activeBuffer = inactiveBuffer;

        return new(currentBuffer!);
    }

    internal sealed override void Initialize(AggregatorStore aggregatorStore)
    {
        var keyFilter = aggregatorStore.TagKeysInteresting;

        for (int a = 0, b = 0;
            a < this.bufferA.Length && b < this.bufferB.Length;
            a++, b++)
        {
            this.bufferA[a].KeyFilter = keyFilter;
            this.bufferB[b].KeyFilter = keyFilter;
        }

        base.Initialize(aggregatorStore);
    }

    protected virtual void OnCollectionCompleted()
    {
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

        activeBuffer[exemplarIndex].Update(in measurement);
    }
}
