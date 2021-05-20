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

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;

/*
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.202
  [Host]     : .NET Core 3.1.13 (CoreCLR 4.700.21.11102, CoreFX 4.700.21.11602), X64 RyuJIT
  DefaultJob : .NET Core 3.1.13 (CoreCLR 4.700.21.11102, CoreFX 4.700.21.11602), X64 RyuJIT

|                    Method | WithSDK |       Mean |      Error |     StdDev |     Median |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |-------- |-----------:|-----------:|-----------:|-----------:|-------:|------:|------:|----------:|
|            CounterHotPath |   False |   3.098 ns |  0.0924 ns |  0.2231 ns |   3.039 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |  12.696 ns |  0.4042 ns |  1.0859 ns |  12.367 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |  33.626 ns |  0.6625 ns |  1.1428 ns |  33.502 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False |  28.656 ns |  0.5640 ns |  0.6035 ns |  28.817 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |    True | 132.766 ns |  2.0410 ns |  1.9092 ns | 132.664 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True | 221.057 ns |  4.1977 ns |  3.9265 ns | 222.972 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True | 594.423 ns | 11.8099 ns | 21.2957 ns | 595.432 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True | 724.727 ns | 14.3369 ns | 14.0807 ns | 725.218 ns | 0.0801 |     - |     - |     336 B |


BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
  [Host]     : .NET Framework 4.8 (4.8.4341.0), X64 RyuJIT
  DefaultJob : .NET Framework 4.8 (4.8.4341.0), X64 RyuJIT


|                    Method | WithSDK |        Mean |     Error |    StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |-------- |------------:|----------:|----------:|-------:|------:|------:|----------:|
|            CounterHotPath |   False |    20.60 ns |  0.341 ns |  0.319 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |    31.10 ns |  0.629 ns |  0.961 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |    52.88 ns |  1.090 ns |  2.322 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False |    50.22 ns |  1.003 ns |  2.202 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |    True |   153.21 ns |  2.963 ns |  2.475 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True |   240.46 ns |  4.353 ns |  3.635 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True |   973.99 ns | 12.978 ns | 10.837 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True | 2,068.10 ns | 39.119 ns | 43.481 ns | 0.0801 |     - |     - |     337 B |
*/

namespace Benchmarks.Metrics
{
    [MemoryDiagnoser]
    public class MetricsBenchmarks
    {
        private readonly KeyValuePair<string, object> tag1 = new KeyValuePair<string, object>("attrib1", "value1");
        private readonly KeyValuePair<string, object> tag2 = new KeyValuePair<string, object>("attrib2", "value2");
        private readonly KeyValuePair<string, object> tag3 = new KeyValuePair<string, object>("attrib3", "value3");
        private readonly KeyValuePair<string, object> tag4 = new KeyValuePair<string, object>("attrib4", "value4");
        private readonly KeyValuePair<string, object> tag5 = new KeyValuePair<string, object>("attrib5", "value5");

        private Counter<int> counter;
        private MeterProvider provider;
        private Meter meter;

        [Params(false, true)]
        public bool WithSDK { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            if (this.WithSDK)
            {
                this.provider = Sdk.CreateMeterProviderBuilder()
                    .AddSource("TestMeter") // All instruments from this meter are enabled.
                    .SetObservationPeriod(10000)
                    .SetCollectionPeriod(10000)

                    // .AddExportProcessor(new MetricConsoleExporter())
                    .Build();
            }

            this.meter = new Meter("TestMeter");
            this.counter = this.meter.CreateCounter<int>("counter");
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
            this.counter?.Add(100, this.tag1);
        }

        [Benchmark]
        public void CounterWith3LabelsHotPath()
        {
            this.counter?.Add(100, this.tag1, this.tag2, this.tag3);
        }

        [Benchmark]
        public void CounterWith5LabelsHotPath()
        {
            this.counter?.Add(100, this.tag1, this.tag2, this.tag3, this.tag4, this.tag5);
        }
    }
}
