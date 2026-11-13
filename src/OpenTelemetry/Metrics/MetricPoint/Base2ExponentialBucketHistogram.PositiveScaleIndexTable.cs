// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Metrics;

/// <content>
/// Contains the lookup table that maps values to bucket indexes at positive scales.
/// </content>
internal sealed partial class Base2ExponentialBucketHistogram
{
    /// <summary>
    /// Maps values to bucket indexes for a positive scale using a lookup table of
    /// precomputed bucket boundaries instead of the logarithm function.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a positive scale <c>s</c> there are <c>2^s</c> buckets per power of two, so a
    /// value <c>2^e * m</c> with mantissa <c>m</c> in <c>[1, 2)</c> maps to index
    /// <c>(e &lt;&lt; s) + k - 1</c>, where <c>k</c> is the smallest integer in
    /// <c>[0, 2^s]</c> with <c>m &lt;= 2^(k / 2^s)</c>. The exponent part is a shift, and
    /// the mantissa part is found by comparing <c>m</c> against the <c>2^s + 1</c>
    /// boundaries <c>2^(k / 2^s)</c>.
    /// </para>
    /// <para>
    /// Each boundary is stored as the largest <see cref="double"/> that does not exceed
    /// the true (irrational) boundary, which is established with exact integer arithmetic
    /// when the table is built. The comparison <c>m &lt;= boundary</c> is therefore exact
    /// for every <see cref="double"/> mantissa, so the mapping is exact for every input,
    /// unlike the logarithm method which can misplace values near a boundary.
    /// </para>
    /// <para>
    /// The table is indexed by the top <c>s</c> bits of the mantissa, which select a
    /// sub-range of width <c>2^-s</c>. Consecutive boundaries are at least
    /// <c>ln(2) / 2^s</c> apart, so a sub-range contains at most two boundaries, and each
    /// entry holds the first candidate <c>k</c> for its sub-range together with the two
    /// boundaries that may lie above the sub-range's lowest mantissa. The mantissa is
    /// compared against both with integer arithmetic on the IEEE 754 bit patterns, which
    /// order the same way as the positive values they encode, so the lookup is one load
    /// and a few arithmetic operations with no data-dependent branches.
    /// </para>
    /// </remarks>
    internal sealed class PositiveScaleIndexTable
    {
        /// <summary>
        /// The largest scale a table is built for. Beyond this the tables grow too large
        /// (<c>2^scale</c> entries) and the logarithm method is used instead.
        /// </summary>
        internal const int MaxScale = 10;

        private const int FractionWidth = 52;
        private const long FractionMask = 0xFFFFFFFFFFFFFL;
        private const long ImplicitBit = 1L << FractionWidth;
        private const long OneBits = 0x3FF0000000000000L;
        private const int ExponentBias = 1023;
        private const double TwoPow52 = 4503599627370496.0;

        private static readonly PositiveScaleIndexTable?[] Tables = new PositiveScaleIndexTable?[MaxScale + 1];
        private static readonly Lock TablesLock = new();

        private readonly Entry[] entries;
        private readonly long[] boundaries;
        private readonly int scale;

        private PositiveScaleIndexTable(int scale)
        {
            Debug.Assert(scale is > 0 and <= MaxScale, "scale was out of range");

            var count = 1 << scale;
            var boundaries = new long[count + 1];
            var entries = new Entry[count];

            boundaries[0] = OneBits;
            boundaries[count] = BitConverter.DoubleToInt64Bits(2);

            for (var k = 1; k < count; k++)
            {
                boundaries[k] = LargestDoubleAtMostBoundary(k, scale);
            }

            var start = 0;

            for (var j = 0; j < count; j++)
            {
                // The lowest mantissa whose top bits are j is exactly representable, and the
                // entry starts at the first boundary that is not below it.
                var lowest = BitConverter.DoubleToInt64Bits(1 + ((double)j / count));

                while (boundaries[start] < lowest)
                {
                    start++;
                }

                entries[j] = new Entry
                {
                    Start = start,
                    Lower = boundaries[start],
                    Upper = boundaries[Math.Min(start + 1, count)],
                };
            }

            this.entries = entries;
            this.boundaries = boundaries;
            this.scale = scale;
        }

        /// <summary>
        /// Gets the number of boundaries in the table, for testing.
        /// </summary>
        internal int Count => this.boundaries.Length;

        /// <summary>
        /// Gets the table for a scale, building it on first use.
        /// </summary>
        /// <param name="scale">The scale.</param>
        /// <returns>The table, or <see langword="null"/> when the scale has no table.</returns>
        public static PositiveScaleIndexTable? GetOrCreate(int scale)
        {
            if (scale is <= 0 or > MaxScale)
            {
                return null;
            }

            var table = Volatile.Read(ref Tables[scale]);

            if (table == null)
            {
                // Building a table is comparatively expensive, so threads that race to the first
                // use of a scale wait for one of them to build it rather than each building it.
                lock (TablesLock)
                {
                    table = Tables[scale];

                    if (table == null)
                    {
                        table = new(scale);
                        Volatile.Write(ref Tables[scale], table);
                    }
                }
            }

            return table;
        }

        /// <summary>
        /// Maps a finite positive value to its bucket index.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="bits">The IEEE 754 bits of <paramref name="value"/>.</param>
        /// <param name="fraction">The fraction bits of <paramref name="value"/>.</param>
        /// <returns>The bucket index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int MapToIndex(double value, long bits, long fraction)
        {
            var exponent = (int)((bits >> FractionWidth) & 0x7FF);
            var bias = ExponentBias;

            if (exponent == 0)
            {
                // A subnormal has no implicit leading bit. Scaling by 2^52 is exact and
                // makes it normal, so the mantissa and exponent can be read as usual.
                bits = BitConverter.DoubleToInt64Bits(value * TwoPow52);
                exponent = (int)((bits >> FractionWidth) & 0x7FF);
                fraction = bits & FractionMask;
                bias += FractionWidth;
            }

            // The bit pattern of the mantissa as a double in [1, 2), which compares the same
            // way as the double. A boundary is below the mantissa exactly when the difference
            // of the two patterns is negative, so the sign bit counts the boundaries passed.
            var mantissa = fraction | OneBits;
            ref readonly var entry = ref this.entries[(int)(fraction >> (FractionWidth - this.scale))];

            var k = entry.Start
                + (int)((ulong)(entry.Lower - mantissa) >> 63)
                + (int)((ulong)(entry.Upper - mantissa) >> 63);

            return ((exponent - bias) << this.scale) + k - 1;
        }

        /// <summary>
        /// Gets the boundary at an index, for testing.
        /// </summary>
        /// <param name="k">The index of the boundary.</param>
        /// <returns>The largest <see cref="double"/> not exceeding <c>2^(k / 2^scale)</c>.</returns>
        internal double GetBoundary(int k) => BitConverter.Int64BitsToDouble(this.boundaries[k]);

        private static long LargestDoubleAtMostBoundary(int k, int scale)
        {
            var count = 1 << scale;

            // The runtime's estimate is close to the true boundary but may be on either side of
            // it, and its accuracy is not guaranteed across platforms. Step up until the candidate
            // exceeds the boundary, then down to the largest double that does not, so the result
            // is exact however far off the estimate is.
            var bits = BitConverter.DoubleToInt64Bits(Math.Pow(2, (double)k / count));

            if (ExceedsBoundary(bits, k, scale))
            {
                do
                {
                    bits--;
                }
                while (ExceedsBoundary(bits, k, scale));
            }
            else
            {
                while (!ExceedsBoundary(bits + 1, k, scale))
                {
                    bits++;
                }
            }

            return bits;
        }

        private static bool ExceedsBoundary(long bits, int k, int scale)
        {
            // The double is m * 2^-52 for the 53-bit integer m, so it exceeds 2^(k / 2^scale)
            // exactly when m^(2^scale) > 2^(k + 52 * 2^scale). The power has 53 * 2^scale bits,
            // which is expensive to compute in full at the larger scales, so it is first squared
            // 'scale' times at a fixed precision, keeping a lower and an upper bound that bracket
            // the exact value. A double candidate differs from the irrational boundary by far more
            // than the bounds' width, so this decides the comparison; the exact power is computed
            // only in the vanishingly rare case that the bounds straddle the threshold.
            const int Precision = 192;

            var mantissa = new BigInteger((bits & FractionMask) | ImplicitBit);
            var threshold = k + (FractionWidth << scale);

            var lower = mantissa;
            var upper = mantissa;
            long exponent = 0;

            for (var i = 0; i < scale; i++)
            {
                lower *= lower;
                upper *= upper;
                exponent *= 2;

                var excess = BitLength(lower) - Precision;

                if (excess > 0)
                {
                    // Truncating rounds the lower bound down; adding one after truncating rounds
                    // the upper bound up, so the exact value stays between them.
                    lower >>= excess;
                    upper = (upper >> excess) + BigInteger.One;
                    exponent += excess;
                }
            }

            // The value is between lower * 2^exponent and upper * 2^exponent, compared with 2^threshold.
            var shift = threshold - exponent;

            if (shift < 0)
            {
                // Even the lower bound is at least 2^exponent, which is above the threshold.
                return true;
            }

            if (shift > Precision + 1)
            {
                // Even the upper bound, which has at most Precision + 1 bits, is below the threshold.
                return false;
            }

            var limit = BigInteger.One << (int)shift;

            if (lower > limit)
            {
                return true;
            }

            if (upper < limit)
            {
                return false;
            }

            // Fallback path for the extremely rare case that the bounds straddle the threshold
            var power = BigInteger.Pow(mantissa, 1 << scale);
            return power > (BigInteger.One << threshold);

            static int BitLength(BigInteger value)
            {
#if NET
                return (int)value.GetBitLength();
#else
                // The array is little-endian two's complement, so a positive value whose top bit is
                // set carries an extra zero byte that must be skipped.
                var bytes = value.ToByteArray();
                var top = bytes.Length - 1;

                while (top > 0 && bytes[top] == 0)
                {
                    top--;
                }

                var length = top * 8;

                for (var b = bytes[top]; b != 0; b >>= 1)
                {
                    length++;
                }

                return length;
#endif
            }
        }

        /// <summary>
        /// The lookup entry for the mantissas that share their top <c>scale</c> bits.
        /// </summary>
        private struct Entry
        {
            /// <summary>
            /// The bit pattern of the first boundary not below the lowest of the mantissas.
            /// </summary>
            public long Lower;

            /// <summary>
            /// The bit pattern of the boundary after <see cref="Lower"/>, or <see cref="Lower"/>
            /// itself when it is the last boundary.
            /// </summary>
            public long Upper;

            /// <summary>
            /// The index of <see cref="Lower"/>, which is the smallest possible <c>k</c>.
            /// </summary>
            public int Start;
        }
    }
}
