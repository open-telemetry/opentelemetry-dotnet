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
using System.Threading;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.22000
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=6.0.200
  [Host]     : .NET 6.0.2 (6.0.222.6406), X64 RyuJIT
  DefaultJob : .NET 6.0.2 (6.0.222.6406), X64 RyuJIT


|                      Method | BoundCount |      Mean |    Error |   StdDev |    Median | Allocated |
|---------------------------- |----------- |----------:|---------:|---------:|----------:|----------:|
|            HistogramHotPath |         10 |  49.45 ns | 1.013 ns | 1.902 ns |  50.42 ns |         - |
|  HistogramWith1LabelHotPath |         10 |  89.33 ns | 0.323 ns | 0.270 ns |  89.38 ns |         - |
| HistogramWith3LabelsHotPath |         10 | 176.37 ns | 1.693 ns | 1.501 ns | 176.24 ns |         - |
| HistogramWith5LabelsHotPath |         10 | 264.33 ns | 2.254 ns | 1.883 ns | 265.06 ns |         - |
| HistogramWith7LabelsHotPath |         10 | 318.65 ns | 2.808 ns | 2.627 ns | 317.88 ns |         - |
|            HistogramHotPath |         20 |  47.66 ns | 0.154 ns | 0.129 ns |  47.66 ns |         - |
|  HistogramWith1LabelHotPath |         20 |  88.38 ns | 0.391 ns | 0.346 ns |  88.43 ns |         - |
| HistogramWith3LabelsHotPath |         20 | 184.54 ns | 1.977 ns | 1.849 ns | 185.45 ns |         - |
| HistogramWith5LabelsHotPath |         20 | 271.21 ns | 3.180 ns | 2.655 ns | 271.93 ns |         - |
| HistogramWith7LabelsHotPath |         20 | 320.97 ns | 1.790 ns | 1.675 ns | 320.62 ns |         - |
|            HistogramHotPath |         50 |  54.83 ns | 0.279 ns | 0.247 ns |  54.79 ns |         - |
|  HistogramWith1LabelHotPath |         50 |  95.65 ns | 0.204 ns | 0.191 ns |  95.57 ns |         - |
| HistogramWith3LabelsHotPath |         50 | 197.58 ns | 1.124 ns | 1.052 ns | 197.73 ns |         - |
| HistogramWith5LabelsHotPath |         50 | 275.50 ns | 1.078 ns | 0.955 ns | 275.59 ns |         - |
| HistogramWith7LabelsHotPath |         50 | 331.57 ns | 2.632 ns | 2.462 ns | 331.11 ns |         - |
|            HistogramHotPath |        100 |  66.91 ns | 0.247 ns | 0.206 ns |  66.90 ns |         - |
|  HistogramWith1LabelHotPath |        100 | 108.24 ns | 1.120 ns | 0.875 ns | 108.29 ns |         - |
| HistogramWith3LabelsHotPath |        100 | 207.67 ns | 0.610 ns | 0.476 ns | 207.70 ns |         - |
| HistogramWith5LabelsHotPath |        100 | 292.93 ns | 1.694 ns | 1.502 ns | 292.97 ns |         - |
| HistogramWith7LabelsHotPath |        100 | 350.70 ns | 4.753 ns | 4.214 ns | 349.39 ns |         - |
*/

namespace Benchmarks.Metrics
{
    [MemoryDiagnoser]
    public class HistogramBenchmarks
    {
        private const int MaxValue = 1000;
        private static readonly ThreadLocal<Random> ThreadLocalRandom = new(() => new Random());
        private static readonly Random Random = ThreadLocalRandom.Value;
        private Histogram<long> histogram;
        private MeterProvider provider;
        private Meter meter;
        private double[] bounds;
        private string[] dimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };

        [Params(10, 20, 50, 100)]
        public int BoundCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.meter = new Meter(Utils.GetCurrentMethodName());
            this.histogram = this.meter.CreateHistogram<long>("histogramLong");

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
                    metricReaderOptions.MetricReaderType = MetricReaderType.Periodic;
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
            this.histogram.Record(Random.Next(MaxValue));
        }

        [Benchmark]
        public void HistogramWith1LabelHotPath()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[Random.Next(0, 2)]);
            this.histogram.Record(Random.Next(MaxValue), tag1);
        }

        [Benchmark]
        public void HistogramWith3LabelsHotPath()
        {
            var tag1 = new KeyValuePair<string, object>("DimName1", this.dimensionValues[Random.Next(0, 10)]);
            var tag2 = new KeyValuePair<string, object>("DimName2", this.dimensionValues[Random.Next(0, 10)]);
            var tag3 = new KeyValuePair<string, object>("DimName3", this.dimensionValues[Random.Next(0, 10)]);
            this.histogram.Record(Random.Next(MaxValue), tag1, tag2, tag3);
        }

        [Benchmark]
        public void HistogramWith5LabelsHotPath()
        {
            var tags = new TagList
            {
                { "DimName1", this.dimensionValues[Random.Next(0, 2)] },
                { "DimName2", this.dimensionValues[Random.Next(0, 2)] },
                { "DimName3", this.dimensionValues[Random.Next(0, 5)] },
                { "DimName4", this.dimensionValues[Random.Next(0, 5)] },
                { "DimName5", this.dimensionValues[Random.Next(0, 10)] },
            };
            this.histogram.Record(Random.Next(MaxValue), tags);
        }

        [Benchmark]
        public void HistogramWith7LabelsHotPath()
        {
            var tags = new TagList
            {
                { "DimName1", this.dimensionValues[Random.Next(0, 2)] },
                { "DimName2", this.dimensionValues[Random.Next(0, 2)] },
                { "DimName3", this.dimensionValues[Random.Next(0, 5)] },
                { "DimName4", this.dimensionValues[Random.Next(0, 5)] },
                { "DimName5", this.dimensionValues[Random.Next(0, 5)] },
                { "DimName6", this.dimensionValues[Random.Next(0, 2)] },
                { "DimName7", this.dimensionValues[Random.Next(0, 1)] },
            };
            this.histogram.Record(Random.Next(MaxValue), tags);
        }
    }
}
