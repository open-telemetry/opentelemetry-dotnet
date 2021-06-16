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

|                    Method |  Mode |       Mean |      Error |     StdDev |     Median |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |------ |-----------:|-----------:|-----------:|-----------:|-------:|------:|------:|----------:|
|            CounterHotPath | NoSDK |  13.886 ns |  0.3009 ns |  0.3090 ns |  13.914 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath | NoSDK |   8.433 ns |  0.1158 ns |  0.1027 ns |   8.423 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath | NoSDK |  23.082 ns |  0.2709 ns |  0.2401 ns |  23.059 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath | NoSDK |  34.260 ns |  0.4007 ns |  0.3748 ns |  34.072 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |   SDK |  53.448 ns |  0.3378 ns |  0.3160 ns |  53.417 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   SDK | 118.682 ns |  0.9682 ns |  0.9056 ns | 118.429 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   SDK | 426.226 ns |  7.1118 ns |  7.9047 ns | 424.518 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   SDK | 613.745 ns | 14.2660 ns | 41.6146 ns | 600.588 ns | 0.0553 |     - |     - |     232 B |
|            CounterHotPath |  View | 205.877 ns |  4.0924 ns |  6.3713 ns | 204.168 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |  View | 182.158 ns |  3.6239 ns |  6.4415 ns | 179.651 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |  View | 549.815 ns |  8.2622 ns |  7.3242 ns | 549.284 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |  View | 953.732 ns | 19.0132 ns | 38.8390 ns | 949.856 ns | 0.0553 |     - |     - |     232 B |


BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19043
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
  [Host]     : .NET Framework 4.8 (4.8.4360.0), X64 RyuJIT
  DefaultJob : .NET Framework 4.8 (4.8.4360.0), X64 RyuJIT

|                    Method |  Mode |        Mean |     Error |    StdDev |  Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |------ |------------:|----------:|----------:|-------:|------:|------:|----------:|
|            CounterHotPath | NoSDK |    22.37 ns |  0.430 ns |  0.403 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath | NoSDK |    27.43 ns |  0.550 ns |  0.676 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath | NoSDK |    44.17 ns |  0.738 ns |  0.654 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath | NoSDK |    49.14 ns |  0.999 ns |  1.901 ns | 0.0249 |     - |     - |     104 B |
|            CounterHotPath |   SDK |    75.09 ns |  1.451 ns |  1.613 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |   SDK |   149.84 ns |  2.946 ns |  3.507 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |   SDK |   870.82 ns | 17.311 ns | 30.770 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |   SDK | 1,843.29 ns | 34.145 ns | 31.940 ns | 0.0553 |     - |     - |     233 B |
|            CounterHotPath |  View |   210.29 ns |  4.105 ns |  4.031 ns |      - |     - |     - |         - |
| CounterWith1LabelsHotPath |  View |   218.88 ns |  4.084 ns |  3.820 ns |      - |     - |     - |         - |
| CounterWith3LabelsHotPath |  View |   980.54 ns | 14.153 ns | 12.546 ns |      - |     - |     - |         - |
| CounterWith5LabelsHotPath |  View | 2,076.05 ns | 26.116 ns | 21.808 ns | 0.0534 |     - |     - |     233 B |
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
                            (inst) => true,
                            new MetricAggregatorType[] { MetricAggregatorType.HISTOGRAM },
                            "test",
                            new IncludeTagRule((tag) => tag != "attrib1"),
                            new RequireTagRule("attrib0", "defaultValue"))
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
