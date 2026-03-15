// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace OpenTelemetry.Tests;

[StructLayout(LayoutKind.Explicit)]
internal struct IEEE754Double
{
    [FieldOffset(0)]
    public double DoubleValue = 0;

    [FieldOffset(0)]
    public long LongValue = 0;

    [FieldOffset(0)]
    public ulong ULongValue = 0;

    public IEEE754Double(double value)
    {
        this.DoubleValue = value;
    }

    public static implicit operator double(IEEE754Double value)
        => ToDouble(value);

    public static IEEE754Double operator ++(IEEE754Double value)
        => Increment(value);

    public static IEEE754Double operator --(IEEE754Double value)
        => Decrement(value);

    public static double ToDouble(IEEE754Double value)
        => value.DoubleValue;

    public static IEEE754Double Increment(IEEE754Double value)
    {
        value.ULongValue++;
        return value;
    }

    public static IEEE754Double Decrement(IEEE754Double value)
    {
        value.ULongValue--;
        return value;
    }

    public static IEEE754Double FromDouble(double value)
        => new(value);

    public static IEEE754Double FromLong(long value)
        => new() { LongValue = value };

    public static IEEE754Double FromULong(ulong value)
        => new() { ULongValue = value };

#pragma warning disable IDE0022 // Use expression body for method
    public static IEEE754Double FromString(string value)
    {
#if NET
        return FromLong(Convert.ToInt64(value.Replace(" ", string.Empty, StringComparison.Ordinal), 2));
#else
        return FromLong(Convert.ToInt64(value.Replace(" ", string.Empty), 2));
#endif
    }
#pragma warning restore IDE0022 // Use expression body for method

    public override readonly string ToString()
    {
        Span<char> chars = stackalloc char[66];

        var bits = this.ULongValue;
        var index = chars.Length - 1;

        for (var i = 0; i < 52; i++)
        {
            chars[index--] = (char)((bits & 0x01) | 0x30);
            bits >>= 1;
        }

        chars[index--] = ' ';

        for (var i = 0; i < 11; i++)
        {
            chars[index--] = (char)((bits & 0x01) | 0x30);
            bits >>= 1;
        }

        chars[index--] = ' ';

        chars[index--] = (char)((bits & 0x01) | 0x30);

        return chars.ToString();
    }
}
