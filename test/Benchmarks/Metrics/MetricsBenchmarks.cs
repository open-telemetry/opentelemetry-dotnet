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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
// * Summary *

BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19043.1288 (21H1/May2021Update)
Intel Xeon CPU E5-1650 v4 3.60GHz, 1 CPU, 12 logical and 6 physical cores
.NET SDK=6.0.100
  [Host]     : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT
  DefaultJob : .NET 6.0.0 (6.0.21.52210), X64 RyuJIT


|                    Method | AggregationTemporality |      Mean |     Error |    StdDev |    Median | Allocated |
|-------------------------- |----------------------- |----------:|----------:|----------:|----------:|----------:|
|            CounterHotPath |             Cumulative |  19.35 ns |  0.419 ns |  0.946 ns |  19.25 ns |         - |
| CounterWith1LabelsHotPath |             Cumulative |  97.25 ns |  1.973 ns |  3.657 ns |  96.57 ns |         - |
| CounterWith3LabelsHotPath |             Cumulative | 467.93 ns |  9.265 ns | 16.228 ns | 466.28 ns |         - |
| CounterWith5LabelsHotPath |             Cumulative | 746.34 ns | 14.804 ns | 34.014 ns | 749.77 ns |         - |
| CounterWith6LabelsHotPath |             Cumulative | 858.71 ns | 17.180 ns | 37.711 ns | 855.80 ns |         - |
| CounterWith7LabelsHotPath |             Cumulative | 972.73 ns | 19.371 ns | 39.130 ns | 970.10 ns |         - |
|            CounterHotPath |                  Delta |  20.27 ns |  0.415 ns |  0.912 ns |  20.36 ns |         - |
| CounterWith1LabelsHotPath |                  Delta |  98.39 ns |  1.979 ns |  4.891 ns |  98.67 ns |         - |
| CounterWith3LabelsHotPath |                  Delta | 483.07 ns |  9.694 ns | 22.850 ns | 478.88 ns |         - |
| CounterWith5LabelsHotPath |                  Delta | 723.44 ns | 14.472 ns | 24.574 ns | 722.89 ns |         - |
| CounterWith6LabelsHotPath |                  Delta | 850.73 ns | 16.661 ns | 19.187 ns | 850.21 ns |         - |
| CounterWith7LabelsHotPath |                  Delta | 946.01 ns | 18.713 ns | 43.742 ns | 930.80 ns |         - |

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
            this.provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(this.meter.Name) // All instruments from this meter are enabled.
                .AddInMemoryExporter(exportedItems, metricReaderOptions =>
                {
                    metricReaderOptions.MetricReaderType = MetricReaderType.Periodic;
                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
                    metricReaderOptions.Temporality = this.AggregationTemporality;
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
