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

namespace CustomizingTheSdk;

public class Program
{
    private static readonly Meter MyMeter = new("MyCompany.MyProduct.MyLibrary", "1.0");
    private static readonly Histogram<double> MyHistogram = MyMeter.CreateHistogram<double>("MyHistogram");

    public static void Main()
    {
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

        Console.WriteLine(ExponentialBucketHistogram.IEEE754Double.ToString(-0.25));
        Console.WriteLine(ExponentialBucketHistogram.IEEE754Double.ToString(-0.5));
        Console.WriteLine(ExponentialBucketHistogram.IEEE754Double.ToString(-0));
        Console.WriteLine(ExponentialBucketHistogram.IEEE754Double.ToString(-1));
        Console.WriteLine(ExponentialBucketHistogram.IEEE754Double.ToString(-2));
        Console.WriteLine(ExponentialBucketHistogram.IEEE754Double.ToString(-4));

        Console.WriteLine(ExponentialBucketHistogram.IEEE754Double.ToString(0.25));
        Console.WriteLine(ExponentialBucketHistogram.IEEE754Double.ToString(0.5));
        Console.WriteLine(ExponentialBucketHistogram.IEEE754Double.ToString(0));
        Console.WriteLine(ExponentialBucketHistogram.IEEE754Double.ToString(1));
        Console.WriteLine(ExponentialBucketHistogram.IEEE754Double.ToString(2));
        Console.WriteLine(ExponentialBucketHistogram.IEEE754Double.ToString(4));
        Console.WriteLine(ExponentialBucketHistogram.IEEE754Double.ToString(double.PositiveInfinity));
        Console.WriteLine(ExponentialBucketHistogram.IEEE754Double.ToString(double.NegativeInfinity));
        Console.WriteLine(ExponentialBucketHistogram.IEEE754Double.ToString(double.NaN));
    }
}
