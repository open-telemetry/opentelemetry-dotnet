// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;

namespace OpenTelemetry.Tests;

[StructLayout(LayoutKind.Explicit)]
public struct IEEE754Double
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
    {
        return value.DoubleValue;
    }

    public static IEEE754Double operator ++(IEEE754Double value)
    {
        value.ULongValue++;
        return value;
    }

    public static IEEE754Double operator --(IEEE754Double value)
    {
        value.ULongValue--;
        return value;
    }

    public static IEEE754Double FromDouble(double value)
    {
        return new IEEE754Double(value);
    }

    public static IEEE754Double FromLong(long value)
    {
        return new IEEE754Double { LongValue = value };
    }

    public static IEEE754Double FromULong(ulong value)
    {
        return new IEEE754Double { ULongValue = value };
    }

    public static IEEE754Double FromString(string value)
    {
        return FromLong(Convert.ToInt64(value.Replace(" ", string.Empty, StringComparison.Ordinal), 2));
    }

    public override string ToString()
    {
        Span<char> chars = stackalloc char[66];

        var bits = this.ULongValue;
        var index = chars.Length - 1;

        for (int i = 0; i < 52; i++)
        {
            chars[index--] = (char)(bits & 0x01 | 0x30);
            bits >>= 1;
        }

        chars[index--] = ' ';

        for (int i = 0; i < 11; i++)
        {
            chars[index--] = (char)(bits & 0x01 | 0x30);
            bits >>= 1;
        }

        chars[index--] = ' ';

        chars[index--] = (char)(bits & 0x01 | 0x30);

        return chars.ToString();
    }
}
