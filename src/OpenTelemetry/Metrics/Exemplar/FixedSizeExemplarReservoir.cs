// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NET6_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#endif
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

internal abstract class FixedSizeExemplarReservoir : ExemplarReservoir
{
    private readonly Exemplar[] bufferA;
    private readonly Exemplar[] bufferB;
    private Exemplar[]? activeBuffer;

    protected FixedSizeExemplarReservoir(int capacity)
    {
        Guard.ThrowIfOutOfRange(capacity, min: 1);

        this.bufferA = new Exemplar[capacity];
        this.bufferB = new Exemplar[capacity];
        this.activeBuffer = this.bufferA;
        this.Capacity = capacity;
    }

    internal int Capacity { get; }

    /// <summary>
    /// Collects all the exemplars accumulated by the Reservoir.
    /// </summary>
    /// <returns><see cref="ReadOnlyExemplarCollection"/>.</returns>
    public sealed override ReadOnlyExemplarCollection Collect()
    {
        var currentBuffer = Volatile.Read(ref this.activeBuffer);

        Debug.Assert(currentBuffer != null, "currentBuffer was null");

        Volatile.Write(ref this.activeBuffer, null);

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

        Volatile.Write(ref this.activeBuffer, inactiveBuffer);

        return new(currentBuffer!);
    }

    internal sealed override void Initialize(AggregatorStore aggregatorStore)
    {
        var keyFilter = aggregatorStore.TagKeysInteresting;

#if NET6_0_OR_GREATER
        var length = this.bufferA.Length;
        ref var a = ref MemoryMarshal.GetArrayDataReference(this.bufferA);
        ref var b = ref MemoryMarshal.GetArrayDataReference(this.bufferB);
        do
        {
            a.KeyFilter = keyFilter;
            b.KeyFilter = keyFilter;
            a = ref Unsafe.Add(ref a, 1);
            b = ref Unsafe.Add(ref b, 1);
        }
        while (--length > 0);
#else
        for (int i = 0;
            i < this.bufferA.Length && i < this.bufferB.Length;
            i++)
        {
            this.bufferA[i].KeyFilter = keyFilter;
            this.bufferB[i].KeyFilter = keyFilter;
        }
#endif

        base.Initialize(aggregatorStore);
    }

    protected virtual void OnCollectionCompleted()
    {
    }

    protected void UpdateExemplar<T>(int exemplarIndex, in ExemplarMeasurement<T> measurement)
        where T : struct
    {
        var activeBuffer = Volatile.Read(ref this.activeBuffer);
        if (activeBuffer == null)
        {
            // Note: This is expected to happen when we race with a collection.
            return;
        }

        activeBuffer[exemplarIndex].Update(in measurement);
    }
}
