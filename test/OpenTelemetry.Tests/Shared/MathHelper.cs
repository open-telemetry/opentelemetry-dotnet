// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable SA1119 // Statement should not use unnecessary parenthesis

namespace OpenTelemetry.Tests;

internal static class MathHelper
{
    // Math.BitIncrement was introduced in .NET Core 3.0.
    // This is the implementation from:
    // https://github.com/dotnet/runtime/blob/v7.0.0/src/libraries/System.Private.CoreLib/src/System/Math.cs#L259
    public static double BitIncrement(double x)
    {
#if NET6_0_OR_GREATER
        return Math.BitIncrement(x);
#else
        long bits = BitConverter.DoubleToInt64Bits(x);

        if (((bits >> 32) & 0x7FF00000) >= 0x7FF00000)
        {
            // NaN returns NaN
            // -Infinity returns double.MinValue
            // +Infinity returns +Infinity

            return (bits == unchecked((long)(0xFFF00000_00000000))) ? double.MinValue : x;
        }

        if (bits == unchecked((long)(0x80000000_00000000)))
        {
            // -0.0 returns double.Epsilon
            return double.Epsilon;
        }

        // Negative values need to be decremented
        // Positive values need to be incremented

        bits += ((bits < 0) ? -1 : +1);
        return BitConverter.Int64BitsToDouble(bits);
#endif
    }

    public static double BitDecrement(double x)
    {
#if NET6_0_OR_GREATER
        return Math.BitDecrement(x);
#else
        long bits = BitConverter.DoubleToInt64Bits(x);

        if (((bits >> 32) & 0x7FF00000) >= 0x7FF00000)
        {
            // NaN returns NaN
            // -Infinity returns -Infinity
            // +Infinity returns double.MaxValue
            return (bits == 0x7FF00000_00000000) ? double.MaxValue : x;
        }

        if (bits == 0x00000000_00000000)
        {
            // +0.0 returns -double.Epsilon
            return -double.Epsilon;
        }

        // Negative values need to be incremented
        // Positive values need to be decremented

        bits += ((bits < 0) ? +1 : -1);
        return BitConverter.Int64BitsToDouble(bits);
#endif
    }
}
