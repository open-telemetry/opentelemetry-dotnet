// <copyright file="OtlpExporterBenchmarks.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Threading;
using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using Grpc.Core;
using OpenTelemetry;
using OpenTelemetry.Exporter.OpenTelemetryProtocol;
using OpenTelemetry.Internal;
using OtlpCollector = Opentelemetry.Proto.Collector.Trace.V1;

namespace Benchmarks.Exporter
{
    [MemoryDiagnoser]
    public class OtlpExporterBenchmarks
    {
        private OtlpExporter exporter;
        private Activity activity;
        private CircularBuffer<Activity> activityBatch;

        [Params(1, 10, 100)]
        public int NumberOfBatches { get; set; }

        [Params(10000)]
        public int NumberOfSpans { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.exporter = new OtlpExporter(
                new OtlpExporterOptions(),
                new NoopTraceServiceClient());
            this.activity = ActivityHelper.CreateTestActivity();
            this.activityBatch = new CircularBuffer<Activity>(this.NumberOfSpans);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.exporter.Shutdown();
            this.exporter.Dispose();
        }

        [Benchmark]
        public void OtlpExporter_Batching()
        {
            for (int i = 0; i < this.NumberOfBatches; i++)
            {
                for (int c = 0; c < this.NumberOfSpans; c++)
                {
                    this.activityBatch.Add(this.activity);
                }

                this.exporter.Export(new Batch<Activity>(this.activityBatch, this.NumberOfSpans));
            }
        }

        private class NoopTraceServiceClient : OtlpCollector.TraceService.ITraceServiceClient
        {
            public OtlpCollector.ExportTraceServiceResponse Export(OtlpCollector.ExportTraceServiceRequest request, Metadata headers = null, DateTime? deadline = null, CancellationToken cancellationToken = default)
            {
                return null;
            }
        }
    }
}
