// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// A histogram buckets implementation based on circular buffer.
/// </summary>
internal sealed class CircularBufferBuckets
{
    private long[]? trait;
    private int begin = 0;
    private int end = -1;

    public CircularBufferBuckets(int capacity)
    {
        Guard.ThrowIfOutOfRange(capacity, min: 1);

        this.Capacity = capacity;
    }

    /// <summary>
    /// Gets the capacity of the <see cref="CircularBufferBuckets"/>.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// Gets the offset of the start index for the <see cref="CircularBufferBuckets"/>.
    /// </summary>
    public int Offset => this.begin;

    /// <summary>
    /// Gets the size of the <see cref="CircularBufferBuckets"/>.
    /// </summary>
    public int Size => this.end - this.begin + 1;

    /// <summary>
    /// Returns the value of <c>Bucket[index]</c>.
    /// </summary>
    /// <param name="index">The index of the bucket.</param>
    /// <remarks>
    /// The "index" value can be positive, zero or negative.
    /// This method does not validate if "index" falls into [begin, end],
    /// the caller is responsible for the validation.
    /// </remarks>
    public long this[int index]
    {
        get
        {
            Debug.Assert(this.trait != null, "trait was null");

            return this.trait![this.ModuloIndex(index)];
        }
    }

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

        if (this.trait == null)
        {
            this.trait = new long[capacity];

            this.begin = index;
            this.end = index;
            this.trait[this.ModuloIndex(index)] += value;

            return 0;
        }

        var begin = this.begin;
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
            this.trait[this.ModuloIndex(index)] += value;

            return 0;
        }

        var diff = end - begin;

        if (diff >= capacity || diff < 0)
        {
            return CalculateScaleReduction(begin, end, capacity);
        }

        this.begin = begin;
        this.end = end;

        this.trait[this.ModuloIndex(index)] += value;

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
        uint capacity = (uint)this.Capacity;
        var offset = (uint)this.ModuloIndex(this.begin);

        var currentBegin = this.begin;
        var currentEnd = this.end;

        for (int i = 0; i < level; i++)
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

        this.begin = currentBegin;
        this.end = currentEnd;

        if (capacity > 1)
        {
            AdjustPosition(this.trait, offset, (uint)this.ModuloIndex(currentBegin), (uint)(currentEnd - currentBegin + 1), capacity);
        }

        return;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ScaleDownInternal(long[] array, uint offset, int begin, int end, uint capacity)
        {
            for (var index = begin + 1; index < end; index++)
            {
                Consolidate(array, (offset + (uint)(index - begin)) % capacity, (offset + (uint)((index >> 1) - (begin >> 1))) % capacity);
            }

            // Don't merge below call into above for loop.
            // Merging causes above loop to be infinite if end = int.MaxValue, because index <= int.MaxValue is always true.
            Consolidate(array, (offset + (uint)(end - begin)) % capacity, (offset + (uint)((end >> 1) - (begin >> 1))) % capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void AdjustPosition(long[] array, uint src, uint dst, uint size, uint capacity)
        {
            var advancement = (dst + capacity - src) % capacity;

            if (advancement == 0)
            {
                return;
            }

            if (size - 1 == advancement && advancement << 1 == capacity)
            {
                Exchange(array, src++, dst++);
                size -= 2;
            }
            else if (advancement < size)
            {
                src = src + size - 1;
                dst = dst + size - 1;

                while (size-- != 0)
                {
                    Move(array, src-- % capacity, dst-- % capacity);
                }

                return;
            }

            while (size-- != 0)
            {
                Move(array, src++ % capacity, dst++ % capacity);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Consolidate(long[] array, uint src, uint dst)
        {
            array[dst] += array[src];
            array[src] = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Exchange(long[] array, uint src, uint dst)
        {
            var value = array[dst];
            array[dst] = array[src];
            array[src] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Move(long[] array, uint src, uint dst)
        {
            array[dst] = array[src];
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
    }

    internal void Copy(long[] dst)
    {
        Debug.Assert(dst.Length == this.Capacity, "The length of the destination array must equal the capacity.");

        if (this.trait != null)
        {
            for (var i = 0; i < this.Size; ++i)
            {
                dst[i] = this[this.Offset + i];
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ModuloIndex(int value)
    {
        return MathHelper.PositiveModulo32(value, this.Capacity);
    }
}
