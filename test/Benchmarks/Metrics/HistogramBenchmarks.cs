// <copyright file="HistogramBenchmarks.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Tests;

/*
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19044.1706 (21H2)
AMD Ryzen 9 3900X, 1 CPU, 24 logical and 12 physical cores
.NET SDK=6.0.203
  [Host]     : .NET 6.0.5 (6.0.522.21309), X64 RyuJIT
  DefaultJob : .NET 6.0.5 (6.0.522.21309), X64 RyuJIT


|                      Method | BoundCount |      Mean |    Error |   StdDev | Allocated |
|---------------------------- |----------- |----------:|---------:|---------:|----------:|
|            HistogramHotPath |         10 |  42.68 ns | 0.116 ns | 0.109 ns |         - |
|  HistogramWith1LabelHotPath |         10 |  89.94 ns | 0.195 ns | 0.173 ns |         - |
| HistogramWith3LabelsHotPath |         10 | 175.81 ns | 0.597 ns | 0.558 ns |         - |
| HistogramWith5LabelsHotPath |         10 | 259.52 ns | 0.435 ns | 0.363 ns |         - |
| HistogramWith7LabelsHotPath |         10 | 316.83 ns | 0.530 ns | 0.470 ns |         - |
|            HistogramHotPath |         50 |  50.70 ns | 0.356 ns | 0.333 ns |         - |
|  HistogramWith1LabelHotPath |         50 | 101.23 ns | 0.155 ns | 0.145 ns |         - |
| HistogramWith3LabelsHotPath |         50 | 185.92 ns | 0.290 ns | 0.271 ns |         - |
| HistogramWith5LabelsHotPath |         50 | 275.40 ns | 0.357 ns | 0.316 ns |         - |
| HistogramWith7LabelsHotPath |         50 | 333.33 ns | 0.646 ns | 0.540 ns |         - |
|            HistogramHotPath |        390 | 115.16 ns | 0.115 ns | 0.108 ns |         - |
|  HistogramWith1LabelHotPath |        390 | 165.81 ns | 0.378 ns | 0.353 ns |         - |
| HistogramWith3LabelsHotPath |        390 | 265.34 ns | 1.043 ns | 0.975 ns |         - |
| HistogramWith5LabelsHotPath |        390 | 374.90 ns | 0.938 ns | 0.878 ns |         - |
| HistogramWith7LabelsHotPath |        390 | 437.83 ns | 1.014 ns | 0.847 ns |         - |
|            HistogramHotPath |        410 | 118.25 ns | 0.103 ns | 0.096 ns |         - |
|  HistogramWith1LabelHotPath |        410 | 171.96 ns | 0.139 ns | 0.130 ns |         - |
| HistogramWith3LabelsHotPath |        410 | 269.87 ns | 0.679 ns | 0.635 ns |         - |
| HistogramWith5LabelsHotPath |        410 | 355.99 ns | 0.831 ns | 0.778 ns |         - |
| HistogramWith7LabelsHotPath |        410 | 421.68 ns | 0.663 ns | 0.587 ns |         - |
*/

namespace Benchmarks.Metrics
{
    public class HistogramBenchmarks
    {
        private const int MaxValue = 10000;
        private readonly Random random = new();
        private readonly string[] dimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };
        private Histogram<long> histogram;
        private MeterProvider provider;
        private Meter meter;
        private double[] bounds;

        [Params(10, 50, 390, 410)]
        public int BoundCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.meter = new Meter(Utils.GetCurrentMethodName());
            this.histogram = this.meter.CreateHistogram<long>("histogram");

            // Evenly distribute the bound values over the range [0, MaxValue)
            this.bounds = new double[this.BoundCount];
            for (int i = 0; i < this.bounds.Length; i++)
            {
                this.bounds[i] = i * MaxValue / this.bounds.Length;
            }

            var exportedItems = new List<Metric>();

            this.provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(this.meter.Name)
                .AddInMemoryExporter(exportedItems, metricReaderOptions =>
                {
                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
                })
                .AddView(this.histogram.Name, new ExplicitBucketHistogramConfiguration() { Boundaries = this.bounds })
                .Build();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.meter?.Dispose();
            this.provider?.Dispose();
        }

        [Benchmark]
        public void HistogramHotPath()
        {
            this.histogram.Record(this.random.Next(MaxValue));
        }

        [Benchmark]
        public void HistogramWith1LabelHotPath()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[this.random.Next(0, 2)]);
            this.histogram.Record(this.random.Next(MaxValue), tag1);
        }

        [Benchmark]
        public void HistogramWith3LabelsHotPath()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[this.random.Next(0, 10)]);
            var tag2 = new KeyValuePair<string, object>("DimName2", this.dimensionValues[this.random.Next(0, 10)]);
            var tag3 = new KeyValuePair<string, object>("DimName3", this.dimensionValues[this.random.Next(0, 10)]);
            this.histogram.Record(this.random.Next(MaxValue), tag1, tag2, tag3);
        }

        [Benchmark]
        public void HistogramWith5LabelsHotPath()
        {
            var tags = new TagList
            {
                { "DimName1", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName2", this.dimensionValues[this.random.Next(0, 2)] },
                { "DimName3", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName4", this.dimensionValues[this.random.Next(0, 5)] },
                { "DimName5", this.dimensionValues[this.random.Next(0, 10)] },
            };
            this.histogram.Record(this.random.Next(MaxValue), tags);
        }

        [Benchmark]
        public void HistogramWith7LabelsHotPath()
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
            this.histogram.Record(this.random.Next(MaxValue), tags);
        }
    }
}
