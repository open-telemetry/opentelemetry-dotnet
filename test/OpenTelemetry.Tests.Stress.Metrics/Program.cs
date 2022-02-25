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
    private const int MaxHistogramMeasurement = 1000;
    private static readonly Meter TestMeter = new Meter(Utils.GetCurrentMethodName());
    private static readonly Counter<long> TestCounter = TestMeter.CreateCounter<long>("TestCounter");
    private static readonly Histogram<long> TestHistogram = TestMeter.CreateHistogram<long>("TestHistogram");
    private static readonly string[] DimensionValues = new string[ArraySize];
    private static readonly ThreadLocal<Random> ThreadLocalRandom = new ThreadLocal<Random>(() => new Random());

    public static void Main(string[] args)
    {
        for (int i = 0; i < ArraySize; i++)
        {
            DimensionValues[i] = $"DimValue{i}";
        }

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(TestMeter.Name)
            .AddPrometheusExporter(options =>
            {
                options.StartHttpListener = true;
                options.HttpListenerPrefixes = new string[] { $"http://localhost:9185/" };
                options.ScrapeResponseCacheDurationMilliseconds = 0;
            })
            .Build();

        Action run = RunCounter;
        if (args.Length > 0)
        {
            var command = args[0];
            run = command.ToLower() switch
            {
                "counter" => RunCounter,
                "histogram" => RunHistogram,
                _ => RunCounter,
            };
        }

        Stress(run, prometheusPort: 9184);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void RunCounter()
    {
        var random = ThreadLocalRandom.Value;
        TestCounter.Add(
            100,
            new("DimName1", DimensionValues[random.Next(0, ArraySize)]),
            new("DimName2", DimensionValues[random.Next(0, ArraySize)]),
            new("DimName3", DimensionValues[random.Next(0, ArraySize)]));
    }

    protected static void RunHistogram()
    {
        var random = ThreadLocalRandom.Value;
        TestHistogram.Record(
            random.Next(MaxHistogramMeasurement),
            new("DimName1", DimensionValues[random.Next(0, ArraySize)]),
            new("DimName2", DimensionValues[random.Next(0, ArraySize)]),
            new("DimName3", DimensionValues[random.Next(0, ArraySize)]));
    }
}
