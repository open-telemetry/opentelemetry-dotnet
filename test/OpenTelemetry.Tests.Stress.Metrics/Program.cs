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
using System.Runtime.CompilerServices;
using System.Threading;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Tests.Stress;

public partial class Program
{
    private const int ArraySize = 10;
    private static readonly Meter TestMeter = new Meter(Utils.GetCurrentMethodName());
    private static readonly Counter<long> TestCounter = TestMeter.CreateCounter<long>("TestCounter");
    private static readonly Histogram<double> TestHistogram = TestMeter.CreateHistogram<double>("TestHistogram");
    private static readonly string[] DimensionValues = new string[ArraySize];
    private static readonly ThreadLocal<Random> ThreadLocalRandom = new ThreadLocal<Random>(() => new Random());

    public static void Main()
    {
        for (int i = 0; i < ArraySize; i++)
        {
            DimensionValues[i] = $"DimValue{i}";
        }

        var boundaries = new double[] { 0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160, 170, 180, 190, 200, 210, 220, 230, 240, 250, 260, 270, 280, 290, 300, 310, 320, 330, 340, 350, 360, 370, 380, 390, 400, 410, 420, 430, 440, 450, 460, 470, 480, 490, 500 };
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(TestMeter.Name)
            .AddPrometheusExporter(options =>
            {
                options.StartHttpListener = true;
                options.HttpListenerPrefixes = new string[] { $"http://localhost:9185/" };
                options.ScrapeResponseCacheDurationMilliseconds = 0;
            })
            .AddView(TestHistogram.Name, new ExplicitBucketHistogramConfiguration() { Boundaries = boundaries })
            .Build();

        Stress(prometheusPort: 9184);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void Run()
    {
        var random = ThreadLocalRandom.Value;
        /*TestCounter.Add(
            100,
            new("DimName1", DimensionValues[random.Next(0, ArraySize)]),
            new("DimName2", DimensionValues[random.Next(0, ArraySize)]),
            new("DimName3", DimensionValues[random.Next(0, ArraySize)]));*/
        TestHistogram.Record(random.Next(-100, 600));
    }
}
