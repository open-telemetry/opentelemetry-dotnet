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
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
|                 Method | BoundCount |     Mean |    Error |   StdDev | Allocated |
|----------------------- |----------- |---------:|---------:|---------:|----------:|
|   HistogramLongHotPath |         10 | 43.85 ns | 0.151 ns | 0.134 ns |         - |
| HistogramDoubleHotPath |         10 | 42.62 ns | 0.428 ns | 0.400 ns |         - |
|   HistogramLongHotPath |         20 | 46.81 ns | 0.229 ns | 0.214 ns |         - |
| HistogramDoubleHotPath |         20 | 44.97 ns | 0.106 ns | 0.099 ns |         - |
|   HistogramLongHotPath |         50 | 58.76 ns | 0.179 ns | 0.150 ns |         - |
| HistogramDoubleHotPath |         50 | 53.16 ns | 0.168 ns | 0.149 ns |         - |
|   HistogramLongHotPath |        100 | 69.91 ns | 1.021 ns | 0.955 ns |         - |
| HistogramDoubleHotPath |        100 | 64.25 ns | 0.088 ns | 0.082 ns |         - |
*/

namespace Benchmarks.Metrics
{
    [MemoryDiagnoser]
    public class HistogramBenchmarks
    {
        private const int MaxValue = 1000;
        private readonly Random random = new();
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
            this.histogramLong?.Record(this.random.Next(MaxValue));
        }

        [Benchmark]
        public void HistogramDoubleHotPath()
        {
            this.histogramDouble?.Record(this.random.NextDouble() * MaxValue);
        }
    }
}
