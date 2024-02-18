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
        var activeBuffer = Volatile.Read(ref this.activeBuffer);

        Debug.Assert(activeBuffer != null, "activeBuffer was null");

        Volatile.Write(ref this.activeBuffer, null);

        var inactiveBuffer = activeBuffer == this.bufferA
            ? this.bufferB
            : this.bufferA;

        if (this.ResetOnCollect)
        {
            for (int i = 0; i < inactiveBuffer.Length; i++)
            {
                inactiveBuffer[i].Reset();
            }
        }
        else
        {
#if NET6_0_OR_GREATER
            var length = this.Capacity;
            ref var inactive = ref MemoryMarshal.GetArrayDataReference(inactiveBuffer);
            ref var active = ref MemoryMarshal.GetArrayDataReference(activeBuffer);
            do
            {
                if (active.IsUpdated())
                {
                    active.Copy(ref inactive);
                }

                inactive = ref Unsafe.Add(ref inactive, 1);
                active = ref Unsafe.Add(ref active, 1);
            }
            while (--length > 0);
#else
            for (int i = 0; i <= activeBuffer!.Length; i++)
            {
                ref var active = ref activeBuffer[i];
                if (active.IsUpdated())
                {
                    active.Copy(ref inactiveBuffer[i]);
                }
            }
#endif
        }

        this.OnCollectionCompleted();

        Volatile.Write(ref this.activeBuffer, inactiveBuffer);

        return new(activeBuffer!);
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
        var activeBuffer = Volatile.Read(ref this.activeBuffer)
            ?? this.AcquireActiveBufferRare();

        activeBuffer[exemplarIndex].Update(in measurement);
    }

    private Exemplar[] AcquireActiveBufferRare()
    {
        // Note: We reach here if performing a write while racing with collect.

        Exemplar[]? activeBuffer;
        var spinWait = default(SpinWait);
        do
        {
            spinWait.SpinOnce();
        }
        while ((activeBuffer = Volatile.Read(ref this.activeBuffer)) == null);

        return activeBuffer;
    }
}
