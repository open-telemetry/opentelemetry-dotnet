// <copyright file="Base2ExponentialBucketHistogram.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Represents an exponential bucket histogram with base = 2 ^ (2 ^ (-scale)).
/// An exponential bucket histogram has infinite number of buckets, which are
/// identified by <c>Bucket[index] = ( base ^ index, base ^ (index + 1) ]</c>,
/// where <c>index</c> is an integer.
/// </summary>
internal sealed class Base2ExponentialBucketHistogram
{
    internal double RunningSum;
    internal double SnapshotSum;

    internal double RunningMin = double.PositiveInfinity;
    internal double SnapshotMin;

    internal double RunningMax = double.NegativeInfinity;
    internal double SnapshotMax;

    internal int IsCriticalSectionOccupied = 0;

    private int scale;
    private double scalingFactor; // 2 ^ scale / log(2)

    /// <summary>
    /// Initializes a new instance of the <see cref="Base2ExponentialBucketHistogram"/> class.
    /// </summary>
    /// <param name="maxBuckets">
    /// The maximum number of buckets in each of the positive and negative ranges, not counting the special zero bucket. The default value is 160.
    /// </param>
    public Base2ExponentialBucketHistogram(int maxBuckets = 160)
        : this(maxBuckets, 20)
    {
    }

    internal Base2ExponentialBucketHistogram(int maxBuckets, int scale)
    {
        /*
        The following table is calculated based on [ MapToIndex(double.Epsilon), MapToIndex(double.MaxValue) ]:

        | Scale | Index Range               |
        | ----- | ------------------------- |
        | < -11 | [-1, 0]                   |
        | -11   | [-1, 0]                   |
        | -10   | [-2, 0]                   |
        | -9    | [-3, 1]                   |
        | -8    | [-5, 3]                   |
        | -7    | [-9, 7]                   |
        | -6    | [-17, 15]                 |
        | -5    | [-34, 31]                 |
        | -4    | [-68, 63]                 |
        | -3    | [-135, 127]               |
        | -2    | [-269, 255]               |
        | -1    | [-538, 511]               |
        | 0     | [-1075, 1023]             |
        | 1     | [-2149, 2047]             |
        | 2     | [-4297, 4095]             |
        | 3     | [-8593, 8191]             |
        | 4     | [-17185, 16383]           |
        | 5     | [-34369, 32767]           |
        | 6     | [-68737, 65535]           |
        | 7     | [-137473, 131071]         |
        | 8     | [-274945, 262143]         |
        | 9     | [-549889, 524287]         |
        | 10    | [-1099777, 1048575]       |
        | 11    | [-2199553, 2097151]       |
        | 12    | [-4399105, 4194303]       |
        | 13    | [-8798209, 8388607]       |
        | 14    | [-17596417, 16777215]     |
        | 15    | [-35192833, 33554431]     |
        | 16    | [-70385665, 67108863]     |
        | 17    | [-140771329, 134217727]   |
        | 18    | [-281542657, 268435455]   |
        | 19    | [-563085313, 536870911]   |
        | 20    | [-1126170625, 1073741823] |
        | 21    | [underflow, 2147483647]   |
        | > 21  | [underflow, overflow]     |
        */
        Guard.ThrowIfOutOfRange(scale, min: -11, max: 20);

        /*
        Regardless of the scale, MapToIndex(1) will always be -1, so we need two buckets at minimum:
            bucket[-1] = (1/base, 1]
            bucket[0] = (1, base]
        */
        Guard.ThrowIfOutOfRange(maxBuckets, min: 2);

        this.Scale = scale;
        this.PositiveBuckets = new CircularBufferBuckets(maxBuckets);
        this.NegativeBuckets = new CircularBufferBuckets(maxBuckets);
    }

    internal int Scale
    {
        get => this.scale;

        set
        {
            this.scale = value;

            // A subset of Math.ScaleB(Math.Log2(Math.E), value)
            this.scalingFactor = BitConverter.Int64BitsToDouble(0x71547652B82FEL | ((0x3FFL + value) << 52 /* fraction width */));
        }
    }

    internal double ScalingFactor => this.scalingFactor;

    internal CircularBufferBuckets PositiveBuckets { get; }

    internal long ZeroCount { get; private set; }

    internal CircularBufferBuckets NegativeBuckets { get; }

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
        Debug.Assert(value > 0, "IEEE-754 negative values should be normalized before calling this method.");

        var bits = BitConverter.DoubleToInt64Bits(value);
        var fraction = bits & 0xFFFFFFFFFFFFFL /* fraction mask */;

        if (this.Scale > 0)
        {
            // TODO: do we really need this given the lookup table is needed for scale>0 anyways?
            if (fraction == 0)
            {
                var exp = (int)((bits & 0x7FF0000000000000L /* exponent mask */) >> 52 /* fraction width */);
                return ((exp - 1023 /* exponent bias */) << this.Scale) - 1;
            }

            // TODO: due to precision issue, the values that are close to the bucket
            // boundaries should be closely examined to avoid off-by-one.

            return (int)Math.Ceiling(Math.Log(value) * this.scalingFactor) - 1;
        }
        else
        {
            var exp = (int)((bits & 0x7FF0000000000000L /* exponent mask */) >> 52 /* fraction width */);

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

        if (c == 0)
        {
            this.ZeroCount++;
            return;
        }

        var index = this.MapToIndex(c > 0 ? value : -value);
        var buckets = c > 0 ? this.PositiveBuckets : this.NegativeBuckets;
        var n = buckets.TryIncrement(index);

        if (n == 0)
        {
            return;
        }

        this.PositiveBuckets.ScaleDown(n);
        this.NegativeBuckets.ScaleDown(n);
        this.Scale -= n;
        n = buckets.TryIncrement(index >> n);
        Debug.Assert(n == 0, "Increment should always succeed after scale down.");
    }
}
