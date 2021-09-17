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


|                    Method | WithSDK |        Mean |     Error |    StdDev |      Median |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |-------- |------------:|----------:|----------:|------------:|-------:|------:|------:|----------:|
|            CounterHotPath |   False |    21.16 ns |  1.807 ns |  5.037 ns |    19.55 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |    32.33 ns |  1.596 ns |  4.501 ns |    30.53 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |    88.70 ns |  3.963 ns | 11.497 ns |    86.24 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False |   122.19 ns |  4.101 ns | 11.898 ns |   120.32 ns | 0.0248 |     - |     - |     104 B |
| CounterWith6LabelsHotPath |   False |   151.19 ns |  5.875 ns | 17.324 ns |   146.80 ns | 0.0286 |     - |     - |     120 B |
| CounterWith7LabelsHotPath |   False |   170.32 ns |  4.907 ns | 14.392 ns |   165.91 ns | 0.0324 |     - |     - |     136 B |
|            CounterHotPath |    True |    52.47 ns |  1.080 ns |  2.481 ns |    51.89 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True |   142.46 ns |  2.118 ns |  1.769 ns |   142.51 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True |   594.14 ns | 11.827 ns | 21.326 ns |   591.94 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True |   843.52 ns | 16.612 ns | 17.775 ns |   835.77 ns | 0.0553 |     - |     - |     232 B |
| CounterWith6LabelsHotPath |    True |   957.71 ns | 18.017 ns | 19.278 ns |   953.99 ns | 0.0629 |     - |     - |     264 B |
| CounterWith7LabelsHotPath |    True | 1,112.38 ns | 21.805 ns | 27.576 ns | 1,104.74 ns | 0.0706 |     - |     - |     296 B |
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
    }
}
