// <copyright file="ZPagesExporterTests.cs" company="OpenTelemetry Authors">
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
using System.Net.Http;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.ZPages.Implementation;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.ZPages.Tests
{
    public class ZPagesExporterTests
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        static ZPagesExporterTests()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            };

            ActivitySource.AddActivityListener(listener);
        }

        [Fact]
        public void CheckingBadArgs()
        {
            TracerProviderBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddZPagesExporter());
        }

        [Fact]
        public void CheckingCustomActivityProcessor()
        {
            const string ActivitySourceName = "zpages.test";
            Guid requestId = Guid.NewGuid();
            TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

            bool startCalled = false;
            bool endCalled = false;

            testActivityProcessor.StartAction =
                (a) =>
                {
                    startCalled = true;
                };

            testActivityProcessor.EndAction =
                (a) =>
                {
                    endCalled = true;
                };

            using var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .AddProcessor(testActivityProcessor)
                .AddZPagesExporter()
                .Build();

            using var source = new ActivitySource(ActivitySourceName);
            using var activity = source.StartActivity("Test Zipkin Activity");
            activity?.Stop();

            Assert.True(startCalled);
            Assert.True(endCalled);

            ZPagesActivityTracker.Reset();
        }

        [Fact]
        public void CheckingCustomOptions()
        {
            ZPagesExporterOptions options = new ZPagesExporterOptions
            {
                RetentionTime = 100_000,
                Url = "http://localhost:7284/rpcz/",
            };

            ZPagesExporter exporter = new ZPagesExporter(options);

            Assert.Equal(options.Url, exporter.Options.Url);
            Assert.Equal(options.RetentionTime, exporter.Options.RetentionTime);
        }

        [Fact]
        public async Task CheckingZPagesProcessor()
        {
            const string ActivitySourceName = "zpages.test";
            ZPagesExporterOptions options = new ZPagesExporterOptions
            {
                RetentionTime = 100_000,
                Url = "http://localhost:7284/rpcz/",
            };
            ZPagesExporter exporter = new ZPagesExporter(options);
            var zpagesProcessor = new ZPagesProcessor(exporter);

            var source = new ActivitySource(ActivitySourceName);
            var activity0 = source.StartActivity("Test Zipkin Activity 1");
            zpagesProcessor.OnStart(activity0);

            // checking size of dictionaries from ZPagesActivityTracker
            Assert.Equal(1, ZPagesActivityTracker.ProcessingList.First().Value);
            Assert.Equal(1, ZPagesActivityTracker.TotalCount.First().Value);
            Assert.Single(ZPagesActivityTracker.TotalEndedCount);
            Assert.Single(ZPagesActivityTracker.TotalErrorCount);
            Assert.Single(ZPagesActivityTracker.TotalLatency);

            var activity1 = source.StartActivity("Test Zipkin Activity 1");
            zpagesProcessor.OnStart(activity1);

            // checking size of dictionaries from ZPagesActivityTracker
            Assert.Equal(2, ZPagesActivityTracker.ProcessingList.First().Value);
            Assert.Equal(2, ZPagesActivityTracker.TotalCount.First().Value);
            Assert.Single(ZPagesActivityTracker.TotalEndedCount);
            Assert.Single(ZPagesActivityTracker.TotalErrorCount);
            Assert.Single(ZPagesActivityTracker.TotalLatency);

            var activity2 = source.StartActivity("Test Zipkin Activity 2");
            zpagesProcessor.OnStart(activity2);

            // checking size of dictionaries from ZPagesActivityTracker
            Assert.Equal(2, ZPagesActivityTracker.ProcessingList.Count);
            Assert.Equal(2, ZPagesActivityTracker.TotalCount.Count);
            Assert.Equal(2, ZPagesActivityTracker.TotalEndedCount.Count);
            Assert.Equal(2, ZPagesActivityTracker.TotalErrorCount.Count);
            Assert.Equal(2, ZPagesActivityTracker.TotalLatency.Count);

            activity0?.Stop();
            activity1?.Stop();
            activity2?.Stop();
            zpagesProcessor.OnEnd(activity0);
            zpagesProcessor.OnEnd(activity1);
            zpagesProcessor.OnEnd(activity2);

            // checking if activities were processed
            Assert.Equal(0, ZPagesActivityTracker.ProcessingList.First().Value);
            Assert.Equal(0, ZPagesActivityTracker.ProcessingList.Last().Value);
            Assert.Empty(ZPagesActivityTracker.ZQueue);

            var zpagesServer = new ZPagesExporterStatsHttpServer(exporter);
            zpagesServer.Start();

            using var httpResponseMessage = await HttpClient.GetAsync("http://localhost:7284/rpcz/");
            Assert.True(httpResponseMessage.IsSuccessStatusCode);

            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            Assert.Contains($"<td>Test Zipkin Activity 1</td>", content);
            Assert.Contains($"<td>Test Zipkin Activity 2</td>", content);

            zpagesProcessor.Dispose();
            zpagesServer.Stop();
            zpagesServer.Dispose();
            exporter.Dispose();
        }

        internal static Activity CreateTestActivity(
           bool setAttributes = true,
           Dictionary<string, object> additionalAttributes = null,
           bool addEvents = true,
           bool addLinks = true,
           Resource resource = null,
           ActivityKind kind = ActivityKind.Client)
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
            if (additionalAttributes != null)
            {
                foreach (var attribute in additionalAttributes)
                {
                    attributes.Add(attribute.Key, attribute.Value);
                }
            }

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

            var activitySource = new ActivitySource(nameof(CreateTestActivity));

            var tags = setAttributes ?
                    attributes.Select(kvp => new KeyValuePair<string, object>(kvp.Key, kvp.Value.ToString()))
                    : null;
            var links = addLinks ?
                    new[]
                    {
                        new ActivityLink(new ActivityContext(
                            traceId,
                            linkedSpanId,
                            ActivityTraceFlags.Recorded)),
                    }
                    : null;

            var activity = activitySource.StartActivity(
                "Name",
                kind,
                parentContext: new ActivityContext(traceId, parentSpanId, ActivityTraceFlags.Recorded),
                tags,
                links,
                startTime: startTimestamp);

            if (addEvents)
            {
                foreach (var evnt in events)
                {
                    activity.AddEvent(evnt);
                }
            }

            activity.SetEndTime(endTimestamp);
            activity.Stop();

            return activity;
        }
    }
}
