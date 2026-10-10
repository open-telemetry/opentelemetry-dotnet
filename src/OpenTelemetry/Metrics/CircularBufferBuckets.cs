// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// A histogram buckets implementation based on circular buffer.
/// </summary>
/// <remarks>
/// The bucket with index <see cref="Offset"/> lives in an arbitrary slot of the
/// underlying array and the following buckets occupy the following slots, wrapping
/// at the end of the array. Because every live index lies within
/// <see cref="Capacity"/> of <see cref="Offset"/>, a slot is found with a single
/// subtraction and at most one wrap, so no division is needed on the measurement
/// path and the slots never have to be moved when the scale changes.
/// </remarks>
internal sealed class CircularBufferBuckets
{
    private long[]? trait;
    private int end = -1;

    // The slot of the underlying array that holds Bucket[Offset].
    private int offsetSlot;

    public CircularBufferBuckets(int capacity)
    {
        Guard.ThrowIfOutOfRange(capacity, min: 2);

        this.Capacity = capacity;
    }

    /// <summary>
    /// Gets the capacity of the <see cref="CircularBufferBuckets"/>.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Gets the offset of the start index for the <see cref="CircularBufferBuckets"/>.
    /// </summary>
    public int Offset { get; private set; }

    /// <summary>
    /// Gets the size of the <see cref="CircularBufferBuckets"/>.
    /// </summary>
    public int Size => this.end - this.Offset + 1;

    /// <summary>
    /// Returns the value of <c>Bucket[index]</c>.
    /// </summary>
    /// <param name="index">The index of the bucket.</param>
    /// <remarks>
    /// The "index" value can be positive, zero or negative.
    /// This method does not validate if "index" falls into [begin, end],
    /// the caller is responsible for the validation.
    /// </remarks>
    public long this[int index] => this.trait![this.Slot(index)];

    /// <summary>
    /// Attempts to increment the value of <c>Bucket[index]</c> by <c>value</c>.
    /// </summary>
    /// <param name="index">The index of the bucket.</param>
    /// <param name="value">The increment.</param>
    /// <returns>
    /// Returns <c>0</c> if the increment attempt succeeded;
    /// Returns a positive integer indicating the minimum scale reduction level
    /// if the increment attempt failed.
    /// </returns>
    /// <remarks>
    /// The "index" value can be positive, zero or negative.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int TryIncrement(int index, long value = 1)
    {
        var capacity = this.Capacity;

        if (this.trait == null || this.end < this.Offset)
        {
            this.trait ??= new long[capacity];

            this.Offset = index;
            this.end = index;
            this.offsetSlot = 0;
            this.trait[0] += value;

            return 0;
        }

        var begin = this.Offset;
        var end = this.end;

        if (index > end)
        {
            end = index;
        }
        else if (index < begin)
        {
            begin = index;
        }
        else
        {
            this.trait[this.Slot(index)] += value;

            return 0;
        }

        var diff = end - begin;

        if (diff >= capacity || diff < 0)
        {
            return CalculateScaleReduction(begin, end, capacity);
        }

        if (begin < this.Offset)
        {
            // The window grew downwards, so the slot of the first bucket moves back by
            // the same amount. The distance is less than the capacity, so one wrap suffices.
            var slot = this.offsetSlot - (this.Offset - begin);

            if (slot < 0)
            {
                slot += capacity;
            }

            this.offsetSlot = slot;
        }

        this.Offset = begin;
        this.end = end;

        this.trait[this.Slot(index)] += value;

        return 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int CalculateScaleReduction(int begin, int end, int capacity)
        {
            Debug.Assert(capacity >= 2, "The capacity must be at least 2.");

            var retval = 0;
            var diff = end - begin;
            while (diff >= capacity || diff < 0)
            {
                begin >>= 1;
                end >>= 1;
                diff = end - begin;
                retval++;
            }

            return retval;
        }
    }

    public void ScaleDown(int level = 1)
    {
        Debug.Assert(level > 0, "The scale down level must be a positive integer.");

        if (this.trait == null)
        {
            return;
        }

        // 0 <= offset < capacity <= 2147483647
        var capacity = this.Capacity;
        var offset = this.offsetSlot;

        var currentBegin = this.Offset;
        var currentEnd = this.end;

        for (var i = 0; i < level; i++)
        {
            var newBegin = currentBegin >> 1;
            var newEnd = currentEnd >> 1;

            if (currentBegin != currentEnd)
            {
                if (currentBegin % 2 == 0)
                {
                    ScaleDownInternal(this.trait, offset, currentBegin, currentEnd, capacity);
                }
                else
                {
                    currentBegin++;

                    if (currentBegin != currentEnd)
                    {
                        ScaleDownInternal(this.trait, offset + 1, currentBegin, currentEnd, capacity);
                    }
                }
            }

            currentBegin = newBegin;
            currentEnd = newEnd;
        }

        // Each level consolidates Bucket[index] into the slot offset + (index >> 1) - (begin >> 1),
        // so after every level the first bucket of the new window is still in the original slot
        // of the first bucket and offsetSlot does not change.
        this.Offset = currentBegin;
        this.end = currentEnd;

        return;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ScaleDownInternal(long[] array, int offset, int begin, int end, int capacity)
        {
            for (var index = begin + 1; index < end; index++)
            {
                Consolidate(array, Wrap(offset + (index - begin), capacity), Wrap(offset + ((index >> 1) - (begin >> 1)), capacity));
            }

            // Don't merge below call into above for loop.
            // Merging causes above loop to be infinite if end = int.MaxValue, because index <= int.MaxValue is always true.
            Consolidate(array, Wrap(offset + (end - begin), capacity), Wrap(offset + ((end >> 1) - (begin >> 1)), capacity));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int Wrap(int slot, int capacity)
        {
            // offset is at most capacity and the distance is less than capacity, so one wrap suffices.
            Debug.Assert(slot >= 0 && slot < 2 * capacity, "slot was out of range");

            return slot >= capacity ? slot - capacity : slot;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Consolidate(long[] array, int src, int dst)
        {
            array[dst] += array[src];
            array[src] = 0;
        }
    }

    internal void Reset()
    {
        if (this.trait != null)
        {
#if NET
            Array.Clear(this.trait);
#else
            Array.Clear(this.trait, 0, this.trait.Length);
#endif
        }

        this.Offset = 0;
        this.end = -1;
        this.offsetSlot = 0;
    }

    internal void Copy(long[] dst)
    {
        Debug.Assert(dst.Length >= this.Size, "The length of the destination array must be at least the size.");

        if (this.trait != null)
        {
            var size = this.Size;
            var offset = this.offsetSlot;
            var first = Math.Min(size, this.Capacity - offset);
            Array.Copy(this.trait, offset, dst, 0, first);

            if (first < size)
            {
                Array.Copy(this.trait, 0, dst, first, size - first);
            }
        }
    }

    /// <summary>
    /// Returns the slot of the underlying array that holds <c>Bucket[index]</c>.
    /// </summary>
    /// <param name="index">The index of the bucket, which must be within the window.</param>
    /// <returns>The slot.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Slot(int index)
    {
        Debug.Assert(index >= this.Offset && index - this.Offset < this.Capacity, "index was outside the window");

        var capacity = this.Capacity;
        var slot = this.offsetSlot + (index - this.Offset);

        // Subtract the capacity when the slot has run past the end of the array. Whether it
        // has depends on the value being recorded, so this is done with a mask rather than a
        // branch, which would mispredict for a window that wraps around the array.
        return slot - (capacity & ((capacity - 1 - slot) >> 31));
    }
}
