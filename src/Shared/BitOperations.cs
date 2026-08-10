// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if !NET
using System.Runtime.CompilerServices;

namespace System.Numerics;

internal static class BitOperations
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
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int LeadingZeroCount(ulong value)
    {
        unchecked
        {
            var high32 = (int)(value >> 32);

            if (high32 != 0)
            {
                return LeadingZero32(high32);
            }

            return LeadingZero32((int)value) + 32;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int LeadingZero8(byte value)
        => LeadingZeroLookupTable[value];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int LeadingZero16(short value)
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
    internal static int LeadingZero32(int value)
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
}

#endif
