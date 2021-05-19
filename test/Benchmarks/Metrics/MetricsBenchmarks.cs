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

|                    Method | WithSDK |       Mean |     Error |    StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |-------- |-----------:|----------:|----------:|-------:|------:|------:|----------:|
|            CounterHotPath |   False |   2.792 ns | 0.0792 ns | 0.0702 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |  11.394 ns | 0.2440 ns | 0.2712 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |  29.669 ns | 0.3933 ns | 0.3486 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False |  23.727 ns | 0.3956 ns | 0.4397 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |    True | 129.288 ns | 2.3708 ns | 2.1016 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True | 201.401 ns | 2.6410 ns | 2.3412 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True | 534.127 ns | 7.1438 ns | 5.9654 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True | 647.037 ns | 6.2771 ns | 5.2416 ns | 0.0801 |     - |     - |     336 B |


BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
  [Host]     : .NET Framework 4.8 (4.8.4341.0), X64 RyuJIT
  DefaultJob : .NET Framework 4.8 (4.8.4341.0), X64 RyuJIT

|                    Method | WithSDK |        Mean |     Error |    StdDev |      Median |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |-------- |------------:|----------:|----------:|------------:|-------:|------:|------:|----------:|
|            CounterHotPath |   False |    19.44 ns |  0.188 ns |  0.157 ns |    19.49 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |    29.16 ns |  0.604 ns |  1.043 ns |    28.83 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |    49.66 ns |  0.860 ns |  1.118 ns |    49.24 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False |    44.24 ns |  0.844 ns |  0.790 ns |    44.03 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |    True |   167.68 ns |  4.819 ns | 13.905 ns |   167.76 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True |   247.20 ns |  7.750 ns | 22.609 ns |   233.91 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True |   918.33 ns | 14.140 ns | 11.808 ns |   916.30 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True | 1,903.26 ns | 21.358 ns | 17.835 ns | 1,901.57 ns | 0.0801 |     - |     - |     337 B |
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
