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
Intel Core i7-9700 CPU 3.00GHz, 1 CPU, 8 logical and 8 physical cores
.NET SDK=6.0.200
  [Host]     : .NET 6.0.2 (6.0.222.6406), X64 RyuJIT
  DefaultJob : .NET 6.0.2 (6.0.222.6406), X64 RyuJIT


|                      Method | BoundCount |      Mean |    Error |   StdDev | Allocated |
|---------------------------- |----------- |----------:|---------:|---------:|----------:|
|            HistogramHotPath |         10 |  45.27 ns | 0.384 ns | 0.359 ns |         - |
|  HistogramWith1LabelHotPath |         10 |  89.99 ns | 0.373 ns | 0.312 ns |         - |
| HistogramWith3LabelsHotPath |         10 | 185.34 ns | 3.184 ns | 3.667 ns |         - |
| HistogramWith5LabelsHotPath |         10 | 266.69 ns | 1.391 ns | 1.301 ns |         - |
| HistogramWith7LabelsHotPath |         10 | 323.20 ns | 1.834 ns | 1.531 ns |         - |
|            HistogramHotPath |         20 |  48.69 ns | 0.347 ns | 0.307 ns |         - |
|  HistogramWith1LabelHotPath |         20 |  93.84 ns | 0.696 ns | 0.651 ns |         - |
| HistogramWith3LabelsHotPath |         20 | 189.82 ns | 1.208 ns | 1.071 ns |         - |
| HistogramWith5LabelsHotPath |         20 | 269.23 ns | 2.027 ns | 1.693 ns |         - |
| HistogramWith7LabelsHotPath |         20 | 329.92 ns | 1.272 ns | 1.128 ns |         - |
|            HistogramHotPath |         50 |  55.73 ns | 0.339 ns | 0.317 ns |         - |
|  HistogramWith1LabelHotPath |         50 | 100.38 ns | 0.455 ns | 0.425 ns |         - |
| HistogramWith3LabelsHotPath |         50 | 200.02 ns | 1.011 ns | 0.844 ns |         - |
| HistogramWith5LabelsHotPath |         50 | 279.94 ns | 1.595 ns | 1.492 ns |         - |
| HistogramWith7LabelsHotPath |         50 | 346.88 ns | 1.064 ns | 0.943 ns |         - |
|            HistogramHotPath |        100 |  66.39 ns | 0.167 ns | 0.148 ns |         - |
|  HistogramWith1LabelHotPath |        100 | 114.98 ns | 1.340 ns | 1.253 ns |         - |
| HistogramWith3LabelsHotPath |        100 | 220.52 ns | 1.723 ns | 1.528 ns |         - |
| HistogramWith5LabelsHotPath |        100 | 299.10 ns | 1.950 ns | 1.629 ns |         - |
| HistogramWith7LabelsHotPath |        100 | 356.25 ns | 2.153 ns | 1.798 ns |         - |
*/

namespace Benchmarks.Metrics
{
    [MemoryDiagnoser]
    public class HistogramBenchmarks
    {
        private const int MaxValue = 1000;
        private Random random = new();
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
