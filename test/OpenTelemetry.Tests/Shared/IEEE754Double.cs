// <copyright file="IEEE754Double.cs" company="OpenTelemetry Authors">
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

using System;
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
        return IEEE754Double.FromLong(Convert.ToInt64(value.Replace(" ", string.Empty), 2));
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
