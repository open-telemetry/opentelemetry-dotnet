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
|            HistogramHotPath |         10 |  41.79 ns | 0.096 ns | 0.089 ns |         - |
|  HistogramWith1LabelHotPath |         10 |  93.32 ns | 0.185 ns | 0.173 ns |         - |
| HistogramWith3LabelsHotPath |         10 | 173.11 ns | 0.090 ns | 0.079 ns |         - |
| HistogramWith5LabelsHotPath |         10 | 263.42 ns | 0.542 ns | 0.507 ns |         - |
| HistogramWith7LabelsHotPath |         10 | 318.65 ns | 0.388 ns | 0.344 ns |         - |
|            HistogramHotPath |         50 |  51.52 ns | 0.234 ns | 0.208 ns |         - |
|  HistogramWith1LabelHotPath |         50 | 102.16 ns | 0.201 ns | 0.178 ns |         - |
| HistogramWith3LabelsHotPath |         50 | 188.54 ns | 0.263 ns | 0.246 ns |         - |
| HistogramWith5LabelsHotPath |         50 | 274.89 ns | 0.471 ns | 0.441 ns |         - |
| HistogramWith7LabelsHotPath |         50 | 334.87 ns | 0.541 ns | 0.451 ns |         - |
|            HistogramHotPath |        139 |  75.40 ns | 0.085 ns | 0.075 ns |         - |
|  HistogramWith1LabelHotPath |        139 | 123.86 ns | 0.510 ns | 0.477 ns |         - |
| HistogramWith3LabelsHotPath |        139 | 211.11 ns | 0.415 ns | 0.368 ns |         - |
| HistogramWith5LabelsHotPath |        139 | 298.31 ns | 0.788 ns | 0.737 ns |         - |
| HistogramWith7LabelsHotPath |        139 | 357.28 ns | 0.619 ns | 0.548 ns |         - |
|            HistogramHotPath |        140 |  69.13 ns | 0.171 ns | 0.160 ns |         - |
|  HistogramWith1LabelHotPath |        140 | 117.86 ns | 0.182 ns | 0.171 ns |         - |
| HistogramWith3LabelsHotPath |        140 | 208.26 ns | 0.382 ns | 0.319 ns |         - |
| HistogramWith5LabelsHotPath |        140 | 297.56 ns | 0.769 ns | 0.682 ns |         - |
| HistogramWith7LabelsHotPath |        140 | 349.53 ns | 0.581 ns | 0.515 ns |         - |
|            HistogramHotPath |       1000 |  85.90 ns | 0.263 ns | 0.246 ns |         - |
|  HistogramWith1LabelHotPath |       1000 | 136.94 ns | 0.475 ns | 0.444 ns |         - |
| HistogramWith3LabelsHotPath |       1000 | 230.74 ns | 0.465 ns | 0.435 ns |         - |
| HistogramWith5LabelsHotPath |       1000 | 325.73 ns | 2.040 ns | 1.908 ns |         - |
| HistogramWith7LabelsHotPath |       1000 | 379.81 ns | 2.100 ns | 1.964 ns |         - |
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

        // Note: Values related to `Metric.DefaultHistogramCountForBinarySearch`
        [Params(10, 50, 139, 140, 1000)]
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
