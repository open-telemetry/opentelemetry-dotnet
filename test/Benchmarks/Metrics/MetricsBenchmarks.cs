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
|            CounterHotPath |   False |  14.283 ns |  0.3598 ns |  1.0324 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |   8.296 ns |  0.1900 ns |  0.1951 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |  23.124 ns |  0.3948 ns |  0.3500 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False |  31.497 ns |  0.5106 ns |  0.4264 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |    True | 105.109 ns |  2.0763 ns |  2.3078 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True | 164.264 ns |  3.1936 ns |  4.5801 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True | 483.273 ns |  5.9420 ns |  4.9619 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True | 640.766 ns | 12.8239 ns | 19.5835 ns | 0.0801 |     - |     - |     336 B |


BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
  [Host]     : .NET Framework 4.8 (4.8.4341.0), X64 RyuJIT
  DefaultJob : .NET Framework 4.8 (4.8.4341.0), X64 RyuJIT

|                    Method | WithSDK |        Mean |     Error |     StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |-------- |------------:|----------:|-----------:|-------:|------:|------:|----------:|
|            CounterHotPath |   False |    25.15 ns |  0.493 ns |   0.937 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |    27.18 ns |  0.566 ns |   0.793 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |    43.15 ns |  0.740 ns |   0.692 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False |    46.79 ns |  0.876 ns |   0.777 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |    True |   117.24 ns |  2.097 ns |   2.331 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True |   200.54 ns |  4.042 ns |  10.288 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True |   961.15 ns | 18.483 ns |  15.434 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True | 2,165.80 ns | 44.186 ns | 126.777 ns | 0.0801 |     - |     - |     337 B |
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
