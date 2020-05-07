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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Internal.Test;
using OpenTelemetry.Exporter.Zipkin;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;

namespace Benchmarks.Exporter
{
    [MemoryDiagnoser]
#if !NET462
    [ThreadingDiagnoser]
#endif
    public class ZipkinExporterBenchmarks
    {
        [Params(2000, 5000)]
        public int NumberOfSpans { get; set; }

        private SpanData testSpan;

        private IDisposable server;
        private string serverHost;
        private int serverPort;

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.testSpan = this.CreateTestSpan();
            this.server = TestHttpServer.RunServer(
                (ctx) =>
                {
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
        public async Task ZipkinExporter_ExportAsync()
        {
            var zipkinExporter = new ZipkinTraceExporter(
                new ZipkinTraceExporterOptions
                {
                    Endpoint = new Uri($"http://{this.serverHost}:{this.serverPort}"),
                });

            var spans = new List<SpanData>();
            for (int i = 0; i < this.NumberOfSpans; i++)
            {
                spans.Add(this.testSpan);
            }

            await zipkinExporter.ExportAsync(spans, CancellationToken.None).ConfigureAwait(false);
        }

        private SpanData CreateTestSpan()
        {
            var startTimestamp = new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var endTimestamp = startTimestamp.AddSeconds(60);
            var eventTimestamp = new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero);

            var traceId = ActivityTraceId.CreateFromString("e8ea7e9ac72de94e91fabc613f9686b2".AsSpan());
            var spanId = ActivitySpanId.CreateFromString("6a69db47429ea340".AsSpan());
            var parentSpanId = ActivitySpanId.CreateFromBytes(new byte[] { 12, 23, 34, 45, 56, 67, 78, 89 });
            var attributes = new Dictionary<string, object>
            {
                { "stringKey", "value"},
                { "longKey", 1L},
                { "longKey2", 1 },
                { "doubleKey", 1D},
                { "doubleKey2", 1F},
                { "boolKey", true},
            };
            var events = new List<Event>
            {
                new Event(
                    "Event1",
                    eventTimestamp,
                    new Dictionary<string, object>
                    {
                        { "key", "value" },
                    }
                ),
                new Event(
                    "Event2",
                    eventTimestamp,
                    new Dictionary<string, object>
                    {
                        { "key", "value" },
                    }
                ),
            };

            var linkedSpanId = ActivitySpanId.CreateFromString("888915b6286b9c41".AsSpan());

            var link = new Link(new SpanContext(
                    traceId,
                    linkedSpanId,
                    ActivityTraceFlags.Recorded));

            return new SpanData(
                "Name",
                new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded),
                parentSpanId,
                SpanKind.Client,
                startTimestamp,
                attributes,
                events,
                new[] { link, },
                null,
                Status.Ok,
                endTimestamp);
        }
    }
}
