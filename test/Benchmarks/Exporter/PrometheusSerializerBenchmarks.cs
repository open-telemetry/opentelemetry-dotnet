// <copyright file="PrometheusSerializerBenchmarks.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using OpenTelemetry;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

namespace Benchmarks.Exporter
{
    public class PrometheusSerializerBenchmarks
    {
        private readonly List<Metric> metrics = new();
        private readonly byte[] buffer = new byte[85000];
        private Meter meter;
        private MeterProvider meterProvider;

        [Params(1, 1000, 10000)]
        public int NumberOfSerializeCalls { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.meter = new Meter(Utils.GetCurrentMethodName());
            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(this.meter.Name)
                .AddInMemoryExporter(this.metrics)
                .Build();

            var counter = this.meter.CreateCounter<long>("counter_name_1", "long", "counter_name_1_description");
            counter.Add(18, new("label1", "value1"), new("label2", "value2"));

            var gauge = this.meter.CreateObservableGauge("gauge_name_1", () => 18.0D, "long", "gauge_name_1_description");

            var histogram = this.meter.CreateHistogram<long>("histogram_name_1", "long", "histogram_name_1_description");
            histogram.Record(100, new("label1", "value1"), new("label2", "value2"));

            this.meterProvider.ForceFlush();
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.meter?.Dispose();
            this.meterProvider?.Dispose();
        }

        // TODO: this has a dependency on https://github.com/open-telemetry/opentelemetry-dotnet/issues/2361
        [Benchmark]
        public void WriteMetric()
        {
            for (int i = 0; i < this.NumberOfSerializeCalls; i++)
            {
                int cursor = 0;
                foreach (var metric in this.metrics)
                {
                    cursor = PrometheusSerializer.WriteMetric(this.buffer, cursor, metric);
                }
            }
        }
    }
}
