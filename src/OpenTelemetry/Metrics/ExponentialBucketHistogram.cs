// <copyright file="ExponentialBucketHistogram.cs" company="OpenTelemetry Authors">
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

#if NET6_0_OR_GREATER

using System;
using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Represents an exponential bucket histogram with base = 2 ^ (2 ^ (-scale)).
/// An exponential bucket histogram has infinite number of buckets, which are
/// identified by <c>Bucket[index] = ( base ^ index, base ^ (index + 1) ]</c>,
/// where <c>index</c> is an integer.
/// </summary>
internal class ExponentialBucketHistogram
{
    private static readonly double Log2E = Math.Log2(Math.E); // 1 / Math.Log(2)

    private int scale;
    private double scalingFactor; // 2 ^ scale / log(2)

    public ExponentialBucketHistogram(int scale, int maxBuckets = 160)
    {
        Guard.ThrowIfOutOfRange(scale, min: -20, max: 20); // TODO: calculate the actual range
        Guard.ThrowIfOutOfRange(maxBuckets, min: 1);

        this.Scale = scale;
        this.PositiveBuckets = new CircularBufferBuckets(maxBuckets);
        this.NegativeBuckets = new CircularBufferBuckets(maxBuckets);
    }

    internal int Scale
    {
        get => this.scale;

        private set
        {
            this.scale = value;
            this.scalingFactor = Math.ScaleB(Log2E, value);
        }
    }

    internal CircularBufferBuckets PositiveBuckets { get; }

    internal long ZeroCount { get; private set; }

    internal CircularBufferBuckets NegativeBuckets { get; }

    /// <inheritdoc/>
    public override string ToString()
    {
        return nameof(ExponentialBucketHistogram)
            + "{"
            + nameof(this.Scale) + "=" + this.Scale
            + "}";
    }

    /// <summary>
    /// Maps a finite positive IEEE 754 double-precision floating-point
    /// number to <c>Bucket[index] = ( base ^ index, base ^ (index + 1) ]</c>,
    /// where <c>index</c> is an integer.
    /// </summary>
    /// <param name="value">
    /// The value to be bucketized. Must be a finite positive number.
    /// </param>
    /// <returns>
    /// Returns the index of the bucket.
    /// </returns>
    public int MapToIndex(double value)
    {
        Debug.Assert(MathHelper.IsFinite(value), "IEEE-754 +Inf, -Inf and NaN should be filtered out before calling this method.");
        Debug.Assert(value != 0, "IEEE-754 zero values should be handled by ZeroCount.");
        Debug.Assert(!double.IsNegative(value), "IEEE-754 negative values should be normalized before calling this method.");

        if (this.Scale > 0)
        {
            // TODO: due to precision issue, the values that are close to the bucket
            // boundaries should be closely examined to avoid off-by-one.
            return (int)Math.Ceiling(Math.Log(value) * this.scalingFactor) - 1;
        }
        else
        {
            var bits = BitConverter.DoubleToInt64Bits(value);
            var exp = (int)((bits & 0x7FF0000000000000L /* exponent mask */) >> 52 /* fraction width */);
            var fraction = bits & 0xFFFFFFFFFFFFFL /* fraction mask */;

            if (exp == 0)
            {
                exp -= MathHelper.LeadingZero64(fraction - 1) - 12 /* 64 - fraction width */;
            }
            else if (fraction == 0)
            {
                exp--;
            }

            return (exp - 1023 /* exponent bias */) >> -this.Scale;
        }
    }

    public void Record(double value)
    {
        if (!MathHelper.IsFinite(value))
        {
            return;
        }

        var c = value.CompareTo(0);

        if (c > 0)
        {
            this.PositiveBuckets.TryIncrement(this.MapToIndex(value));
        }
        else if (c < 0)
        {
            this.NegativeBuckets.TryIncrement(this.MapToIndex(-value));
        }
        else
        {
            this.ZeroCount++;
        }
    }
}

#endif
