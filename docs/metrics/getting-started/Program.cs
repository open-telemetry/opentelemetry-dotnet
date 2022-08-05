// <copyright file="Program.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

public class Program
{
    public static void Main()
    {
        Console.WriteLine($"{new IEEE754Double(Math.Log2(Math.E))}");
        Console.WriteLine(new IEEE754Double(1 / Math.Log(2)));

        Console.WriteLine();

        var array = new IEEE754Double[]
        {
            IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000001"), // ~4.9406564584124654E-324 (minimum subnormal positive, double.Epsilon, 2 ^ -1074)
            IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000010"), // double.Epsilon * 2
            IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000011"), // double.Epsilon * 3
            IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000100"), // double.Epsilon * 4
            IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000101"), // double.Epsilon * 5
            IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000110"), // double.Epsilon * 6
            IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000111"), // double.Epsilon * 7
            IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000001000"), // double.Epsilon * 8
            IEEE754Double.FromString("0 00000000000 0100000000000000000000000000000000000000000000000000"), // ~5.562684646268003E-309 (2 ^ -1024)
            IEEE754Double.FromString("0 00000000000 0100000000000000000000000000000000000000000000000001"), // ~5.56268464626801E-309
            IEEE754Double.FromString("0 00000000000 1000000000000000000000000000000000000000000000000000"), // ~1.1125369292536007E-308 (2 ^ -1023)
            IEEE754Double.FromString("0 00000000000 1000000000000000000000000000000000000000000000000001"), // ~1.112536929253601E-308
            IEEE754Double.FromString("0 00000000000 1111111111111111111111111111111111111111111111111111"), // ~2.2250738585072009E-308 (maximum subnormal positive)
            IEEE754Double.FromString("0 00000000001 0000000000000000000000000000000000000000000000000000"), // ~2.2250738585072014E-308 (minimum normal positive, 2 ^ -1022)
            IEEE754Double.FromString("0 00000000001 0000000000000000000000000000000000000000000000000001"), // ~2.225073858507202E-308
            IEEE754Double.FromString("0 01111111000 0000000000000000000000000000000000000000000000000000"), // 1/128
            IEEE754Double.FromString("0 01111111001 0000000000000000000000000000000000000000000000000000"), // 1/64
            IEEE754Double.FromString("0 01111111010 0000000000000000000000000000000000000000000000000000"), // 1/32
            IEEE754Double.FromString("0 01111111011 0000000000000000000000000000000000000000000000000000"), // 1/16
            IEEE754Double.FromString("0 01111111100 0000000000000000000000000000000000000000000000000000"), // 1/8
            IEEE754Double.FromString("0 01111111101 0000000000000000000000000000000000000000000000000000"), // 1/4
            IEEE754Double.FromString("0 01111111110 0000000000000000000000000000000000000000000000000000"), // 1/2
            IEEE754Double.FromString("0 01111111110 0000000000000000000000000000000000000000000000000001"), // ~0.5000000000000001
            IEEE754Double.FromString("0 01111111111 0000000000000000000000000000000000000000000000000000"), // 1
            IEEE754Double.FromString("0 01111111111 0000000000000000000000000000000000000000000000000001"), // ~1.0000000000000002
            IEEE754Double.FromString("0 10000000000 0000000000000000000000000000000000000000000000000000"), // 2
            IEEE754Double.FromString("0 10000000001 0000000000000000000000000000000000000000000000000000"), // 4
            IEEE754Double.FromString("0 10000000010 0000000000000000000000000000000000000000000000000000"), // 8
            IEEE754Double.FromString("0 10000000011 0000000000000000000000000000000000000000000000000000"), // 16
            IEEE754Double.FromString("0 10000000100 0000000000000000000000000000000000000000000000000000"), // 32
            IEEE754Double.FromString("0 10000000101 0000000000000000000000000000000000000000000000000000"), // 64
            IEEE754Double.FromString("0 10000000110 0000000000000000000000000000000000000000000000000000"), // 128
            IEEE754Double.FromString("0 11111111110 0000000000000000000000000000000000000000000000000000"), // ~8.98846567431158E+307
            IEEE754Double.FromString("0 11111111110 0000000000000000000000000000000000000000000000000001"), // ~8.988465674311582E+307
            IEEE754Double.FromString("0 11111111110 1111111111111111111111111111111111111111111111111110"), // ~1.7976931348623155E+308
            IEEE754Double.FromString("0 11111111110 1111111111111111111111111111111111111111111111111111"), // ~1.7976931348623157E+308 (maximum normal positive, double.MaxValue, 2 ^ 1023 - 1)
        };

        // 1 should always map to -1 regardless of the scale, because bucket[-1] = (1/base, 1] and bucket[0] = (1, base]

        Console.WriteLine("Scale 0:");

        for (int i = 0; i < array.Length; i++)
        {
            TestMapToIndex(0, array[i]);
        }

        Console.WriteLine("Scale 1:");

        for (int i = 0; i < array.Length; i++)
        {
            TestMapToIndex(1, array[i]);
        }

        Console.WriteLine("Scale 2:");

        for (int i = 0; i < array.Length; i++)
        {
            TestMapToIndex(2, array[i]);
        }

        for (var scale = -11; scale <= 20; scale++)
        {
            var histogram = new ExponentialBucketHistogram(2, scale);
            var minIndex = histogram.MapToIndex(double.Epsilon);
            var maxIndex = histogram.MapToIndex(double.MaxValue);
            var bas = Math.Pow(2, Math.Pow(2, -scale));
            Console.WriteLine($"Scale {scale} (base={bas}): index in [{minIndex}, {maxIndex}]");

            for (var index = minIndex; index <= maxIndex; index++)
            {
                if (index > minIndex + 1 && index < -1)
                {
                    continue;
                }

                if (index > 1 && index < maxIndex - 1)
                {
                    continue;
                }

                var left = Math.Pow(2, index * Math.Pow(2, -scale));
                var right = Math.Pow(2, (index + 1) * Math.Pow(2, -scale));

                var output = $"    * {index}: ";
                output += left == double.Epsilon ? $"(0" : $"({left}";
                output += double.IsFinite(right) ? $", {right}]" : $", {right})";
                Console.WriteLine(output);
            }
        }

        static void TestMapToIndex(int scale, IEEE754Double value)
        {
            var histogram = new ExponentialBucketHistogram(2, scale);
            var index = histogram.MapToIndex(value.DoubleValue);
            Console.WriteLine($"{value} => {index}");
        }
    }
}
