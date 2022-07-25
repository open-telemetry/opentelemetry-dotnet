// <copyright file="CircularBufferBuckets.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// A histogram buckets implementation based on circular buffer.
/// </summary>
internal sealed class CircularBufferBuckets
{
    private long[] trait;
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
        get => this.trait[this.ModuloIndex(index)];
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
        }
        else if (index > this.end)
        {
            var diff = index - this.begin;

            if (diff >= capacity || diff < 0)
            {
                return CalculateScaleReduction(diff + 1, capacity);
            }

            this.end = index;
        }
        else if (index < this.begin)
        {
            var diff = this.end - index;

            if (diff >= this.Capacity || diff < 0)
            {
                return CalculateScaleReduction(diff + 1, capacity);
            }

            this.begin = index;
        }

        this.trait[this.ModuloIndex(index)] += value;

        return 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int CalculateScaleReduction(int size, int capacity)
        {
            var shift = MathHelper.LeadingZero32(capacity);

            if (size > 0)
            {
                shift -= MathHelper.LeadingZero32(size);
            }

            if (size > (capacity << shift))
            {
                shift++;
            }

            return shift;
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

    public override string ToString()
    {
        return nameof(CircularBufferBuckets)
            + "{"
            + nameof(this.Capacity) + "=" + this.Capacity + ", "
            + nameof(this.Size) + "=" + this.Size + ", "
            + nameof(this.begin) + "=" + this.begin + ", "
            + nameof(this.end) + "=" + this.end + ", "
            + (this.trait == null ? "null" : "{" + string.Join(", ", this.trait) + "}")
            + "}";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ModuloIndex(int value)
    {
        return MathHelper.PositiveModulo32(value, this.Capacity);
    }
}
