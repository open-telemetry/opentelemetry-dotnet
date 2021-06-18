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

|                    Method |  Mode |         Mean |      Error |     StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |------ |-------------:|-----------:|-----------:|-------:|------:|------:|----------:|
|            CounterHotPath | NoSDK |    15.975 ns |  0.3493 ns |  0.6208 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath | NoSDK |     9.855 ns |  0.2172 ns |  0.4812 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath | NoSDK |    25.570 ns |  0.5311 ns |  0.6905 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath | NoSDK |    37.721 ns |  0.7728 ns |  1.3936 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |   SDK |    58.688 ns |  0.9150 ns |  0.8559 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   SDK |   129.334 ns |  2.5663 ns |  4.2164 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   SDK |   479.547 ns |  8.5865 ns | 14.5806 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   SDK |   625.457 ns |  7.4830 ns |  6.9996 ns | 0.0553 |     - |     - |     232 B |
|            CounterHotPath |  View |   190.994 ns |  3.4499 ns |  5.0569 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |  View |   198.916 ns |  3.7446 ns |  4.0067 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |  View |   607.863 ns | 11.9418 ns | 20.5990 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |  View | 1,052.530 ns | 21.0554 ns | 51.6493 ns | 0.0553 |     - |     - |     232 B |


BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19043
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
  [Host]     : .NET Framework 4.8 (4.8.4360.0), X64 RyuJIT
  DefaultJob : .NET Framework 4.8 (4.8.4360.0), X64 RyuJIT

|                    Method |  Mode |        Mean |     Error |    StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |------ |------------:|----------:|----------:|-------:|------:|------:|----------:|
|            CounterHotPath | NoSDK |    24.34 ns |  0.491 ns |  0.525 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath | NoSDK |    29.63 ns |  0.240 ns |  0.225 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath | NoSDK |    46.41 ns |  0.917 ns |  0.942 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath | NoSDK |    52.80 ns |  1.062 ns |  0.993 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |   SDK |    79.59 ns |  1.570 ns |  1.986 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   SDK |   160.64 ns |  3.060 ns |  2.862 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   SDK |   903.80 ns | 17.879 ns | 16.724 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   SDK | 2,012.27 ns | 38.694 ns | 36.195 ns | 0.0534 |     - |     - |     233 B |
|            CounterHotPath |  View |   236.99 ns |  4.707 ns |  6.899 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |  View |   242.43 ns |  4.732 ns |  7.905 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |  View | 1,082.05 ns | 18.868 ns | 17.649 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |  View | 2,290.68 ns | 41.679 ns | 38.986 ns | 0.0534 |     - |     - |     233 B |
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
                        .SetDefaultCollectionPeriod(10000)
                        .Build();
                    break;

                case "View":
                    this.provider = Sdk.CreateMeterProviderBuilder()
                        .AddSource("TestMeter") // All instruments from this meter are enabled.
                        .SetDefaultCollectionPeriod(10000)
                        .AddView(
                            meterName: "TestMeter",
                            aggregator: Aggregator.HISTOGRAM,
                            aggregatorParam: false,
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
