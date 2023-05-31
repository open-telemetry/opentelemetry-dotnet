// <copyright file="ExponentialHistogramBenchmarks.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

/*
BenchmarkDotNet=v0.13.3, OS=macOS 13.2.1 (22D68) [Darwin 22.3.0]
Apple M1 Max, 1 CPU, 10 logical and 10 physical cores
.NET SDK=7.0.101
  [Host]     : .NET 7.0.1 (7.0.122.56804), Arm64 RyuJIT AdvSIMD
  DefaultJob : .NET 7.0.1 (7.0.122.56804), Arm64 RyuJIT AdvSIMD


|           Method | Scale |     Mean |    Error |   StdDev | Allocated |
|----------------- |------ |---------:|---------:|---------:|----------:|
| HistogramHotPath |   -11 | 29.66 ns | 0.181 ns | 0.151 ns |         - |
| HistogramHotPath |    20 | 31.79 ns | 0.091 ns | 0.080 ns |         - |
*/

namespace Benchmarks.Metrics
{
    public class ExponentialHistogramBenchmarks
    {
        private const int MaxValue = 10000;
        private readonly Random random = new();
        private Histogram<long> histogram;
        private MeterProvider provider;
        private Meter meter;

        [Params(-11, 20)]
        public int Scale { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.meter = new Meter(Utils.GetCurrentMethodName());
            this.histogram = this.meter.CreateHistogram<long>("histogram");

            var exportedItems = new List<Metric>();

            this.provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(this.meter.Name)
                .AddInMemoryExporter(exportedItems, metricReaderOptions =>
                {
                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = 1000;
                })
                .AddView("histogram", new Base2ExponentialBucketHistogramConfiguration() { MaxScale = this.Scale })
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
    }
}
