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
|            CounterHotPath |   False |   2.872 ns | 0.0849 ns | 0.1190 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |  10.889 ns | 0.2090 ns | 0.1852 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |  33.151 ns | 0.3697 ns | 0.2886 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False |  25.051 ns | 0.4204 ns | 0.3932 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |    True |  82.304 ns | 1.6376 ns | 2.5495 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True | 150.810 ns | 2.1988 ns | 1.9492 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True | 465.500 ns | 8.0853 ns | 7.1674 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True | 590.172 ns | 7.9478 ns | 7.0455 ns | 0.0553 |     - |     - |     232 B |


BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
  [Host]     : .NET Framework 4.8 (4.8.4341.0), X64 RyuJIT
  DefaultJob : .NET Framework 4.8 (4.8.4341.0), X64 RyuJIT

|                    Method | WithSDK |        Mean |     Error |    StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |-------- |------------:|----------:|----------:|-------:|------:|------:|----------:|
|            CounterHotPath |   False |    19.70 ns |  0.325 ns |  0.304 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   False |    28.97 ns |  0.586 ns |  0.802 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   False |    49.78 ns |  0.785 ns |  0.696 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   False |    44.14 ns |  0.620 ns |  0.550 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |    True |   131.35 ns |  2.401 ns |  2.358 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |    True |   205.92 ns |  3.455 ns |  3.063 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |    True |   885.30 ns | 10.616 ns |  9.410 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |    True | 1,807.11 ns | 17.611 ns | 15.612 ns | 0.0553 |     - |     - |     233 B |
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
