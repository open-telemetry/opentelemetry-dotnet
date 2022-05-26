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
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK=6.0.203
  [Host]     : .NET 6.0.5 (6.0.522.21309), X64 RyuJIT
  DefaultJob : .NET 6.0.5 (6.0.522.21309), X64 RyuJIT


|                      Method | BoundCount |      Mean |     Error |    StdDev |
|---------------------------- |----------- |----------:|----------:|----------:|
|            HistogramHotPath |         10 |  55.07 ns |  0.664 ns |  1.091 ns |
|  HistogramWith1LabelHotPath |         10 | 108.66 ns |  1.324 ns |  1.174 ns |
| HistogramWith3LabelsHotPath |         10 | 193.79 ns |  3.261 ns |  3.349 ns |
| HistogramWith5LabelsHotPath |         10 | 279.44 ns |  4.608 ns |  3.848 ns |
| HistogramWith7LabelsHotPath |         10 | 334.28 ns |  6.650 ns |  5.895 ns |
|            HistogramHotPath |         49 |  68.27 ns |  0.744 ns |  0.581 ns |
|  HistogramWith1LabelHotPath |         49 | 125.55 ns |  2.265 ns |  2.518 ns |
| HistogramWith3LabelsHotPath |         49 | 207.95 ns |  4.023 ns |  3.951 ns |
| HistogramWith5LabelsHotPath |         49 | 293.45 ns |  5.689 ns |  5.842 ns |
| HistogramWith7LabelsHotPath |         49 | 362.19 ns |  5.610 ns |  6.003 ns |
|            HistogramHotPath |         50 |  69.64 ns |  1.422 ns |  1.330 ns |
|  HistogramWith1LabelHotPath |         50 | 118.15 ns |  2.040 ns |  1.908 ns |
| HistogramWith3LabelsHotPath |         50 | 250.31 ns |  4.617 ns |  9.326 ns |
| HistogramWith5LabelsHotPath |         50 | 335.31 ns |  3.904 ns |  3.461 ns |
| HistogramWith7LabelsHotPath |         50 | 398.02 ns |  6.815 ns |  6.374 ns |
|            HistogramHotPath |       1000 |  94.05 ns |  1.890 ns |  2.100 ns |
|  HistogramWith1LabelHotPath |       1000 | 148.57 ns |  2.055 ns |  1.822 ns |
| HistogramWith3LabelsHotPath |       1000 | 661.78 ns | 11.599 ns | 20.314 ns |
| HistogramWith5LabelsHotPath |       1000 | 761.54 ns | 15.049 ns | 16.727 ns |
| HistogramWith7LabelsHotPath |       1000 | 830.14 ns | 16.063 ns | 17.853 ns |
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

        // Note: Values related to `HistogramBuckets.DefaultHistogramCountForBinarySearch`
        [Params(10, 49, 50, 1000)]
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
