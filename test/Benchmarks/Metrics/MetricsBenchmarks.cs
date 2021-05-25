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

|                    Method | WithSDK |       Mean |      Error |     StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |-------- |-----------:|-----------:|-----------:|-------:|------:|------:|----------:|
|            CounterHotPath |   False |   2.780 ns |  0.0847 ns |  0.1160 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |  10.794 ns |  0.2408 ns |  0.3045 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |  30.275 ns |  0.6320 ns |  0.6207 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False |  23.985 ns |  0.4932 ns |  0.5482 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |    True | 133.180 ns |  2.6609 ns |  6.5771 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True | 213.185 ns |  2.8988 ns |  2.4207 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True | 524.721 ns |  9.9822 ns |  8.8490 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True | 682.824 ns | 18.3456 ns | 53.8045 ns | 0.0553 |     - |     - |     232 B |


BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
  [Host]     : .NET Framework 4.8 (4.8.4341.0), X64 RyuJIT
  DefaultJob : .NET Framework 4.8 (4.8.4341.0), X64 RyuJIT

|                    Method | WithSDK |        Mean |     Error |    StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |-------- |------------:|----------:|----------:|-------:|------:|------:|----------:|
|            CounterHotPath |   False |    19.78 ns |  0.261 ns |  0.244 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |    28.94 ns |  0.585 ns |  0.626 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |    50.82 ns |  1.046 ns |  1.027 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False |    44.11 ns |  0.303 ns |  0.298 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |    True |   175.06 ns |  1.296 ns |  1.083 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True |   267.81 ns |  4.409 ns |  3.442 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True |   935.55 ns | 11.504 ns | 10.198 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True | 1,913.39 ns | 37.111 ns | 55.546 ns | 0.0553 |     - |     - |     233 B |
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
