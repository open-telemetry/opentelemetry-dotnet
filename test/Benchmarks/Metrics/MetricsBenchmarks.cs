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

|                    Method |  Mode |       Mean |     Error |     StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |------ |-----------:|----------:|-----------:|-------:|------:|------:|----------:|
|            CounterHotPath | NoSDK |  14.183 ns | 0.3129 ns |  0.3478 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath | NoSDK |   8.917 ns | 0.1507 ns |  0.1258 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath | NoSDK |  23.664 ns | 0.3484 ns |  0.3259 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath | NoSDK |  36.362 ns | 0.7434 ns |  0.7954 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |   SDK |  56.031 ns | 1.0774 ns |  0.9551 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   SDK | 134.472 ns | 2.4319 ns |  2.1558 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   SDK | 445.190 ns | 8.8090 ns | 11.4542 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   SDK | 580.113 ns | 8.2307 ns |  9.4785 ns | 0.0553 |     - |     - |     232 B |
|            CounterHotPath |  View | 100.290 ns | 1.7791 ns |  1.5771 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |  View | 190.740 ns | 3.7800 ns |  6.1040 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |  View | 221.304 ns | 4.3939 ns |  4.1101 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |  View | 258.565 ns | 5.1648 ns |  5.0725 ns | 0.0248 |     - |     - |     104 B |


BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19043
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
  [Host]     : .NET Framework 4.8 (4.8.4360.0), X64 RyuJIT
  DefaultJob : .NET Framework 4.8 (4.8.4360.0), X64 RyuJIT

|                    Method |  Mode |        Mean |     Error |    StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |------ |------------:|----------:|----------:|-------:|------:|------:|----------:|
|            CounterHotPath | NoSDK |    22.22 ns |  0.332 ns |  0.294 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath | NoSDK |    27.62 ns |  0.471 ns |  0.691 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath | NoSDK |    42.27 ns |  0.870 ns |  0.813 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath | NoSDK |    50.45 ns |  0.985 ns |  0.921 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |   SDK |    76.33 ns |  1.526 ns |  1.353 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   SDK |   154.49 ns |  3.106 ns |  3.050 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   SDK |   858.37 ns | 17.108 ns | 16.003 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   SDK | 1,873.38 ns | 36.182 ns | 38.714 ns | 0.0534 |     - |     - |     233 B |
|            CounterHotPath |  View |   119.13 ns |  1.774 ns |  1.573 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |  View |   223.74 ns |  3.621 ns |  3.210 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |  View |   255.62 ns |  4.904 ns |  4.095 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |  View |   297.44 ns |  5.024 ns |  5.159 ns | 0.0248 |     - |     - |     104 B |
*/

namespace Benchmarks.Metrics
{
    // [SimpleJob(launchCount: 1, warmupCount: 1, targetCount: 4)]
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

        [Params("NoSDK", "SDK", "View")]
        public string Mode { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            switch (this.Mode)
            {
                case "NoSDK":
                    break;

                case "SDK":
                    this.provider = Sdk.CreateMeterProviderBuilder()
                        .AddSource("TestMeter") // All instruments from this meter are enabled.
                        .Build();
                    break;

                case "View":
                    this.provider = Sdk.CreateMeterProviderBuilder()
                        .AddSource("TestMeter") // All instruments from this meter are enabled.
                        .AddView(
                            meterName: "TestMeter",
                            aggregator: Aggregator.SUM,
                            attributeKeys: new string[] { "attrib1", "label2" },
                            viewName: "test")
                        .Build();
                    break;
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
