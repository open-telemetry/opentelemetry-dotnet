// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace OpenTelemetry.Internal;

/// <summary>
/// A counter of in-flight operations which is split across per-processor
/// slots so that threads entering and leaving concurrently do not all
/// serialize on a single cache line.
/// </summary>
/// <remarks>
/// <see cref="Enter"/> returns the slot it incremented and <see cref="Exit"/>
/// must be given that same slot. This keeps every slot non-negative, so a
/// slot read as zero has no operation in flight on it and a sum over all
/// slots taken after a full fence cannot miss an operation which entered
/// before that fence. Slots are padded so that each occupies its own cache
/// line (128 bytes, which also covers the adjacent-line prefetcher on x64
/// and Apple M-series parts).
/// </remarks>
internal sealed class StripedCounter
{
    private const int MaxSlots = 64;

    private readonly Slot[] slots = new Slot[Math.Min(Environment.ProcessorCount, MaxSlots)];

    /// <summary>
    /// Increments the counter.
    /// </summary>
    /// <returns>The slot which was incremented; pass it to <see cref="Exit"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Enter()
    {
        var slots = this.slots;
        var slot = (int)((uint)GetProcessorHint() % (uint)slots.Length);

        Interlocked.Increment(ref slots[slot].Value);

        return slot;
    }

    /// <summary>
    /// Decrements the counter on the slot returned by <see cref="Enter"/>.
    /// </summary>
    /// <param name="slot">The slot returned by <see cref="Enter"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Exit(int slot)
        => Interlocked.Decrement(ref this.slots[slot].Value);

    /// <summary>
    /// Gets a value indicating whether any operation is in flight.
    /// </summary>
    /// <returns><see langword="true"/> if every slot reads zero; otherwise <see langword="false"/>.</returns>
    public bool IsZero()
    {
        var slots = this.slots;
        for (var i = 0; i < slots.Length; i++)
        {
            if (Volatile.Read(ref slots[i].Value) != 0)
            {
                return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetProcessorHint() =>
#if NET
        Thread.GetCurrentProcessorId();
#else
        Environment.CurrentManagedThreadId;
#endif

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    private struct Slot
    {
        [FieldOffset(64)]
        public int Value;
    }
}
