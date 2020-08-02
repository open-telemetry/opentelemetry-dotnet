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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Exporter.Jaeger;
using OpenTelemetry.Exporter.Jaeger.Implementation;
using Thrift.Transport;

namespace Benchmarks.Exporter
{
    [MemoryDiagnoser]
#if !NET462
    [ThreadingDiagnoser]
#endif
    public class JaegerExporterBenchmarks
    {
        private readonly byte[] buffer = new byte[1024];
        private UdpClient jaegerServer;
        private JaegerUdpClient jaegerClient;
        private Activity testActivity;

        [Params(1, 10, 100)]
        public int NumberOfBatches { get; set; }

        [Params(10000)]
        public int NumberOfSpans { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.testActivity = this.CreateTestActivity();

            this.jaegerServer = new UdpClient(new IPEndPoint(IPAddress.Any, 10018));
            ThreadPool.QueueUserWorkItem(this.ReceivedData);

            this.jaegerClient = new JaegerUdpClient();
            this.jaegerClient.Connect("localhost", 10018);
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            this.jaegerServer.Dispose();
            this.jaegerClient.Dispose();
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
                    await jaegerUdpBatcher.AppendAsync(this.testActivity, CancellationToken.None).ConfigureAwait(false);
                }

                await jaegerUdpBatcher.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        [Benchmark]
        public async Task JaegerUdpClient_SendAsync()
        {
            for (int i = 0; i < this.NumberOfSpans * this.NumberOfBatches; i++)
            {
                await this.jaegerClient.SendAsync(this.buffer).ConfigureAwait(false);
            }
        }

        private void ReceivedData(object state)
        {
            var buffer = new byte[1024];

            while (true)
            {
                if (this.jaegerServer.Client.Receive(buffer) == 0)
                {
                    return;
                }
            }
        }

        private Activity CreateTestActivity()
        {
            var startTimestamp = new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var endTimestamp = startTimestamp.AddSeconds(60);
            var eventTimestamp = new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero);

            var traceId = ActivityTraceId.CreateFromString("e8ea7e9ac72de94e91fabc613f9686b2".AsSpan());
            var spanId = ActivitySpanId.CreateFromString("6a69db47429ea340".AsSpan());
            var parentSpanId = ActivitySpanId.CreateFromBytes(new byte[] { 12, 23, 34, 45, 56, 67, 78, 89 });
            var attributes = new Dictionary<string, object>
            {
                { "stringKey", "value" },
                { "longKey", 1L },
                { "longKey2", 1 },
                { "doubleKey", 1D },
                { "doubleKey2", 1F },
                { "boolKey", true },
            };
            var events = new List<ActivityEvent>
            {
                new ActivityEvent(
                    "Event1",
                    eventTimestamp,
                    new ActivityTagsCollection(new Dictionary<string, object>
                    {
                        { "key", "value" },
                    })),
                new ActivityEvent(
                    "Event2",
                    eventTimestamp,
                    new ActivityTagsCollection(new Dictionary<string, object>
                    {
                        { "key", "value" },
                    })),
            };

            var linkedSpanId = ActivitySpanId.CreateFromString("888915b6286b9c41".AsSpan());

            var activitySource = new ActivitySource(nameof(this.CreateTestActivity));

            var tags = attributes.Select(kvp => new KeyValuePair<string, object>(kvp.Key, kvp.Value.ToString()));

            var links = new[]
            {
                new ActivityLink(new ActivityContext(
                    traceId,
                    linkedSpanId,
                    ActivityTraceFlags.Recorded)),
            };

            return activitySource.StartActivity(
                "Name",
                ActivityKind.Client,
                parentContext: new ActivityContext(traceId, parentSpanId, ActivityTraceFlags.Recorded),
                tags,
                links,
                startTime: startTimestamp);
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
    }
}
