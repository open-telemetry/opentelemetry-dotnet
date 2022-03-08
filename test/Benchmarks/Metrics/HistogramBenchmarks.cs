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
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
BenchmarkDotNet=v0.13.1, OS=Windows 10.0.19044.1566 (21H2)
AMD Ryzen 9 3900X, 1 CPU, 24 logical and 12 physical cores
.NET SDK=6.0.200
  [Host]     : .NET 6.0.2 (6.0.222.6406), X64 RyuJIT
  DefaultJob : .NET 6.0.2 (6.0.222.6406), X64 RyuJIT


|                 Method | BoundCount |     Mean |    Error |   StdDev | Allocated |
|----------------------- |----------- |---------:|---------:|---------:|----------:|
|   HistogramLongHotPath |         10 | 53.30 ns | 0.713 ns | 0.667 ns |         - |
| HistogramDoubleHotPath |         10 | 53.18 ns | 0.267 ns | 0.236 ns |         - |
|   HistogramLongHotPath |         20 | 56.39 ns | 0.487 ns | 0.431 ns |         - |
| HistogramDoubleHotPath |         20 | 55.08 ns | 0.236 ns | 0.209 ns |         - |
|   HistogramLongHotPath |         50 | 61.95 ns | 0.318 ns | 0.265 ns |         - |
| HistogramDoubleHotPath |         50 | 60.00 ns | 0.201 ns | 0.188 ns |         - |
|   HistogramLongHotPath |        100 | 69.57 ns | 0.299 ns | 0.279 ns |         - |
| HistogramDoubleHotPath |        100 | 68.41 ns | 0.229 ns | 0.214 ns |         - |
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
            var reader = new PeriodicExportingMetricReader(new InMemoryExporter<Metric>(exportedItems), 1000);

            this.provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(this.meter.Name)
                .AddReader(reader)
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
