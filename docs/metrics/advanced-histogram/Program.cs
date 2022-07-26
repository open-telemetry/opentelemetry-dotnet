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
    private static readonly Meter MyMeter = new("MyCompany.MyProduct.MyLibrary", "1.0");
    private static readonly Histogram<double> MyHistogram = MyMeter.CreateHistogram<double>("MyHistogram");

    public static void Main()
    {
        /*
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(MyMeter.Name)
            .AddView(instrumentName: "MyHistogram", new ExponentialBucketHistogramConfiguration() { MaxSize = 80 })
            .AddConsoleExporter()
            .Build();

        var random = new Random();

        for (int i = 0; i < 20000; i++)
        {
            MyHistogram.Record(random.Next(1, 1000), new("tag1", "value1"), new("tag2", "value2"));
        }
        */

        Console.WriteLine(IEEE754Double.FromDouble(double.NaN)); // NaN (language/runtime native)
        Console.WriteLine(IEEE754Double.FromString("0 11111111111 0000000000000000000000000000000000000000000000000001")); // sNaN on x86/64 and ARM
        Console.WriteLine(IEEE754Double.FromString("0 11111111111 1000000000000000000000000000000000000000000000000001")); // qNaN on x86/64 and ARM
        Console.WriteLine(IEEE754Double.FromString("0 11111111111 1111111111111111111111111111111111111111111111111111")); // NaN (alternative encoding)
        Console.WriteLine(IEEE754Double.FromString("1 11111111111 0000000000000000000000000000000000000000000000000000")); // negative infinity
        Console.WriteLine(IEEE754Double.FromString("1 11111111110 1111111111111111111111111111111111111111111111111111")); // smallest normal
        Console.WriteLine(IEEE754Double.FromString("1 10000000001 0000000000000000000000000000000000000000000000000000")); // -4
        Console.WriteLine(IEEE754Double.FromString("1 10000000000 0000000000000000000000000000000000000000000000000000")); // -2
        Console.WriteLine(IEEE754Double.FromString("1 01111111111 0000000000000000000000000000000000000000000000000000")); // -1
        Console.WriteLine(IEEE754Double.FromString("1 01111111110 0000000000000000000000000000000000000000000000000000")); // -0.5
        Console.WriteLine(IEEE754Double.FromString("1 01111111101 0000000000000000000000000000000000000000000000000000")); // -0.25
        Console.WriteLine(IEEE754Double.FromString("1 00000000001 0000000000000000000000000000000000000000000000000000")); // biggest negative normal
        Console.WriteLine(IEEE754Double.FromString("1 00000000000 1111111111111111111111111111111111111111111111111111")); // smallest subnormal
        Console.WriteLine(IEEE754Double.FromString("1 00000000000 0000000000000000000000000000000000000000000000000101")); // -5 * epsilon
        Console.WriteLine(IEEE754Double.FromString("1 00000000000 0000000000000000000000000000000000000000000000000100")); // -4 * epsilon
        Console.WriteLine(IEEE754Double.FromString("1 00000000000 0000000000000000000000000000000000000000000000000011")); // -3 * epsilon
        Console.WriteLine(IEEE754Double.FromString("1 00000000000 0000000000000000000000000000000000000000000000000010")); // -2 * epsilon
        Console.WriteLine(IEEE754Double.FromString("1 00000000000 0000000000000000000000000000000000000000000000000001")); // biggest negative subnormal (-epsilon)
        Console.WriteLine(IEEE754Double.FromString("1 00000000000 0000000000000000000000000000000000000000000000000000")); // -0
        Console.WriteLine(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000000")); // +0
        Console.WriteLine(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000001")); // smallest positive subnormal (epsilon)
        Console.WriteLine(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000010")); // 2 * epsilon
        Console.WriteLine(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000011")); // 3 * epsilon
        Console.WriteLine(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000100")); // 4 * epsilon
        Console.WriteLine(IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000101")); // 5 * epsilon
        Console.WriteLine(IEEE754Double.FromString("0 00000000000 1111111111111111111111111111111111111111111111111111")); // biggest subnormal
        Console.WriteLine(IEEE754Double.FromString("0 00000000001 0000000000000000000000000000000000000000000000000000")); // smallest positive normal
        Console.WriteLine(IEEE754Double.FromString("0 01111111101 0000000000000000000000000000000000000000000000000000")); // 0.25
        Console.WriteLine(IEEE754Double.FromString("0 01111111110 0000000000000000000000000000000000000000000000000000")); // 0.5
        Console.WriteLine(IEEE754Double.FromString("0 01111111111 0000000000000000000000000000000000000000000000000000")); // 1
        Console.WriteLine(IEEE754Double.FromString("0 10000000000 0000000000000000000000000000000000000000000000000000")); // 2
        Console.WriteLine(IEEE754Double.FromString("0 10000000001 0000000000000000000000000000000000000000000000000000")); // 4
        Console.WriteLine(IEEE754Double.FromString("0 11111111110 1111111111111111111111111111111111111111111111111111")); // biggest normal
        Console.WriteLine(IEEE754Double.FromString("0 11111111111 0000000000000000000000000000000000000000000000000000")); // positive infinity

        Console.WriteLine();
        var value = IEEE754Double.FromString("1000000000000000000000000000000000000000000000000000000000000000");
        for (int i = 0; i < 10; i++)
        {
            Console.WriteLine(value);
            value++;
        }

        Console.WriteLine(new IEEE754Double(Math.Log2(Math.E)));
        Console.WriteLine(new IEEE754Double(1 / Math.Log(2)));

        Console.WriteLine();

        var array = new IEEE754Double[]
        {
            IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000001"), // POWER(2, -1074) smallest positive subnormal (epsilon)
            IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000011"), // 3 * epsilon
            IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000111"), // 5 * epsilon
            IEEE754Double.FromString("0 00000000000 1000000000000000000000000000000000000000000000000000"), // POWER(2, -1023)
            IEEE754Double.FromString("0 00000000000 1111111111111111111111111111111111111111111111111111"), // biggest subnormal
            IEEE754Double.FromString("0 00000000001 0000000000000000000000000000000000000000000000000000"), // POWER(2, -1022) smallest positive normal
            IEEE754Double.FromString("0 01111111000 0000000000000000000000000000000000000000000000000000"), // 1/128
            IEEE754Double.FromString("0 01111111001 0000000000000000000000000000000000000000000000000000"), // 1/64
            IEEE754Double.FromString("0 01111111010 0000000000000000000000000000000000000000000000000000"), // 1/32
            IEEE754Double.FromString("0 01111111011 0000000000000000000000000000000000000000000000000000"), // 1/16
            IEEE754Double.FromString("0 01111111100 0000000000000000000000000000000000000000000000000000"), // 1/8
            IEEE754Double.FromString("0 01111111101 0000000000000000000000000000000000000000000000000000"), // 1/4
            IEEE754Double.FromString("0 01111111110 0000000000000000000000000000000000000000000000000000"), // 1/2
            IEEE754Double.FromString("0 01111111111 0000000000000000000000000000000000000000000000000000"), // 1
            IEEE754Double.FromString("0 10000000000 0000000000000000000000000000000000000000000000000000"), // 2
            IEEE754Double.FromString("0 10000000001 0000000000000000000000000000000000000000000000000000"), // 4
            IEEE754Double.FromString("0 10000000010 0000000000000000000000000000000000000000000000000000"), // 8
            IEEE754Double.FromString("0 10000000011 0000000000000000000000000000000000000000000000000000"), // 16
            IEEE754Double.FromString("0 10000000100 0000000000000000000000000000000000000000000000000000"), // 32
            IEEE754Double.FromString("0 10000000101 0000000000000000000000000000000000000000000000000000"), // 64
            IEEE754Double.FromString("0 10000000110 0000000000000000000000000000000000000000000000000000"), // 128
            IEEE754Double.FromString("0 10000000111 0000000000000000000000000000000000000000000000000000"), // 256
            IEEE754Double.FromString("0 11111111110 0000000000000000000000000000000000000000000000000000"), // POWER(2, 1022)
            IEEE754Double.FromString("0 11111111110 1111111111111111111111111111111111111111111111111110"), // --biggest normal
        };

        // 1 should always map to -1 regardless of the scale, because bucket[-1] = (1/base, 1] and bucket[0] = (1, base]

        Console.WriteLine("Scale 0:");

        for (int i = 0; i < array.Length; i++)
        {
            value = array[i];
            TestMapToIndex(0, value);
            TestMapToIndex(0, ++value);
        }

        Console.WriteLine("Scale -2:");

        for (int i = 0; i < array.Length; i++)
        {
            value = array[i];
            TestMapToIndex(-2, value);
            TestMapToIndex(-2, ++value);
        }

        Console.WriteLine("Scale -1:");

        for (int i = 0; i < array.Length; i++)
        {
            value = array[i];
            TestMapToIndex(-1, value);
            TestMapToIndex(-1, ++value);
        }

        Console.WriteLine("Scale 1:");

        for (int i = 0; i < array.Length; i++)
        {
            value = array[i];
            TestMapToIndex(1, value);
            TestMapToIndex(1, ++value);
        }

        Console.WriteLine("Scale 2:");

        for (int i = 0; i < array.Length; i++)
        {
            value = array[i];
            TestMapToIndex(2, value);
            TestMapToIndex(2, ++value);
        }

        /*
        for (var scale = -11; scale <= 20; scale++)
        {
            var histogram = new ExponentialBucketHistogram(scale);
            Console.WriteLine($"Scale {scale}: index in [{histogram.MapToIndex(double.Epsilon)}, {histogram.MapToIndex(double.MaxValue)}]");
        }
        */

        static void TestMapToIndex(int scale, IEEE754Double value)
        {
            var histogram = new ExponentialBucketHistogram(scale);
            var index = histogram.MapToIndex(value.DoubleValue);
            Console.WriteLine($"{value} => {index}");
        }
    }
}
