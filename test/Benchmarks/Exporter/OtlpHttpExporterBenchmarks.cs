// <copyright file="OtlpHttpExporterBenchmarks.cs" company="OpenTelemetry Authors">
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

extern alias OpenTelemetryProtocol;

using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using OpenTelemetry;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;
using OpenTelemetryProtocol::OpenTelemetry.Exporter;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetryProtocol::OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;

namespace Benchmarks.Exporter
{
    public class OtlpHttpExporterBenchmarks
    {
        private readonly byte[] buffer = new byte[1024 * 1024];
        private IDisposable server;
        private string serverHost;
        private int serverPort;
        private OtlpTraceExporter exporter;
        private Activity activity;
        private CircularBuffer<Activity> activityBatch;

        [Params(1, 10, 100)]
        public int NumberOfBatches { get; set; }

        [Params(10000)]
        public int NumberOfSpans { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.server = TestHttpServer.RunServer(
                (ctx) =>
                {
                    using (Stream receiveStream = ctx.Request.InputStream)
                    {
                        while (true)
                        {
                            if (receiveStream.Read(this.buffer, 0, this.buffer.Length) == 0)
                            {
                                break;
                            }
                        }
                    }

                    ctx.Response.StatusCode = 200;
                    ctx.Response.OutputStream.Close();
                },
                out this.serverHost,
                out this.serverPort);

            var options = new OtlpExporterOptions
            {
                Endpoint = new Uri($"http://{this.serverHost}:{this.serverPort}"),
            };
            this.exporter = new OtlpTraceExporter(
                options,
                new SdkLimitOptions(),
                new OtlpHttpTraceExportClient(options, options.HttpClientFactory()));

            this.activity = ActivityHelper.CreateTestActivity();
            this.activityBatch = new CircularBuffer<Activity>(this.NumberOfSpans);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.exporter.Shutdown();
            this.exporter.Dispose();
            this.server.Dispose();
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
    }
}
