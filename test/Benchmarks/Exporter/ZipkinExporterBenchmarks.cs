// <copyright file="ZipkinExporterBenchmarks.cs" company="OpenTelemetry Authors">
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

extern alias Zipkin;

using System;
using System.Diagnostics;
using System.IO;
using BenchmarkDotNet.Attributes;
using Benchmarks.Helper;
using OpenTelemetry;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;
using Zipkin::OpenTelemetry.Exporter;

namespace Benchmarks.Exporter
{
#if !NET462
    [ThreadingDiagnoser]
#endif
    public class ZipkinExporterBenchmarks
    {
        private readonly byte[] buffer = new byte[4096];
        private Activity activity;
        private CircularBuffer<Activity> activityBatch;
        private IDisposable server;
        private string serverHost;
        private int serverPort;

        [Params(1, 10, 100)]
        public int NumberOfBatches { get; set; }

        [Params(10000)]
        public int NumberOfSpans { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.activity = ActivityHelper.CreateTestActivity();
            this.activityBatch = new CircularBuffer<Activity>(this.NumberOfSpans);
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
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.server.Dispose();
        }

        [Benchmark]
        public void ZipkinExporter_Batching()
        {
            var exporter = new ZipkinExporter(
                new ZipkinExporterOptions
                {
                    Endpoint = new Uri($"http://{this.serverHost}:{this.serverPort}"),
                });

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
    }
}
