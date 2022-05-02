// <copyright file="JaegerExporterBenchmarks.cs" company="OpenTelemetry Authors">
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

extern alias Jaeger;

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using Jaeger::OpenTelemetry.Exporter;
using Jaeger::OpenTelemetry.Exporter.Jaeger.Implementation;
using Jaeger::Thrift.Protocol;
using OpenTelemetry;
using OpenTelemetry.Internal;

namespace Benchmarks.Exporter
{
    public class JaegerExporterBenchmarks
    {
        private Activity activity;
        private CircularBuffer<Activity> activityBatch;

        [Params(1, 10, 100)]
        public int NumberOfBatches { get; set; }

        [Params(10000)]
        public int NumberOfSpans { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.activity = ActivityHelper.CreateTestActivity();
            this.activityBatch = new CircularBuffer<Activity>(this.NumberOfSpans);
        }

        [Benchmark]
        public void JaegerExporter_Batching()
        {
            using JaegerExporter exporter = new JaegerExporter(
                new JaegerExporterOptions(),
                new TCompactProtocol.Factory(),
                new NoopJaegerClient())
            {
                Process = new Jaeger::OpenTelemetry.Exporter.Jaeger.Implementation.Process("TestService"),
            };

            for (int i = 0; i < this.NumberOfBatches; i++)
            {
                for (int c = 0; c < this.NumberOfSpans; c++)
                {
                    this.activityBatch.Add(this.activity);
                }

                exporter.Export(new Batch<Activity>(this.activityBatch, this.NumberOfSpans));
            }

            exporter.Shutdown();
        }

        private sealed class NoopJaegerClient : IJaegerClient
        {
            public bool Connected => true;

            public void Close()
            {
            }

            public void Connect()
            {
            }

            public void Dispose()
            {
            }

            public int Send(byte[] buffer, int offset, int count)
            {
                return count;
            }
        }
    }
}
