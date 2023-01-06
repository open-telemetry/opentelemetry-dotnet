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
|            CounterHotPath |             Cumulative |  14.45 ns | 0.082 ns | 0.068 ns |         - |
| CounterWith1LabelsHotPath |             Cumulative |  60.54 ns | 0.473 ns | 0.442 ns |         - |
| CounterWith3LabelsHotPath |             Cumulative | 134.96 ns | 1.543 ns | 1.368 ns |         - |
| CounterWith5LabelsHotPath |             Cumulative | 206.31 ns | 1.219 ns | 1.081 ns |         - |
| CounterWith6LabelsHotPath |             Cumulative | 230.43 ns | 0.898 ns | 0.840 ns |         - |
| CounterWith7LabelsHotPath |             Cumulative | 254.62 ns | 1.156 ns | 1.082 ns |         - |
|            CounterHotPath |                  Delta |  13.79 ns | 0.021 ns | 0.019 ns |         - |
| CounterWith1LabelsHotPath |                  Delta |  58.06 ns | 0.227 ns | 0.212 ns |         - |
| CounterWith3LabelsHotPath |                  Delta | 131.89 ns | 0.304 ns | 0.285 ns |         - |
| CounterWith5LabelsHotPath |                  Delta | 207.92 ns | 1.050 ns | 0.982 ns |         - |
| CounterWith6LabelsHotPath |                  Delta | 232.65 ns | 1.011 ns | 0.896 ns |         - |
| CounterWith7LabelsHotPath |                  Delta | 266.57 ns | 5.200 ns | 5.988 ns |         - |
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
