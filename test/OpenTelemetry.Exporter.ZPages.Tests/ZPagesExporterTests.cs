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
using System.Threading;
using System.Threading.Tasks;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Xunit;

namespace OpenTelemetry.Exporter.ZPages.Tests
{
    public class ZPagesExporterTests
    {
        static ZPagesExporterTests()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                GetRequestedDataUsingParentId = (ref ActivityCreationOptions<string> options) => ActivityDataRequest.AllData,
                GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => ActivityDataRequest.AllData,
            };

            ActivitySource.AddActivityListener(listener);
        }

        [Fact]
        public async Task ZPagesExporter_Integration()
        {
            var batchActivity = new List<Activity> { CreateTestActivity() };

            ZPagesExporter exporter = new ZPagesExporter(new ZPagesExporterOptions());

            var exportResult = await exporter.ExportAsync(batchActivity, CancellationToken.None).ConfigureAwait(false);
            Assert.Equal(ExportResult.Success, exportResult);

            await exporter.ShutdownAsync(CancellationToken.None).ConfigureAwait(false);
        }

        [Fact]
        public void ZPagesExporter_CustomActivityProcessor()
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

            var openTelemetrySdk = OpenTelemetrySdk.CreateTracerProvider(b => b
                            .AddActivitySource(ActivitySourceName)
                            .UseZPagesExporter(
                                processorConfigure: p => p.AddProcessor((next) => testActivityProcessor)));

            var source = new ActivitySource(ActivitySourceName);
            var activity = source.StartActivity("Test Zipkin Activity");
            activity?.Stop();

            Assert.True(startCalled);
            Assert.True(endCalled);
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
                    new Dictionary<string, object>
                    {
                        { "key", "value" },
                    }),
                new ActivityEvent(
                    "Event2",
                    eventTimestamp,
                    new Dictionary<string, object>
                    {
                        { "key", "value" },
                    }),
            };

            var linkedSpanId = ActivitySpanId.CreateFromString("888915b6286b9c41".AsSpan());

            var activitySource = new ActivitySource(nameof(CreateTestActivity));

            var tags = setAttributes ?
                    attributes.Select(kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value.ToString()))
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
