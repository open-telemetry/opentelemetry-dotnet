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

extern alias InMemory;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using InMemory::OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
// * Summary *

BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19043.1288 (21H1/May2021Update)
Intel Core i7-4790 CPU 3.60GHz (Haswell), 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.100-rc.2.21505.57
  [Host]     : .NET 5.0.11 (5.0.1121.47308), X64 RyuJIT
  DefaultJob : .NET 5.0.11 (5.0.1121.47308), X64 RyuJIT


|                    Method | AggregationTemporality |        Mean |     Error |    StdDev | Allocated |
|-------------------------- |----------------------- |------------:|----------:|----------:|----------:|
|            CounterHotPath |             Cumulative |    22.12 ns |  0.233 ns |  0.218 ns |         - |
| CounterWith1LabelsHotPath |             Cumulative |   101.78 ns |  0.283 ns |  0.265 ns |         - |
| CounterWith3LabelsHotPath |             Cumulative |   488.20 ns |  6.580 ns |  7.577 ns |         - |
| CounterWith5LabelsHotPath |             Cumulative |   748.28 ns | 14.936 ns | 22.355 ns |         - |
| CounterWith6LabelsHotPath |             Cumulative |   857.56 ns |  6.008 ns |  4.691 ns |         - |
| CounterWith7LabelsHotPath |             Cumulative | 1,023.17 ns | 11.500 ns |  9.603 ns |         - |
|            CounterHotPath |                  Delta |    20.93 ns |  0.244 ns |  0.216 ns |         - |
| CounterWith1LabelsHotPath |                  Delta |   104.09 ns |  0.432 ns |  0.404 ns |         - |
| CounterWith3LabelsHotPath |                  Delta |   477.48 ns |  2.646 ns |  2.066 ns |         - |
| CounterWith5LabelsHotPath |                  Delta |   732.90 ns |  8.778 ns |  7.782 ns |         - |
| CounterWith6LabelsHotPath |                  Delta |   868.00 ns |  4.503 ns |  3.760 ns |         - |
| CounterWith7LabelsHotPath |                  Delta |   986.40 ns |  9.503 ns |  7.419 ns |         - |
*/

namespace Benchmarks.Metrics
{
    [MemoryDiagnoser]
    public class MetricsBenchmarks
    {
        private Counter<long> counter;
        private MeterProvider provider;
        private Meter meter;
        private Random random = new Random();
        private string[] dimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };

        [Params(AggregationTemporality.Cumulative, AggregationTemporality.Delta)]
        public AggregationTemporality AggregationTemporality { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.meter = new Meter(Utils.GetCurrentMethodName());

            var exportedItems = new List<Metric>();
            var reader = new PeriodicExportingMetricReader(new InMemoryExporter<Metric>(exportedItems), 1000);
            reader.PreferredAggregationTemporality = this.AggregationTemporality;
            this.provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(this.meter.Name) // All instruments from this meter are enabled.
                .AddReader(reader)
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
            this.counter?.Add(100);
        }

        [Benchmark]
        public void CounterWith1LabelsHotPath()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[this.random.Next(0, 2)]);
            this.counter?.Add(100, tag1);
        }

        [Benchmark]
        public void CounterWith3LabelsHotPath()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[this.random.Next(0, 10)]);
            var tag2 = new KeyValuePair<string, object>("DimName2", this.dimensionValues[this.random.Next(0, 10)]);
            var tag3 = new KeyValuePair<string, object>("DimName3", this.dimensionValues[this.random.Next(0, 10)]);
            this.counter?.Add(100, tag1, tag2, tag3);
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
            this.counter?.Add(100, tags);
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
            this.counter?.Add(100, tags);
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
            this.counter?.Add(100, tags);
        }
    }
}
