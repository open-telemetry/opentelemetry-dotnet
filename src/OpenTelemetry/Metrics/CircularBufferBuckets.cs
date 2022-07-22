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

using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// A histogram buckets implementation based on circular buffer.
/// </summary>
internal class CircularBufferBuckets
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
            if (diff >= capacity)
            {
                return CalculateScaleReduction(diff + 1, capacity);
            }

            this.end = index;
        }
        else if (index < this.begin)
        {
            var diff = this.end - index;
            if (diff >= this.Capacity)
            {
                return CalculateScaleReduction(diff + 1, capacity);
            }

            this.begin = index;
        }

        this.trait[this.ModuloIndex(index)] += 1;

        return 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int CalculateScaleReduction(int size, int capacity)
        {
            var shift = MathHelper.LeadingZero32(capacity) - MathHelper.LeadingZero32(size);

            if (size > (capacity << shift))
            {
                shift++;
            }

            return shift;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ModuloIndex(int value)
    {
        value %= this.Capacity;

        if (value < 0)
        {
            value += this.Capacity;
        }

        return value;
    }
}
