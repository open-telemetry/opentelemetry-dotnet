// <copyright file="Base2ExponentialBucketHistogram.LowerBoundary.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics;

/// <content>
/// This file contains an implementation for LowerBoundary.
/// LowerBoundary returns the lower boundary of a bucket for
/// a Base2ExponentialBucketHistogram.
///
/// The LowerBoundary implementation is intentionally placed
/// in its own file so that components like the Console exporter
/// can include it.
/// </content>
internal sealed partial class Base2ExponentialBucketHistogram
{
    private const double EpsilonTimes2 = double.Epsilon * 2;
    private static readonly double Ln2 = Math.Log(2);

    public static double LowerBoundary(int index, int scale)
    {
        if (scale > 0)
        {
#if NET6_0_OR_GREATER
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

#if NET6_0_OR_GREATER
            return Math.ScaleB(1, n);
#else
            return ScaleB(1, n);
#endif
        }
    }

#if !NET6_0_OR_GREATER
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

        double u = BitConverter.Int64BitsToDouble(((long)(0x3ff + n) << 52));
        return y * u;
    }
#pragma warning restore SA1119 // Statement should not use unnecessary parenthesis
#pragma warning restore SA1310 // Field names should not contain underscore
#pragma warning restore SA1203 // Constants should appear before fields
#pragma warning restore SA1201 // Elements should appear in the correct order
#endif
}
