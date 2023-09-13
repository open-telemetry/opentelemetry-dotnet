// <copyright file="DataPointReclaimBenchmarks.cs" company="OpenTelemetry Authors">
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
// * Summary *

BenchmarkDotNet=v0.13.3, OS=Windows 10 (10.0.19045.3208)
11th Gen Intel Core i7-11800H 2.30GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=7.0.203
  [Host]     : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2
  DefaultJob : .NET 7.0.5 (7.0.523.17405), X64 RyuJIT AVX2


|                              Method | Reclaim |     Mean |    Error |   StdDev |
|------------------------------------ |-------- |---------:|---------:|---------:|
| ObservableInstrumentReclaimOverhead |   False | 35.34 us | 0.193 us | 0.161 us |
| ObservableInstrumentReclaimOverhead |    True | 38.28 us | 0.274 us | 0.229 us |
*/

namespace Benchmarks.Metrics
{
    public class DataPointReclaimBenchmarks
    {
        private const int ReclaimRange = 10; // [-N, N] range of measurements to go over the measurement limit. (Up to 2 * ReclaimRange can be reclaimed per iteration)
        private const int ReclaimOverage = ReclaimRange; // How many points to allow over the limit.
        private const int NumberOfMeasurements = 500; // Maximum number of measurements

        private readonly Random random = new();

        private ObservableGauge<long> gauge;
        private Measurement<long>[] measurements;
        private MeterProvider provider;
        private Meter meter;

        [Params(false, true)]
        public bool Reclaim { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.meter = new Meter(Utils.GetCurrentMethodName());

            this.measurements = new Measurement<long>[NumberOfMeasurements + ReclaimOverage];
            for (int i = 0; i < this.measurements.Length; ++i)
            {
                this.measurements[i] = new Measurement<long>(1, new[] { new KeyValuePair<string, object>("n", i) });
            }

            List<Metric> exportedItems = new();
            this.provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(this.meter.Name) // All instruments from this meter are enabled.
                .SetMaxMetricPointsPerMetricStream(NumberOfMeasurements + 1) // Add an extra metric point to account for 0-tag point.
                .AddInMemoryExporter(exportedItems, metricReaderOptions =>
                {
                    metricReaderOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = (int)TimeSpan.FromDays(5).TotalMilliseconds; // Use Forceflush
                    metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Cumulative; // Doesn't matter for gauges.
                })
                .Build();

            this.gauge = this.meter.CreateObservableGauge("gauge", SelectDatapoints);

            IEnumerable<Measurement<long>> SelectDatapoints()
            {
                var delta = this.random.Next(-ReclaimRange, ReclaimRange); // Always call random even for no reclaim.

                return this.measurements.Take(this.Reclaim ? NumberOfMeasurements + delta : NumberOfMeasurements);
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.meter?.Dispose();
            this.provider?.Dispose();
        }

        [Benchmark]
        public void ObservableInstrumentReclaimOverhead()
        {
            this.provider.ForceFlush(); // Force Collect cycle.
        }
    }
}
