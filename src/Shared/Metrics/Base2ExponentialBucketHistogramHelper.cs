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
#if NET
            var inverseFactor = Math.ScaleB(Ln2, -scale);
#else
            var inverseFactor = ScaleB(Ln2, -scale);
#endif
            var lowerBound = Math.Exp(index * inverseFactor);
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

#if NET
            return Math.ScaleB(1, n);
#else
            return ScaleB(1, n);
#endif
        }
    }

#if !NET
    // Math.ScaleB was introduced in .NET Core 3.0.
    // This implementation is from:
    // https://github.com/dotnet/runtime/blob/v7.0.0/src/libraries/System.Private.CoreLib/src/System/Math.cs#L1494
#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1203 // Constants should appear before fields
#pragma warning disable SA1310 // Field names should not contain underscore
#pragma warning disable SA1119 // Statement should not use unnecessary parenthesis
    private const double SCALEB_C1 = 8.98846567431158E+307; // 0x1p1023
    private const double SCALEB_C2 = 2.2250738585072014E-308; // 0x1p-1022
    private const double SCALEB_C3 = 9007199254740992; // 0x1p53

    private static double ScaleB(double x, int n)
    {
        // Implementation based on https://git.musl-libc.org/cgit/musl/tree/src/math/scalbln.c
        //
        // Performs the calculation x * 2^n efficiently. It constructs a double from 2^n by building
        // the correct biased exponent. If n is greater than the maximum exponent (1023) or less than
        // the minimum exponent (-1022), adjust x and n to compute correct result.

        double y = x;
        if (n > 1023)
        {
            y *= SCALEB_C1;
            n -= 1023;
            if (n > 1023)
            {
                y *= SCALEB_C1;
                n -= 1023;
                if (n > 1023)
                {
                    n = 1023;
                }
            }
        }
        else if (n < -1022)
        {
            y *= SCALEB_C2 * SCALEB_C3;
            n += 1022 - 53;
            if (n < -1022)
            {
                y *= SCALEB_C2 * SCALEB_C3;
                n += 1022 - 53;
                if (n < -1022)
                {
                    n = -1022;
                }
            }
        }

        double u = BitConverter.Int64BitsToDouble((long)(0x3ff + n) << 52);
        return y * u;
    }
#pragma warning restore SA1119 // Statement should not use unnecessary parenthesis
#pragma warning restore SA1310 // Field names should not contain underscore
#pragma warning restore SA1203 // Constants should appear before fields
#pragma warning restore SA1201 // Elements should appear in the correct order
#endif
}
