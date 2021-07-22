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
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;

/*
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19043
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.202
  [Host]     : .NET Core 3.1.13 (CoreCLR 4.700.21.11102, CoreFX 4.700.21.11602), X64 RyuJIT
  DefaultJob : .NET Core 3.1.13 (CoreCLR 4.700.21.11102, CoreFX 4.700.21.11602), X64 RyuJIT


|                    Method | WithSDK |       Mean |      Error |    StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |-------- |-----------:|-----------:|----------:|-------:|------:|------:|----------:|
|            CounterHotPath |   False |  15.126 ns |  0.3228 ns | 0.3965 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |   9.766 ns |  0.2268 ns | 0.3530 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |  25.240 ns |  0.2876 ns | 0.2690 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False |  37.929 ns |  0.7512 ns | 0.5865 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |    True |  44.790 ns |  0.9101 ns | 1.3621 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True | 115.023 ns |  2.1001 ns | 1.9644 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True | 436.527 ns |  6.5121 ns | 5.7728 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True | 586.498 ns | 11.4783 ns | 9.5849 ns | 0.0553 |     - |     - |     232 B |


BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19043
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
  [Host]     : .NET Framework 4.8 (4.8.4360.0), X64 RyuJIT
  DefaultJob : .NET Framework 4.8 (4.8.4360.0), X64 RyuJIT


|                    Method | WithSDK |        Mean |     Error |    StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |-------- |------------:|----------:|----------:|-------:|------:|------:|----------:|
|            CounterHotPath |   False |    23.53 ns |  0.480 ns |  0.401 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |    28.70 ns |  0.592 ns |  0.770 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |    46.27 ns |  0.942 ns |  1.157 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False |    51.66 ns |  1.060 ns |  1.857 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |    True |    70.44 ns |  1.029 ns |  0.912 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True |   151.92 ns |  3.067 ns |  3.651 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True |   876.20 ns | 15.920 ns | 14.892 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True | 1,973.64 ns | 38.393 ns | 45.705 ns | 0.0534 |     - |     - |     233 B |
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
        public void CounterWith1LabelLocal()
        {
            this.counter?.Add(100, new KeyValuePair<string, object>("key", "value"));
        }

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
