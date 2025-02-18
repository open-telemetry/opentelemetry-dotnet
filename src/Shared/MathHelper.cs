// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NET
using System.Numerics;
#endif
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Internal;

internal static class MathHelper
{
    // https://en.wikipedia.org/wiki/Leading_zero
    private static readonly byte[] LeadingZeroLookupTable =
    [
        8, 7, 6, 6, 5, 5, 5, 5, 4, 4, 4, 4, 4, 4, 4, 4,
        3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LeadingZero8(byte value)
    {
        return LeadingZeroLookupTable[value];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LeadingZero16(short value)
    {
        unchecked
        {
            var high8 = (byte)(value >> 8);

            if (high8 != 0)
            {
                return LeadingZero8(high8);
            }

            return LeadingZero8((byte)value) + 8;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LeadingZero32(int value)
    {
        unchecked
        {
            var high16 = (short)(value >> 16);

            if (high16 != 0)
            {
                return LeadingZero16(high16);
            }

            return LeadingZero16((short)value) + 16;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LeadingZero64(long value)
    {
#if NET
        return BitOperations.LeadingZeroCount((ulong)value);
#else
        unchecked
        {
            var high32 = (int)(value >> 32);

            if (high32 != 0)
            {
                return LeadingZero32(high32);
            }

            return LeadingZero32((int)value) + 32;
        }
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PositiveModulo32(int value, int divisor)
    {
        Debug.Assert(divisor > 0, $"{nameof(divisor)} must be a positive integer.");

        value %= divisor;

        if (value < 0)
        {
            value += divisor;
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long PositiveModulo64(long value, long divisor)
    {
        Debug.Assert(divisor > 0, $"{nameof(divisor)} must be a positive integer.");

        value %= divisor;

        if (value < 0)
        {
            value += divisor;
        }

        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFinite(double value)
    {
#if NET
        return double.IsFinite(value);
#else
        return !double.IsInfinity(value) && !double.IsNaN(value);
#endif
    }
}
