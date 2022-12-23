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


|                      Method | BoundCount |      Mean |     Error |    StdDev |
|---------------------------- |----------- |----------:|----------:|----------:|
|            HistogramHotPath |         10 |  45.19 ns |  0.321 ns |  0.285 ns |
|  HistogramWith1LabelHotPath |         10 |  97.21 ns |  0.129 ns |  0.114 ns |
| HistogramWith3LabelsHotPath |         10 | 179.77 ns |  0.270 ns |  0.239 ns |
| HistogramWith5LabelsHotPath |         10 | 263.30 ns |  2.423 ns |  2.267 ns |
| HistogramWith7LabelsHotPath |         10 | 338.42 ns |  3.121 ns |  2.919 ns |
|            HistogramHotPath |         49 |  56.18 ns |  0.593 ns |  0.554 ns |
|  HistogramWith1LabelHotPath |         49 | 110.60 ns |  0.815 ns |  0.762 ns |
| HistogramWith3LabelsHotPath |         49 | 193.30 ns |  1.048 ns |  0.980 ns |
| HistogramWith5LabelsHotPath |         49 | 281.55 ns |  1.638 ns |  1.532 ns |
| HistogramWith7LabelsHotPath |         49 | 343.88 ns |  2.148 ns |  2.010 ns |
|            HistogramHotPath |         50 |  57.46 ns |  0.264 ns |  0.234 ns |
|  HistogramWith1LabelHotPath |         50 | 121.73 ns |  0.372 ns |  0.348 ns |
| HistogramWith3LabelsHotPath |         50 | 227.95 ns |  1.074 ns |  1.004 ns |
| HistogramWith5LabelsHotPath |         50 | 313.15 ns |  1.068 ns |  0.999 ns |
| HistogramWith7LabelsHotPath |         50 | 377.04 ns |  1.191 ns |  0.930 ns |
|            HistogramHotPath |       1000 |  78.33 ns |  0.441 ns |  0.391 ns |
|  HistogramWith1LabelHotPath |       1000 | 127.57 ns |  0.457 ns |  0.428 ns |
| HistogramWith3LabelsHotPath |       1000 | 494.19 ns |  4.490 ns |  3.980 ns |
| HistogramWith5LabelsHotPath |       1000 | 608.75 ns | 11.306 ns | 10.576 ns |
| HistogramWith7LabelsHotPath |       1000 | 649.16 ns |  3.273 ns |  2.555 ns |
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
