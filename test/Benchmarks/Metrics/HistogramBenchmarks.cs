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
BenchmarkDotNet=v0.13.3, OS=Windows 11 (10.0.22621.963)
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=7.0.101
  [Host]     : .NET 7.0.1 (7.0.122.56804), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.1 (7.0.122.56804), X64 RyuJIT AVX2


|                      Method | BoundCount |      Mean |     Error |    StdDev | Allocated |
|---------------------------- |----------- |----------:|----------:|----------:|----------:|
|            HistogramHotPath |         10 |  47.80 ns |  0.111 ns |  0.098 ns |         - |
|  HistogramWith1LabelHotPath |         10 |  99.18 ns |  0.448 ns |  0.419 ns |         - |
| HistogramWith3LabelsHotPath |         10 | 189.60 ns |  0.872 ns |  0.815 ns |         - |
| HistogramWith5LabelsHotPath |         10 | 263.10 ns |  2.813 ns |  2.494 ns |         - |
| HistogramWith7LabelsHotPath |         10 | 309.09 ns |  1.603 ns |  1.421 ns |         - |
|            HistogramHotPath |         49 |  61.80 ns |  0.282 ns |  0.235 ns |         - |
|  HistogramWith1LabelHotPath |         49 | 111.22 ns |  0.347 ns |  0.290 ns |         - |
| HistogramWith3LabelsHotPath |         49 | 203.53 ns |  2.263 ns |  2.006 ns |         - |
| HistogramWith5LabelsHotPath |         49 | 278.45 ns |  2.401 ns |  2.005 ns |         - |
| HistogramWith7LabelsHotPath |         49 | 331.96 ns |  4.160 ns |  3.892 ns |         - |
|            HistogramHotPath |         50 |  62.30 ns |  0.385 ns |  0.342 ns |         - |
|  HistogramWith1LabelHotPath |         50 | 108.47 ns |  0.132 ns |  0.111 ns |         - |
| HistogramWith3LabelsHotPath |         50 | 237.33 ns |  3.291 ns |  3.079 ns |         - |
| HistogramWith5LabelsHotPath |         50 | 316.26 ns |  1.989 ns |  1.763 ns |         - |
| HistogramWith7LabelsHotPath |         50 | 359.67 ns |  2.359 ns |  2.091 ns |         - |
|            HistogramHotPath |       1000 |  82.72 ns |  0.366 ns |  0.286 ns |         - |
|  HistogramWith1LabelHotPath |       1000 | 134.84 ns |  1.502 ns |  1.331 ns |         - |
| HistogramWith3LabelsHotPath |       1000 | 555.89 ns |  3.501 ns |  2.923 ns |         - |
| HistogramWith5LabelsHotPath |       1000 | 645.98 ns | 11.965 ns |  9.991 ns |         - |
| HistogramWith7LabelsHotPath |       1000 | 700.72 ns | 13.467 ns | 12.597 ns |         - |
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
