// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Contains helper methods for the Base2ExponentialBucketHistogram class.
/// </summary>
internal static class Base2ExponentialBucketHistogramHelper
{
    private const double EpsilonTimes2 = double.Epsilon * 2;
    private static readonly double Ln2 = Math.Log(2);

    /// <summary>
    /// Calculate the lower boundary for a Base2ExponentialBucketHistogram bucket.
    /// </summary>
    /// <param name="index">Index.</param>
    /// <param name="scale">Scale.</param>
    /// <returns>Calculated lower boundary.</returns>
    public static double CalculateLowerBoundary(int index, int scale)
    {
        if (scale > 0)
        {
            double lowerBound;
            var indexTable = Base2ExponentialBucketHistogram.PositiveScaleIndexTable.GetOrCreate(scale);

            if (indexTable != null)
            {
                // The boundary is 2^(index / 2^scale) = 2^q * 2^(r / 2^scale) for index = q * 2^scale + r
                // with 0 <= r < 2^scale. The table holds the largest double not exceeding 2^(r / 2^scale)
                // and scaling by 2^q is exact, so this is the same boundary MapToIndex compares against.
                var r = index & ((1 << scale) - 1);
                var q = index >> scale;
                var boundary = indexTable.GetBoundary(r);

                if (q < -1022)
                {
                    // The result is subnormal, so scaling would round to nearest and could round up
                    // above the true boundary. A subnormal is f * 2^-1074 for an integer f and the
                    // table's boundary is m * 2^(q - 52) for its 53-bit integer mantissa m, so the
                    // largest subnormal not exceeding it has f = floor(m * 2^(q + 1022)), which is a
                    // right shift. The table's boundary is within an ulp of the true boundary and the
                    // shift is at least one bit, so no subnormal lies between the two.
                    var mantissa = (BitConverter.DoubleToInt64Bits(boundary) & 0xFFFFFFFFFFFFFL) | (1L << 52);
                    var shift = -1022 - q;

                    lowerBound = shift < 64 ? BitConverter.Int64BitsToDouble(mantissa >> shift) : 0;
                }
                else
                {
                    lowerBound = Math.ScaleB(boundary, q);
                }
            }
            else
            {
                var inverseFactor = Math.ScaleB(Ln2, -scale);

                lowerBound = Math.Exp(index * inverseFactor);
            }

            return lowerBound == 0 ? double.Epsilon : lowerBound;
        }
        else
        {
            if ((scale == -1 && index == -537) || (scale == 0 && index == -1074))
            {
                return EpsilonTimes2;
            }

            var n = index << -scale;

            // LowerBoundary should not return zero.
            // It should return values >= double.Epsilon (2 ^ -1074).
            // n < -1074 occurs at the minimum index of a scale.
            // e.g., At scale -1, minimum index is -538. -538 << 1 = -1075
            if (n < -1074)
            {
                return double.Epsilon;
            }

            return Math.ScaleB(1, n);
        }
    }
}
