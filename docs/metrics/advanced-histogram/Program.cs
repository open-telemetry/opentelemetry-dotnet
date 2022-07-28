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

        Console.WriteLine();

        var array = new IEEE754Double[]
        {
            IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000001"), // POWER(2, -1074) smallest positive subnormal (epsilon)
            IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000011"), // 3 * epsilon
            IEEE754Double.FromString("0 00000000000 0000000000000000000000000000000000000000000000000111"), // 5 * epsilon
            IEEE754Double.FromString("0 00000000000 0100000000000000000000000000000000000000000000000000"), // POWER(2, -1024)
            IEEE754Double.FromString("0 00000000000 0100000000000000000000000000000000000000000000000001"),
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

        for (int scale = 0; scale <= 2; scale++)
        {
            Console.WriteLine($"Scale {scale}:");

            for (int i = 0; i < array.Length; i++)
            {
                var value = array[i];
                TestMapToIndex(scale, value);
                TestMapToIndex(scale, ++value);
            }
        }

        for (var scale = 0; scale <= 20; scale++)
        {
            var histogram = new ExponentialBucketHistogram(2, scale);
            Console.WriteLine($"Scale = {scale}");
            Console.WriteLine($"Base = 2 ^ (2 ^ {-scale}) = {IEEE754Double.FromDouble(Math.Pow(2, Math.Pow(2, -scale)))}");
            Console.WriteLine($"Index in [{histogram.MapToIndex(double.Epsilon)}, {histogram.MapToIndex(double.MaxValue)}]");
            Console.WriteLine($"Bucket[index] = (2 ^ index/{2 << scale}, 2 ^ (index+1)/{2 << scale}]");
            Console.WriteLine();
        }

        TestAutoScale();

        static void TestMapToIndex(int scale, IEEE754Double value)
        {
            var histogram = new ExponentialBucketHistogram(2, scale);
            var index = histogram.MapToIndex(value.DoubleValue);
            Console.WriteLine($"{value} => {index}");
        }

        static void TestAutoScale()
        {
            var histogram = new ExponentialBucketHistogram(2, 0);
            histogram.Record(0);
            histogram.Record(1);
            histogram.Record(2);
            histogram.Record(4);
            histogram.Record(8);
            histogram.Record(-1);
            histogram.Dump();

            histogram.Record(16);
            histogram.Record(32);
            histogram.Dump();

            histogram.Record(double.Epsilon);
            histogram.Dump();

            histogram.Record(double.MaxValue);
            histogram.Dump();
        }
    }
}
