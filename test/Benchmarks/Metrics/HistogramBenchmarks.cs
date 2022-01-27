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
|                 Method | BoundCount |     Mean |    Error |   StdDev | Allocated |
|----------------------- |----------- |---------:|---------:|---------:|----------:|
|   HistogramLongHotPath |         10 | 65.10 ns | 0.694 ns | 0.649 ns |         - |
| HistogramDoubleHotPath |         10 | 61.06 ns | 0.370 ns | 0.328 ns |         - |
|   HistogramLongHotPath |         20 | 68.32 ns | 1.237 ns | 1.157 ns |         - |
| HistogramDoubleHotPath |         20 | 67.71 ns | 0.753 ns | 0.704 ns |         - |
|   HistogramLongHotPath |         50 | 80.11 ns | 0.864 ns | 0.721 ns |         - |
| HistogramDoubleHotPath |         50 | 75.49 ns | 0.437 ns | 0.409 ns |         - |
|   HistogramLongHotPath |        100 | 90.48 ns | 0.296 ns | 0.262 ns |         - |
| HistogramDoubleHotPath |        100 | 86.93 ns | 0.915 ns | 0.856 ns |         - |
*/

namespace Benchmarks.Metrics
{
    [MemoryDiagnoser]
    public class HistogramBenchmarks
    {
        private const int MaxValue = 1000;
        private static readonly string[] DimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };
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
