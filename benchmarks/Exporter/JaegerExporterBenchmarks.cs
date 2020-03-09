// <copyright file="JaegerExporterBenchmarks.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using OpenTelemetry.Exporter.Jaeger;
using OpenTelemetry.Exporter.Jaeger.Implementation;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;
using Thrift.Transport;

namespace Benchmarks.Exporter
{
    [MemoryDiagnoser]
#if !NET462
    [ThreadingDiagnoser]
#endif
    public class JaegerExporterBenchmarks
    {
        [Params(1, 10, 100)]
        public int NumberOfBatches { get; set; }

        [Params(10000)]
        public int NumberOfSpans { get; set; }

        private SpanData testSpan;

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.testSpan = this.CreateTestSpan();
        }

        [Benchmark]
        public async Task JaegerExporter_Batching()
        {
            using var jaegerUdpBatcher = new JaegerUdpBatcher(
                new JaegerExporterOptions(),
                new BlackHoleTransport());
            jaegerUdpBatcher.Process = new OpenTelemetry.Exporter.Jaeger.Process("TestService");

            for (int i = 0; i < this.NumberOfBatches; i++)
            {
                for (int c = 0; c < this.NumberOfSpans; c++)
                {
                    await jaegerUdpBatcher.AppendAsync(this.testSpan, CancellationToken.None).ConfigureAwait(false);
                }

                await jaegerUdpBatcher.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        private class BlackHoleTransport : TTransport
        {
            public override bool IsOpen => true;

            public override async Task OpenAsync(CancellationToken cancellationToken)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
                }
            }

            public override void Close()
            {
                // do nothing
            }

            public override ValueTask<int> ReadAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public override async Task WriteAsync(byte[] buffer, int offset, int length, CancellationToken cancellationToken)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
                }
            }

            public override async Task FlushAsync(CancellationToken cancellationToken)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    await Task.FromCanceled(cancellationToken).ConfigureAwait(false);
                }
            }

            protected override void Dispose(bool disposing)
            {
            }
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
