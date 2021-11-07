// <copyright file="MetricsBenchmarks.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19043
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET Core SDK=6.0.100-rc.1.21458.32
  [Host]     : .NET Core 3.1.19 (CoreCLR 4.700.21.41101, CoreFX 4.700.21.41603), X64 RyuJIT
  DefaultJob : .NET Core 3.1.19 (CoreCLR 4.700.21.41101, CoreFX 4.700.21.41603), X64 RyuJIT


|                                Method | WithSDK |      Mean |    Error |   StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------------------- |-------- |----------:|---------:|---------:|-------:|------:|------:|----------:|
|                        CounterHotPath |   False |  12.87 ns | 0.086 ns | 0.080 ns |      - |     - |     - |         - |
|             CounterWith1LabelsHotPath |   False |  19.36 ns | 0.083 ns | 0.069 ns |      - |     - |     - |         - |
|             CounterWith3LabelsHotPath |   False |  53.71 ns | 0.336 ns | 0.314 ns |      - |     - |     - |         - |
|             CounterWith5LabelsHotPath |   False |  79.82 ns | 0.525 ns | 0.439 ns | 0.0166 |     - |     - |     104 B |
|             CounterWith6LabelsHotPath |   False |  91.35 ns | 0.827 ns | 0.733 ns | 0.0191 |     - |     - |     120 B |
|             CounterWith7LabelsHotPath |   False | 104.40 ns | 0.924 ns | 0.865 ns | 0.0216 |     - |     - |     136 B |
| CounterWith1LabelsHotPathUsingTagList |   False |  50.43 ns | 0.415 ns | 0.388 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPathUsingTagList |   False |  88.48 ns | 0.402 ns | 0.376 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPathUsingTagList |   False | 121.61 ns | 0.472 ns | 0.418 ns |      - |     - |     - |         - |
| CounterWith6LabelsHotPathUsingTagList |   False | 139.11 ns | 0.593 ns | 0.554 ns |      - |     - |     - |         - |
| CounterWith7LabelsHotPathUsingTagList |   False | 154.07 ns | 0.773 ns | 0.685 ns |      - |     - |     - |         - |
|                        CounterHotPath |    True |  38.23 ns | 0.235 ns | 0.220 ns |      - |     - |     - |         - |
|             CounterWith1LabelsHotPath |    True | 102.48 ns | 0.942 ns | 0.881 ns |      - |     - |     - |         - |
|             CounterWith3LabelsHotPath |    True | 417.00 ns | 2.904 ns | 2.716 ns |      - |     - |     - |         - |
|             CounterWith5LabelsHotPath |    True | 578.45 ns | 5.287 ns | 4.946 ns | 0.0162 |     - |     - |     104 B |
|             CounterWith6LabelsHotPath |    True | 665.56 ns | 3.716 ns | 3.476 ns | 0.0191 |     - |     - |     120 B |
|             CounterWith7LabelsHotPath |    True | 778.88 ns | 5.482 ns | 4.578 ns | 0.0210 |     - |     - |     136 B |
| CounterWith1LabelsHotPathUsingTagList |    True | 135.55 ns | 1.012 ns | 0.947 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPathUsingTagList |    True | 457.96 ns | 4.242 ns | 3.968 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPathUsingTagList |    True | 631.81 ns | 3.423 ns | 2.858 ns |      - |     - |     - |         - |
| CounterWith6LabelsHotPathUsingTagList |    True | 719.81 ns | 4.704 ns | 4.400 ns |      - |     - |     - |         - |
| CounterWith7LabelsHotPathUsingTagList |    True | 828.88 ns | 4.321 ns | 3.830 ns |      - |     - |     - |         - |
*/

namespace Benchmarks.Metrics
{
    [MemoryDiagnoser]
    public class MetricsBenchmarks
    {
        private Counter<long> counter;
        private MeterProvider provider;
        private Meter meter;
        private Random random = new Random();
        private string[] dimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };

        [Params(false, true)]
        public bool WithSDK { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.meter = new Meter(Utils.GetCurrentMethodName());

            if (this.WithSDK)
            {
                this.provider = Sdk.CreateMeterProviderBuilder()
                    .AddMeter(this.meter.Name) // All instruments from this meter are enabled.
                    .Build();
            }

            this.counter = this.meter.CreateCounter<long>("counter");
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.meter?.Dispose();
            this.provider?.Dispose();
        }

        [Benchmark]
        public void CounterHotPath()
        {
            this.counter?.Add(100);
        }

        [Benchmark]
        public void CounterWith1LabelsHotPath()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[this.random.Next(0, 2)]);
            this.counter?.Add(100, tag1);
        }

        [Benchmark]
        public void CounterWith3LabelsHotPath()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[this.random.Next(0, 10)]);
            var tag2 = new KeyValuePair<string, object>("DimName2", this.dimensionValues[this.random.Next(0, 10)]);
            var tag3 = new KeyValuePair<string, object>("DimName3", this.dimensionValues[this.random.Next(0, 10)]);
            this.counter?.Add(100, tag1, tag2, tag3);
        }

        [Benchmark]
        public void CounterWith5LabelsHotPath()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[this.random.Next(0, 2)]);
            var tag2 = new KeyValuePair<string, object>("DimName2", this.dimensionValues[this.random.Next(0, 2)]);
            var tag3 = new KeyValuePair<string, object>("DimName3", this.dimensionValues[this.random.Next(0, 5)]);
            var tag4 = new KeyValuePair<string, object>("DimName4", this.dimensionValues[this.random.Next(0, 5)]);
            var tag5 = new KeyValuePair<string, object>("DimName5", this.dimensionValues[this.random.Next(0, 10)]);
            this.counter?.Add(100, tag1, tag2, tag3, tag4, tag5);
        }

        [Benchmark]
        public void CounterWith6LabelsHotPath()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[this.random.Next(0, 2)]);
            var tag2 = new KeyValuePair<string, object>("DimName2", this.dimensionValues[this.random.Next(0, 2)]);
            var tag3 = new KeyValuePair<string, object>("DimName3", this.dimensionValues[this.random.Next(0, 5)]);
            var tag4 = new KeyValuePair<string, object>("DimName4", this.dimensionValues[this.random.Next(0, 5)]);
            var tag5 = new KeyValuePair<string, object>("DimName5", this.dimensionValues[this.random.Next(0, 5)]);
            var tag6 = new KeyValuePair<string, object>("DimName6", this.dimensionValues[this.random.Next(0, 2)]);
            this.counter?.Add(100, tag1, tag2, tag3, tag4, tag5, tag6);
        }

        [Benchmark]
        public void CounterWith7LabelsHotPath()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[this.random.Next(0, 2)]);
            var tag2 = new KeyValuePair<string, object>("DimName2", this.dimensionValues[this.random.Next(0, 2)]);
            var tag3 = new KeyValuePair<string, object>("DimName3", this.dimensionValues[this.random.Next(0, 5)]);
            var tag4 = new KeyValuePair<string, object>("DimName4", this.dimensionValues[this.random.Next(0, 5)]);
            var tag5 = new KeyValuePair<string, object>("DimName5", this.dimensionValues[this.random.Next(0, 5)]);
            var tag6 = new KeyValuePair<string, object>("DimName6", this.dimensionValues[this.random.Next(0, 2)]);
            var tag7 = new KeyValuePair<string, object>("DimName7", this.dimensionValues[this.random.Next(0, 1)]);
            this.counter?.Add(100, tag1, tag2, tag3, tag4, tag5, tag6, tag7);
        }

        [Benchmark]
        public void CounterWith1LabelsHotPathUsingTagList()
        {
            var tags = new TagList
            {
                { "DimName1", this.dimensionValues[this.random.Next(0, 2)] },
            };
            this.counter?.Add(100, tags);
        }

        [Benchmark]
        public void CounterWith3LabelsHotPathUsingTagList()
        {
            var tags = new TagList
            {
                { "DimName1", this.dimensionValues[this.random.Next(0, 10)] },
                { "DimName2", this.dimensionValues[this.random.Next(0, 10)] },
                { "DimName3", this.dimensionValues[this.random.Next(0, 10)] },
            };
            this.counter?.Add(100, tags);
        }

        [Benchmark]
        public void CounterWith5LabelsHotPathUsingTagList()
        {
            var tags = new TagList
            {
                { "DimName1", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName2", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName3", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName4", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName5", this.dimensionValues[this.random.Next(0, 10)] },
            };
            this.counter?.Add(100, tags);
        }

        [Benchmark]
        public void CounterWith6LabelsHotPathUsingTagList()
        {
            var tags = new TagList
            {
                { "DimName1", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName2", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName3", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName4", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName5", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName6", this.dimensionValues[this.random.Next(0, 2)] },
            };
            this.counter?.Add(100, tags);
        }

        [Benchmark]
        public void CounterWith7LabelsHotPathUsingTagList()
        {
            var tags = new TagList
            {
                { "DimName1", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName2", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName3", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName4", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName5", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName6", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName7", this.dimensionValues[this.random.Next(0, 1)] },
            };
            this.counter?.Add(100, tags);
        }
    }
}
