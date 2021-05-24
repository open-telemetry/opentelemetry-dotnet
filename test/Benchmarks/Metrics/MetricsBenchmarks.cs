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

|                    Method | WithSDK |         Mean |      Error |     StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |-------- |-------------:|-----------:|-----------:|-------:|------:|------:|----------:|
|            CounterHotPath |   False |     4.930 ns |  0.1334 ns |  0.3443 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |    20.730 ns |  0.4458 ns |  0.5475 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |    43.788 ns |  0.7995 ns |  0.7479 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False |    37.191 ns |  0.3754 ns |  0.3135 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |    True |   209.193 ns |  2.5954 ns |  2.1673 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True |   354.471 ns |  7.0792 ns | 11.0214 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True |   914.992 ns | 18.2393 ns | 46.4248 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True | 1,167.378 ns | 23.1794 ns | 24.8017 ns | 0.0553 |     - |     - |     232 B |


BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
  [Host]     : .NET Framework 4.8 (4.8.4341.0), X64 RyuJIT
  DefaultJob : .NET Framework 4.8 (4.8.4341.0), X64 RyuJIT

|                    Method | WithSDK |        Mean |      Error |     StdDev |      Median |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |-------- |------------:|-----------:|-----------:|------------:|-------:|------:|------:|----------:|
|            CounterHotPath |   False |    22.27 ns |   0.465 ns |   0.457 ns |    22.27 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |    33.08 ns |   0.664 ns |   0.554 ns |    32.89 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |    56.24 ns |   1.089 ns |   1.119 ns |    55.75 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False |    50.67 ns |   0.523 ns |   0.489 ns |    50.69 ns | 0.0248 |     - |     - |     104 B |
|            CounterHotPath |    True |   204.64 ns |   4.100 ns |   5.186 ns |   202.99 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True |   294.17 ns |   5.902 ns |   6.561 ns |   291.02 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True | 1,306.52 ns |  94.197 ns | 277.742 ns | 1,151.40 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True | 3,152.20 ns | 213.046 ns | 628.171 ns | 3,274.49 ns | 0.0534 |     - |     - |     233 B |
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
