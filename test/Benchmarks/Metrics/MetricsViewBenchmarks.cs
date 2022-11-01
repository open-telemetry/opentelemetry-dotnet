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
using System.Diagnostics.Metrics;
using System.Threading;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
BenchmarkDotNet=v0.13.2, OS=Windows 10 (10.0.19044.2130/21H2/November2021Update)
Intel Core i7-4790 CPU 3.60GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET SDK=7.0.100-preview.7.22377.5
  [Host]     : .NET 6.0.10 (6.0.1022.47605), X64 RyuJIT AVX2
  DefaultJob : .NET 6.0.10 (6.0.1022.47605), X64 RyuJIT AVX2


|                                   Method |        ViewConfig |     Mean |   Error |  StdDev | Allocated |
|----------------------------------------- |------------------ |---------:|--------:|--------:|----------:|
| CounterMeasurementRecordingWithThreeTags |            NoView | 188.1 ns | 1.73 ns | 1.54 ns |         - |
| CounterMeasurementRecordingWithThreeTags | ViewNoInstrSelect | 193.3 ns | 0.66 ns | 0.61 ns |         - |
| CounterMeasurementRecordingWithThreeTags |  ViewSelectsInstr | 271.3 ns | 2.18 ns | 1.94 ns |         - |
*/

namespace Benchmarks.Metrics
{
    public class MetricsViewBenchmarks
    {
        private static readonly ThreadLocal<Random> ThreadLocalRandom = new(() => new Random());
        private static readonly string[] DimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };
        private static readonly int DimensionsValuesLength = DimensionValues.Length;
        private List<Metric> metrics;
        private Counter<long> counter;
        private MeterProvider provider;
        private Meter meter;

        public enum ViewConfiguration
        {
            /// <summary>
            /// No views registered in the provider.
            /// </summary>
            NoView,

            /// <summary>
            /// Provider has view registered, but it doesn't select the instrument.
            /// This tests the perf impact View has on hot path, for those
            /// instruments not participating in View feature.
            /// </summary>
            ViewNoInstrSelect,

            /// <summary>
            /// Provider has view registered and it does select the instrument.
            /// </summary>
            ViewSelectsInstr,
        }

        [Params(
            ViewConfiguration.NoView,
            ViewConfiguration.ViewNoInstrSelect,
            ViewConfiguration.ViewSelectsInstr)]
        public ViewConfiguration ViewConfig { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.meter = new Meter(Utils.GetCurrentMethodName());
            this.counter = this.meter.CreateCounter<long>("counter");
            this.metrics = new List<Metric>();

            if (this.ViewConfig == ViewConfiguration.NoView)
            {
                this.provider = Sdk.CreateMeterProviderBuilder()
                    .AddMeter(this.meter.Name)
                    .AddInMemoryExporter(this.metrics)
                    .Build();
            }
            else if (this.ViewConfig == ViewConfiguration.ViewNoInstrSelect)
            {
                this.provider = Sdk.CreateMeterProviderBuilder()
                    .AddMeter(this.meter.Name)
                    .AddView("nomatch", new MetricStreamConfiguration() { TagKeys = new string[] { "DimName1", "DimName2", "DimName3" } })
                    .AddInMemoryExporter(this.metrics)
                    .Build();
            }
            else if (this.ViewConfig == ViewConfiguration.ViewSelectsInstr)
            {
                this.provider = Sdk.CreateMeterProviderBuilder()
                    .AddMeter(this.meter.Name)
                    .AddView(this.counter.Name, new MetricStreamConfiguration() { TagKeys = new string[] { "DimName1", "DimName2", "DimName3" } })
                    .AddInMemoryExporter(this.metrics)
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
        public void CounterMeasurementRecordingWithThreeTags()
        {
            var random = ThreadLocalRandom.Value;
            this.counter?.Add(
                100,
                new KeyValuePair<string, object>("DimName1", DimensionValues[random.Next(0, DimensionsValuesLength)]),
                new KeyValuePair<string, object>("DimName2", DimensionValues[random.Next(0, DimensionsValuesLength)]),
                new KeyValuePair<string, object>("DimName3", DimensionValues[random.Next(0, DimensionsValuesLength)]));
        }
    }
}
