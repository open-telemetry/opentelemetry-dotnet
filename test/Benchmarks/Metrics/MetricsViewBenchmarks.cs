// <copyright file="MetricsViewBenchmarks.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;

/*
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19043
Intel Core i7-8650U CPU 1.90GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.401
  [Host]     : .NET Core 5.0.10 (CoreCLR 5.0.1021.41214, CoreFX 5.0.1021.41214), X64 RyuJIT
  DefaultJob : .NET Core 5.0.10 (CoreCLR 5.0.1021.41214, CoreFX 5.0.1021.41214), X64 RyuJIT


|                    Method | ViewConfig |     Mean |    Error |   StdDev | Gen 0 | Gen 1 | Gen 2 | Allocated |
|-------------------------- |----------- |---------:|---------:|---------:|------:|------:|------:|----------:|
| CounterWith3LabelsHotPath |          1 | 530.7 ns | 10.45 ns | 17.17 ns |     - |     - |     - |         - |
| CounterWith3LabelsHotPath |          2 | 569.3 ns | 11.27 ns |  9.41 ns |     - |     - |     - |         - |
*/

namespace Benchmarks.Metrics
{
    [MemoryDiagnoser]
    public class MetricsViewBenchmarks
    {
        private Counter<long> counter;
        private MeterProvider provider;
        private Meter meter;
        private Random random = new Random();
        private string[] dimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };

        [Params(1, 2)]
        public int ViewConfig { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            /* ViewConfig 1 = no views registered at all.
             * ViewConfig 2 = view registered, but does not select the instrument
             * TODO: ViewConfig 3 = view registed, selects the instrument.
             */

            this.meter = new Meter("TestMeter");
            this.counter = this.meter.CreateCounter<long>("counter");

            if (this.ViewConfig == 1)
            {
                this.provider = Sdk.CreateMeterProviderBuilder()
                    .AddSource(this.meter.Name)
                    .Build();
            }
            else if (this.ViewConfig == 2)
            {
                this.provider = Sdk.CreateMeterProviderBuilder()
                    .AddSource(this.meter.Name)
                    .AddView(this.counter.Name + "notmatch", new MetricStreamConfiguration() { TagKeys = new string[] { "DimName1", "DimName2", "DimName3" } })
                    .Build();
            }
            else if (this.ViewConfig == 3)
            {
                this.provider = Sdk.CreateMeterProviderBuilder()
                    .AddSource(this.meter.Name)
                    .AddView(this.counter.Name, new MetricStreamConfiguration() { TagKeys = new string[] { "DimName1", "DimName2", "DimName3" } })
                    .Build();
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.meter?.Dispose();
            this.provider?.Dispose();
        }

        [Benchmark]
        public void CounterWith3LabelsHotPath()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[this.random.Next(0, 10)]);
            var tag2 = new KeyValuePair<string, object>("DimName2", this.dimensionValues[this.random.Next(0, 10)]);
            var tag3 = new KeyValuePair<string, object>("DimName3", this.dimensionValues[this.random.Next(0, 10)]);
            this.counter?.Add(100, tag1, tag2, tag3);
        }
    }
}
