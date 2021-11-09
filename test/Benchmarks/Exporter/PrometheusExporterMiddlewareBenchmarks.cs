// <copyright file="PrometheusExporterMiddlewareBenchmarks.cs" company="OpenTelemetry Authors">
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

#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;

namespace Benchmarks.Exporter
{
    [MemoryDiagnoser]
    public class PrometheusExporterMiddlewareBenchmarks
    {
        private Meter meter;
        private MemoryStream responseStream;
        private MeterProvider meterProvider;
        private PrometheusExporter exporter;
        private DefaultHttpContext context;

        [Params(1, 1000, 10000)]
        public int NumberOfExportCalls { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.meter = new Meter(Utils.GetCurrentMethodName());
            this.responseStream = new MemoryStream(1024 * 1024);

            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(this.meter.Name)
                .AddPrometheusExporter()
                .Build();

            var counter = this.meter.CreateCounter<long>("counter_name_1", "long", "counter_name_1_description");
            counter.Add(18, new KeyValuePair<string, object>("label1", "value1"), new KeyValuePair<string, object>("label2", "value2"));

            var gauge = this.meter.CreateObservableGauge("gauge_name_1", () => 18.0D, "long", "gauge_name_1_description");

            var histogram = this.meter.CreateHistogram<long>("histogram_name_1", "long", "histogram_name_1_description");
            histogram.Record(100, new KeyValuePair<string, object>("label1", "value1"), new KeyValuePair<string, object>("label2", "value2"));

            this.context = new DefaultHttpContext();
            this.context.Response.Body = this.responseStream;

            if (!this.meterProvider.TryFindExporter(out this.exporter))
            {
                throw new InvalidOperationException("PrometheusExporter could not be found on MeterProvider.");
            }

            this.exporter.Collect(Timeout.Infinite);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.meter?.Dispose();
            this.responseStream?.Dispose();
            this.meterProvider?.Dispose();
        }

        /* TODO: revisit this after PrometheusExporter race condition is solved
        [Benchmark]
        public async Task WriteMetricsToResponse()
        {
            this.responseStream.Position = 0;

            for (int i = 0; i < this.NumberOfExportCalls; i++)
            {
                await PrometheusExporterMiddleware.WriteMetricsToResponse(this.exporter, this.context.Response).ConfigureAwait(false);
            }
        }
        */
    }
}
#endif
