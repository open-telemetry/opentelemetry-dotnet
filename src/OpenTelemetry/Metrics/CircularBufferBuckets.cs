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
public sealed class CircularBufferBuckets
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
    /// Attempts to increment the value of <c>Bucket[index]</c>.
    /// </summary>
    /// <param name="index">The index of the bucket.</param>
    /// <returns>
    /// Returns <c>0</c> if the increment attempt succeeded;
    /// Returns a positive integer <c>Math.Ceiling(log_2(X))</c> if the
    /// underlying buffer is running out of capacity, and the buffer has to
    /// increase to <c>X * Capacity</c> at minimum.
    /// </returns>
    /// <remarks>
    /// The "index" value can be positive, zero or negative.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int TryIncrement(int index)
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

        this.trait[this.ModuloIndex(index)] += 1;

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

    public void ScaleDownOld(int n)
    {
        Debug.Assert(n > 0, "The scale down level must be a positive integer.");

        if (this.trait == null)
        {
            return;
        }

        // TODO: avoid allocating new array by doing the calculation in-place.
        var array = new long[this.Capacity];

        for (var index = this.begin; index < this.end; index++)
        {
            array[this.ModuloIndex(index >> n)] += this[index];
        }

        array[this.ModuloIndex(this.end >> n)] += this[this.end];

        this.begin >>= n;
        this.end >>= n;
        this.trait = array;
    }

    public void ScaleDown(int n)
    {
        Debug.Assert(n > 0, "The scale down level must be a positive integer.");

        if (this.trait == null)
        {
            return;
        }

        uint capacity = (uint)this.Capacity;

        var offset = (uint)this.ModuloIndex(this.begin); // offset [0, capacity), where capacity is between [1, 2147483647]
        var currentBegin = this.begin;
        var currentEnd = this.end;

        for (int i = 0; i < n; i++)
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
        // Move(this.trait, this.begin, this.begin + newSize - 1, newBegin, capacity);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ScaleDownInternal(long[] array, uint offset, int begin, int end, uint capacity)
        {
            // System.Console.WriteLine($"ScaleDownInternal({offset}, {begin}, {end})");

            for (var index = begin + 1; index < end; index++)
            {
                Consolidate(array, (offset + (uint)(index - begin)) % capacity, (offset + (uint)((index >> 1) - (begin >> 1))) % capacity);
            }

            Consolidate(array, (offset + (uint)(end - begin)) % capacity, (offset + (uint)((end >> 1) - (begin >> 1))) % capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Consolidate(long[] array, uint src, uint dst)
        {
            // System.Console.WriteLine($"Consolidate({src} => {dst})");
            array[dst] += array[src];
            array[src] = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Move(long[] array, int begin, int end, int dst, int capacity)
        {
            // TODO
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
            + (this.trait == null ? "null" : "[" + string.Join(", ", this.trait) + "]")
            + "}";
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ModuloIndex(int value)
    {
        return MathHelper.PositiveModulo32(value, this.Capacity);
    }
}
