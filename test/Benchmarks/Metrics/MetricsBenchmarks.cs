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

|                    Method | WithSDK |       Mean |      Error |     StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |-------- |-----------:|-----------:|-----------:|-------:|------:|------:|----------:|
|            CounterHotPath |   False |  13.291 ns |  0.0867 ns |  0.0769 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |   8.777 ns |  0.1995 ns |  0.2523 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |  22.326 ns |  0.1740 ns |  0.1453 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False |  31.854 ns |  0.5290 ns |  0.4949 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |    True | 111.240 ns |  2.0913 ns |  1.7464 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True | 170.970 ns |  3.4123 ns |  3.1919 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True | 478.776 ns |  2.8576 ns |  2.3862 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True | 614.775 ns | 11.6086 ns | 12.4211 ns | 0.0553 |     - |     - |     232 B |


BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19043
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
  [Host]     : .NET Framework 4.8 (4.8.4360.0), X64 RyuJIT
  DefaultJob : .NET Framework 4.8 (4.8.4360.0), X64 RyuJIT

|                    Method | WithSDK |        Mean |     Error |    StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |-------- |------------:|----------:|----------:|-------:|------:|------:|----------:|
|            CounterHotPath |   False |    21.66 ns |  0.304 ns |  0.270 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |    26.03 ns |  0.154 ns |  0.136 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |    42.22 ns |  0.252 ns |  0.223 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False |    46.35 ns |  0.890 ns |  0.953 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |    True |   143.95 ns |  1.215 ns |  1.014 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True |   224.92 ns |  4.491 ns |  8.760 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True |   905.84 ns |  6.651 ns |  5.193 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True | 1,898.68 ns | 35.805 ns | 36.770 ns | 0.0553 |     - |     - |     233 B |
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
