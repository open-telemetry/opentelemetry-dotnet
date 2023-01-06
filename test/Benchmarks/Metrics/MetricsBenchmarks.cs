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

using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
// * Summary *

BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.22621.963)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.101
  [Host]     : .NET 7.0.1 (7.0.122.56804), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.1 (7.0.122.56804), X64 RyuJIT AVX2


|                    Method | AggregationTemporality |      Mean |    Error |   StdDev | Allocated |
|-------------------------- |----------------------- |----------:|---------:|---------:|----------:|
|            CounterHotPath |             Cumulative |  13.82 ns | 0.020 ns | 0.018 ns |         - |
| CounterWith1LabelsHotPath |             Cumulative |  61.06 ns | 0.276 ns | 0.231 ns |         - |
| CounterWith3LabelsHotPath |             Cumulative | 135.11 ns | 0.609 ns | 0.540 ns |         - |
| CounterWith5LabelsHotPath |             Cumulative | 207.05 ns | 0.232 ns | 0.181 ns |         - |
| CounterWith6LabelsHotPath |             Cumulative | 235.28 ns | 0.513 ns | 0.480 ns |         - |
| CounterWith7LabelsHotPath |             Cumulative | 261.48 ns | 0.665 ns | 0.589 ns |         - |
|            CounterHotPath |                  Delta |  13.88 ns | 0.110 ns | 0.103 ns |         - |
| CounterWith1LabelsHotPath |                  Delta |  58.28 ns | 0.375 ns | 0.351 ns |         - |
| CounterWith3LabelsHotPath |                  Delta | 128.66 ns | 0.246 ns | 0.230 ns |         - |
| CounterWith5LabelsHotPath |                  Delta | 210.62 ns | 1.246 ns | 1.166 ns |         - |
| CounterWith6LabelsHotPath |                  Delta | 233.51 ns | 0.656 ns | 0.614 ns |         - |
| CounterWith7LabelsHotPath |                  Delta | 263.35 ns | 0.865 ns | 0.766 ns |         - |
*/

namespace Benchmarks.Metrics
{
    public class MetricsBenchmarks
    {
        private readonly Random random = new();
        private readonly string[] dimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };
        private Counter<long> counter;
        private MeterProvider provider;
        private Meter meter;

        [Params(MetricReaderTemporalityPreference.Cumulative, MetricReaderTemporalityPreference.Delta)]
        public MetricReaderTemporalityPreference AggregationTemporality { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.meter = new Meter(Utils.GetCurrentMethodName());

            var exportedItems = new List<Metric>();
            this.provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(this.meter.Name) // All instruments from this meter are enabled.
                .AddInMemoryExporter(exportedItems, metricReaderOptions =>
                {
                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
                    metricReaderOptions.TemporalityPreference = this.AggregationTemporality;
                })
                .Build();

            this.counter = this.meter.CreateCounter<long>("counter");
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
            this.counter.Add(100);
        }

        [Benchmark]
        public void CounterWith1LabelsHotPath()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[this.random.Next(0, 2)]);
            this.counter.Add(100, tag1);
        }

        [Benchmark]
        public void CounterWith3LabelsHotPath()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[this.random.Next(0, 10)]);
            var tag2 = new KeyValuePair<string, object>("DimName2", this.dimensionValues[this.random.Next(0, 10)]);
            var tag3 = new KeyValuePair<string, object>("DimName3", this.dimensionValues[this.random.Next(0, 10)]);
            this.counter.Add(100, tag1, tag2, tag3);
        }

        [Benchmark]
        public void CounterWith5LabelsHotPath()
        {
            var tags = new TagList
            {
                { "DimName1", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName2", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName3", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName4", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName5", this.dimensionValues[this.random.Next(0, 10)] },
            };
            this.counter.Add(100, tags);
        }

        [Benchmark]
        public void CounterWith6LabelsHotPath()
        {
            var tags = new TagList
            {
                { "DimName1", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName2", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName3", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName4", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName5", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName6", this.dimensionValues[this.random.Next(0, 2)] },
            };
            this.counter.Add(100, tags);
        }

        [Benchmark]
        public void CounterWith7LabelsHotPath()
        {
            var tags = new TagList
            {
                { "DimName1", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName2", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName3", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName4", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName5", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName6", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName7", this.dimensionValues[this.random.Next(0, 1)] },
            };
            this.counter.Add(100, tags);
        }
    }
}
