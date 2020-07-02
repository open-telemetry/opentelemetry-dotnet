// <copyright file="ZipkinActivityExporterTests.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Exporter.Zipkin.Implementation;
using OpenTelemetry.Internal.Test;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Tests
{
    public class ZipkinActivityExporterTests : IDisposable
    {
        private const string TraceId = "e8ea7e9ac72de94e91fabc613f9686b2";
        private static readonly ConcurrentDictionary<Guid, string> Responses = new ConcurrentDictionary<Guid, string>();

        private readonly IDisposable testServer;
        private readonly string testServerHost;
        private readonly int testServerPort;

        static ZipkinActivityExporterTests()
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

        public ZipkinActivityExporterTests()
        {
            this.testServer = TestHttpServer.RunServer(
                ctx => ProcessServerRequest(ctx),
                out this.testServerHost,
                out this.testServerPort);

            static void ProcessServerRequest(HttpListenerContext context)
            {
                context.Response.StatusCode = 200;

                using StreamReader readStream = new StreamReader(context.Request.InputStream);

                string requestContent = readStream.ReadToEnd();

                Responses.TryAdd(
                    Guid.Parse(context.Request.QueryString["requestId"]),
                    requestContent);

                context.Response.OutputStream.Close();
            }
        }

        public void Dispose()
        {
            this.testServer.Dispose();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ZipkinActivityExporterIntegrationTest(bool useShortTraceIds)
        {
            var batchActivity = new List<Activity> { CreateTestActivity() };

            Guid requestId = Guid.NewGuid();

            ZipkinActivityExporter exporter = new ZipkinActivityExporter(
                new ZipkinTraceExporterOptions
                {
                    Endpoint = new Uri($"http://{this.testServerHost}:{this.testServerPort}/api/v2/spans?requestId={requestId}"),
                    UseShortTraceIds = useShortTraceIds,
                });

            await exporter.ExportAsync(batchActivity, CancellationToken.None).ConfigureAwait(false);

            await exporter.ShutdownAsync(CancellationToken.None).ConfigureAwait(false);

            var activity = batchActivity[0];
            var context = activity.Context;

            var timestamp = activity.StartTimeUtc.ToEpochMicroseconds();
            var eventTimestamp = activity.Events.First().Timestamp.ToEpochMicroseconds();

            StringBuilder ipInformation = new StringBuilder();
            if (!string.IsNullOrEmpty(exporter.LocalEndpoint.Ipv4))
            {
                ipInformation.Append($@",""ipv4"":""{exporter.LocalEndpoint.Ipv4}""");
            }

            if (!string.IsNullOrEmpty(exporter.LocalEndpoint.Ipv6))
            {
                ipInformation.Append($@",""ipv6"":""{exporter.LocalEndpoint.Ipv6}""");
            }

            var traceId = useShortTraceIds ? TraceId.Substring(TraceId.Length - 16, 16) : TraceId;

            Assert.Equal(
                $@"[{{""traceId"":""{traceId}"",""name"":""Name"",""parentId"":""{ZipkinConversionExtensions.EncodeSpanId(activity.ParentSpanId)}"",""id"":""{ZipkinActivityConversionExtensions.EncodeSpanId(context.SpanId)}"",""kind"":""CLIENT"",""timestamp"":{timestamp},""duration"":60000000,""localEndpoint"":{{""serviceName"":""Open Telemetry Exporter""{ipInformation}}},""annotations"":[{{""timestamp"":{eventTimestamp},""value"":""Event1""}},{{""timestamp"":{eventTimestamp},""value"":""Event2""}}],""tags"":{{""stringKey"":""value"",""longKey"":""1"",""longKey2"":""1"",""doubleKey"":""1"",""doubleKey2"":""1"",""boolKey"":""True"",""library.name"":""CreateTestActivity""}}}}]",
                Responses[requestId]);
        }

        [Fact]
        public void UseZipkinActivityExporterWithCustomActivityProcessor()
        {
            const string ActivitySourceName = "zipkin.test";
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

            var openTelemetrySdk = OpenTelemetrySdk.EnableOpenTelemetry(b => b
                            .AddActivitySource(ActivitySourceName)
                            .UseZipkinActivityExporter(
                                o =>
                            {
                                o.ServiceName = "test-zipkin";
                                o.Endpoint = new Uri($"http://{this.testServerHost}:{this.testServerPort}/api/v2/spans?requestId={requestId}");
                            }, p => p.AddProcessor((next) => testActivityProcessor)));

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
