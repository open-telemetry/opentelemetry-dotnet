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
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;

/*
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19043
Intel Core i7-8650U CPU 1.90GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.302
  [Host]     : .NET Core 3.1.17 (CoreCLR 4.700.21.31506, CoreFX 4.700.21.31502), X64 RyuJIT
  DefaultJob : .NET Core 3.1.17 (CoreCLR 4.700.21.31506, CoreFX 4.700.21.31502), X64 RyuJIT


|                    Method | WithSDK |      Mean |     Error |     StdDev |    Median |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |-------- |----------:|----------:|-----------:|----------:|-------:|------:|------:|----------:|
|            CounterHotPath |   False |  18.48 ns |  0.366 ns |   0.570 ns |  18.52 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |  30.25 ns |  1.274 ns |   3.530 ns |  29.14 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |  82.93 ns |  2.586 ns |   7.124 ns |  81.79 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False | 134.94 ns |  4.756 ns |  13.491 ns | 132.45 ns | 0.0248 |     - |     - |     104 B |
|            CounterHotPath |    True |  68.58 ns |  1.417 ns |   3.228 ns |  68.40 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True | 192.19 ns |  8.114 ns |  23.151 ns | 184.06 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True | 799.33 ns | 47.442 ns | 136.882 ns | 757.73 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True | 972.16 ns | 45.809 ns | 133.626 ns | 939.95 ns | 0.0553 |     - |     - |     232 B |
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
            if (this.WithSDK)
            {
                this.provider = Sdk.CreateMeterProviderBuilder()
                    .AddSource("TestMeter") // All instruments from this meter are enabled.
                    .Build();
            }

            this.meter = new Meter("TestMeter");
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
            var tag5 = new KeyValuePair<string, object>("DimName4", this.dimensionValues[this.random.Next(0, 10)]);
            this.counter?.Add(100, tag1, tag2, tag3, tag4, tag5);
        }
    }
}
