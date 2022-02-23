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
using System.Diagnostics.Metrics;
using System.Threading;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19044.1503 (21H2)
AMD Ryzen 9 3900X, 1 CPU, 24 logical and 12 physical cores
.NET SDK=6.0.101
  [Host]     : .NET 6.0.1 (6.0.121.56705), X64 RyuJIT
  DefaultJob : .NET 6.0.1 (6.0.121.56705), X64 RyuJIT


|                 Method | BoundCount |     Mean |    Error |   StdDev | Allocated |
|----------------------- |----------- |---------:|---------:|---------:|----------:|
|   HistogramLongHotPath |         10 | 55.44 ns | 0.211 ns | 0.187 ns |         - |
| HistogramDoubleHotPath |         10 | 55.69 ns | 0.129 ns | 0.107 ns |         - |
|   HistogramLongHotPath |         20 | 57.71 ns | 0.297 ns | 0.278 ns |         - |
| HistogramDoubleHotPath |         20 | 58.10 ns | 0.117 ns | 0.110 ns |         - |
|   HistogramLongHotPath |         50 | 65.21 ns | 0.356 ns | 0.333 ns |         - |
| HistogramDoubleHotPath |         50 | 66.34 ns | 0.381 ns | 0.356 ns |         - |
|   HistogramLongHotPath |        100 | 79.49 ns | 0.804 ns | 0.753 ns |         - |
| HistogramDoubleHotPath |        100 | 85.77 ns | 0.947 ns | 0.840 ns |         - |
*/

namespace Benchmarks.Metrics
{
    [MemoryDiagnoser]
    public class HistogramBenchmarks
    {
        private const int MaxValue = 1000;
        private static readonly ThreadLocal<Random> ThreadLocalRandom = new(() => new Random());
        private Histogram<long> histogramLong;
        private Histogram<double> histogramDouble;
        private MeterProvider provider;
        private Meter meter;
        private double[] bounds;

        [Params(10, 20, 50, 100)]
        public int BoundCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.meter = new Meter(Utils.GetCurrentMethodName());

            this.histogramLong = this.meter.CreateHistogram<long>("histogramLong");
            this.histogramDouble = this.meter.CreateHistogram<double>("histogramDouble");

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
                .AddView(this.histogramLong.Name, new ExplicitBucketHistogramConfiguration() { Boundaries = this.bounds })
                .AddView(this.histogramDouble.Name, new ExplicitBucketHistogramConfiguration() { Boundaries = this.bounds })
                .Build();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.meter?.Dispose();
            this.provider?.Dispose();
        }

        [Benchmark]
        public void HistogramLongHotPath()
        {
            var random = ThreadLocalRandom.Value;
            this.histogramLong?.Record(random.Next(MaxValue));
        }

        [Benchmark]
        public void HistogramDoubleHotPath()
        {
            var random = ThreadLocalRandom.Value;
            this.histogramDouble?.Record(random.NextDouble() * MaxValue);
        }
    }
}
