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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Exporter.Zipkin;
using OpenTelemetry.Internal.Test;

namespace Benchmarks.Exporter
{
    [MemoryDiagnoser]
#if !NET462
    [ThreadingDiagnoser]
#endif
    public class ZipkinExporterBenchmarks
    {
        private Activity testActivity;
        private IDisposable server;
        private string serverHost;
        private int serverPort;

        [Params(2000, 5000)]
        public int NumberOfActivities { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.testActivity = this.CreateTestActivity();
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
            var zipkinExporter = new ZipkinExporter(
                new ZipkinExporterOptions
                {
                    Endpoint = new Uri($"http://{this.serverHost}:{this.serverPort}"),
                });

            var activities = new List<Activity>(this.NumberOfActivities);
            for (int i = 0; i < this.NumberOfActivities; i++)
            {
                activities.Add(this.testActivity);
            }

            await zipkinExporter.ExportAsync(activities, CancellationToken.None).ConfigureAwait(false);
        }

        private Activity CreateTestActivity()
        {
            var startTimestamp = DateTime.UtcNow;
            var endTimestamp = startTimestamp.AddSeconds(60);
            var eventTimestamp = DateTime.UtcNow;
            var traceId = ActivityTraceId.CreateFromString("e8ea7e9ac72de94e91fabc613f9686b2".AsSpan());

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

            var activity = activitySource.StartActivity(
                "Name",
                ActivityKind.Client,
                parentContext: new ActivityContext(traceId, parentSpanId, ActivityTraceFlags.Recorded),
                tags,
                links,
                startTime: startTimestamp);

            foreach (var evnt in events)
            {
                activity.AddEvent(evnt);
            }

            activity.SetEndTime(endTimestamp);
            activity.Stop();

            return activity;
        }
    }
}
