// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET

namespace System;

internal static class MathExtensions
{
    private const ulong PositiveInfinityBits = 0x7FF0_0000_0000_0000;
    private const ulong NegativeInfinityBits = 0xFFF0_0000_0000_0000;
    private const ulong PositiveZeroBits = 0x0000_0000_0000_0000;
    private const ulong NegativeZeroBits = 0x8000_0000_0000_0000;

#pragma warning disable SA1310 // Field names should not contain underscore
    private const double SCALEB_C1 = 8.98846567431158E+307; // 0x1p1023
    private const double SCALEB_C2 = 2.2250738585072014E-308; // 0x1p-1022
    private const double SCALEB_C3 = 9007199254740992; // 0x1p53
#pragma warning restore SA1310 // Field names should not contain underscore

    extension(Math)
    {
        // Math.BitIncrement was introduced in .NET Core 3.0.
        // This implementation is based on
        // https://github.com/dotnet/runtime/blob/60629d14374c56f1cb51819049ad1fa529307f8d/src/libraries/System.Private.CoreLib/src/System/Math.cs#L319-L349
        public static double BitIncrement(double x)
        {
            var bits = BitConverter.DoubleToInt64Bits(x);

            if (!double.IsFinite(x))
            {
                // NaN returns NaN
                // -Infinity returns double.MinValue
                // +Infinity returns +Infinity

                return (bits == unchecked((long)NegativeInfinityBits)) ? double.MinValue : x;
            }

            if (bits == unchecked((long)NegativeZeroBits))
            {
                // -0.0 returns double.Epsilon
                return double.Epsilon;
            }

            // Negative values need to be decremented
            // Positive values need to be incremented

            if (double.IsNegative(x))
            {
                bits -= 1;
            }
            else
            {
                bits += 1;
            }

            return BitConverter.Int64BitsToDouble(bits);
        }

        // This implementation is based on
        // https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/Math.cs#L287-L317
        public static double BitDecrement(double x)
        {
            var bits = BitConverter.DoubleToInt64Bits(x);

            if (!double.IsFinite(x))
            {
                // NaN returns NaN
                // -Infinity returns -Infinity
                // +Infinity returns double.MaxValue
                return (bits == unchecked((long)PositiveInfinityBits)) ? double.MaxValue : x;
            }

            if (bits == unchecked((long)PositiveZeroBits))
            {
                // +0.0 returns -double.Epsilon
                return -double.Epsilon;
            }

            // Negative values need to be incremented
            // Positive values need to be decremented

            if (double.IsNegative(x))
            {
                bits += 1;
            }
            else
            {
                bits -= 1;
            }

            return BitConverter.Int64BitsToDouble(bits);
        }

        // Math.ScaleB was introduced in .NET Core 3.0.
        // This implementation is from:
        // https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Private.CoreLib/src/System/Math.cs#L1502-L1542
#pragma warning disable SA1201 // Elements should appear in the correct order
#pragma warning disable SA1203 // Constants should appear before fields
#pragma warning disable SA1119 // Statement should not use unnecessary parenthesis

        public static double ScaleB(double x, int n)
        {
            // Implementation based on https://git.musl-libc.org/cgit/musl/tree/src/math/scalbln.c
            //
            // Performs the calculation x * 2^n efficiently. It constructs a double from 2^n by building
            // the correct biased exponent. If n is greater than the maximum exponent (1023) or less than
            // the minimum exponent (-1022), adjust x and n to compute correct result.

            var y = x;
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

            var u = BitConverter.Int64BitsToDouble((long)(0x3ff + n) << 52);
            return y * u;
        }
#pragma warning restore SA1119 // Statement should not use unnecessary parenthesis
#pragma warning restore SA1203 // Constants should appear before fields
#pragma warning restore SA1201 // Elements should appear in the correct order
    }
}

#endif
