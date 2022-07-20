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

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Represents an exponential bucket histogram with base = 2 ^ (2 ^ (-scale)).
    /// An exponential bucket histogram has infinite number of buckets, which are
    /// identified by <c>Bucket[i] = ( base ^ i, base ^ (i + 1) ]</c>, where <c>i</c>
    /// is an integer.
    /// </summary>
    internal class ExponentialBucketHistogram
    {
        private static readonly double Log2E = Math.Log2(Math.E); // 1 / Math.Log(2)

        private int scale;
        private double scalingFactor; // 2 ^ scale / log(2)

        public ExponentialBucketHistogram(int scale, int maxBuckets = 160)
        {
            Guard.ThrowIfOutOfRange(scale, min: -20, max: 20); // TODO: calculate the actual range

            this.Scale = scale;
        }

        internal int Scale
        {
            get
            {
                return this.scale;
            }

            private set
            {
                this.scale = value;
                this.scalingFactor = Math.ScaleB(Log2E, value);
            }
        }

        internal long ZeroCount { get; private set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            return nameof(ExponentialBucketHistogram)
                + "{"
                + nameof(this.Scale) + "=" + this.Scale
                + "}";
        }

        public int MapToIndex(double value)
        {
            Debug.Assert(value != 0, "IEEE-754 zero values should be handled by ZeroCount.");

            // TODO: handle +Inf, -Inf, NaN

            value = Math.Abs(value);

            if (this.Scale > 0)
            {
                // TODO: due to precision issue, the values that are close to the bucket
                // boundaries should be closely examined to avoid off-by-one.
                return (int)Math.Ceiling(Math.Log(value) * this.scalingFactor) - 1;
            }
            else
            {
                var bits = BitConverter.DoubleToInt64Bits(value);
                var exp = (int)((bits & IEEE754Double.EXPONENT_MASK) >> IEEE754Double.FRACTION_BITS);
                var fraction = bits & IEEE754Double.FRACTION_MASK;

                if (exp == 0)
                {
                    // TODO: benchmark and see if this should be changed to a lookup table.
                    fraction--;

                    for (int i = IEEE754Double.FRACTION_BITS - 1; i >= 0; i--)
                    {
                        if ((fraction >> i) != 0)
                        {
                            break;
                        }

                        exp--;
                    }
                }
                else if (fraction == 0)
                {
                    exp--;
                }

                return (exp - IEEE754Double.EXPONENT_BIAS) >> -this.Scale;
            }
        }

        public sealed class IEEE754Double
        {
#pragma warning disable SA1310 // Field name should not contain an underscore
            internal const int EXPONENT_BIAS = 1023;
            internal const long EXPONENT_MASK = 0x7FF0000000000000L;
            internal const int FRACTION_BITS = 52;
            internal const long FRACTION_MASK = 0xFFFFFFFFFFFFFL;
#pragma warning restore SA1310 // Field name should not contain an underscore

            public static string ToString(double value)
            {
                var repr = Convert.ToString(BitConverter.DoubleToInt64Bits(value), 2);
                return new string('0', 64 - repr.Length) + repr + ":" + "(" + value + ")";
            }
        }
    }
}

#endif
