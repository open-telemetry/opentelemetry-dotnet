// <copyright file="ZipkinExporterTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.Zipkin.Implementation;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Tests
{
    public class ZipkinExporterTests : IDisposable
    {
        private const string TraceId = "e8ea7e9ac72de94e91fabc613f9686b2";
        private static readonly ConcurrentDictionary<Guid, string> Responses = new ConcurrentDictionary<Guid, string>();

        private readonly IDisposable testServer;
        private readonly string testServerHost;
        private readonly int testServerPort;

        static ZipkinExporterTests()
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

        public ZipkinExporterTests()
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

        [Fact]
        public void BadArgs()
        {
            TracerProviderBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddZipkinExporter());
        }

        [Fact]
        public void SuppresssesInstrumentation()
        {
            const string ActivitySourceName = "zipkin.test";
            Guid requestId = Guid.NewGuid();
            TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

            int endCalledCount = 0;

            testActivityProcessor.EndAction =
                (a) =>
                {
                    endCalledCount++;
                };

            var exporterOptions = new ZipkinExporterOptions();
            exporterOptions.ServiceName = "test-zipkin";
            exporterOptions.Endpoint = new Uri($"http://{this.testServerHost}:{this.testServerPort}/api/v2/spans?requestId={requestId}");
            var zipkinExporter = new ZipkinExporter(exporterOptions);
            var exportActivityProcessor = new BatchExportActivityProcessor(zipkinExporter);

            var openTelemetrySdk = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .AddProcessor(testActivityProcessor)
                .AddProcessor(exportActivityProcessor)
                .AddHttpClientInstrumentation()
                .Build();

            var source = new ActivitySource(ActivitySourceName);
            var activity = source.StartActivity("Test Zipkin Activity");
            activity?.Stop();

            // We call ForceFlush on the exporter twice, so that in the event
            // of a regression, this should give any operations performed in
            // the Zipkin exporter itself enough time to be instrumented and
            // loop back through the exporter.
            exportActivityProcessor.ForceFlush();
            exportActivityProcessor.ForceFlush();

            Assert.Equal(1, endCalledCount);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IntegrationTest(bool useShortTraceIds)
        {
            Guid requestId = Guid.NewGuid();

            ZipkinExporter exporter = new ZipkinExporter(
                new ZipkinExporterOptions
                {
                    Endpoint = new Uri($"http://{this.testServerHost}:{this.testServerPort}/api/v2/spans?requestId={requestId}"),
                    UseShortTraceIds = useShortTraceIds,
                });

            var activity = CreateTestActivity();
            var processor = new SimpleExportActivityProcessor(exporter);

            processor.OnEnd(activity);

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
                $@"[{{""traceId"":""{traceId}"",""name"":""Name"",""parentId"":""{ZipkinActivityConversionExtensions.EncodeSpanId(activity.ParentSpanId)}"",""id"":""{ZipkinActivityConversionExtensions.EncodeSpanId(context.SpanId)}"",""kind"":""CLIENT"",""timestamp"":{timestamp},""duration"":60000000,""localEndpoint"":{{""serviceName"":""OpenTelemetry Exporter""{ipInformation}}},""remoteEndpoint"":{{""serviceName"":""http://localhost:44312/""}},""annotations"":[{{""timestamp"":{eventTimestamp},""value"":""Event1""}},{{""timestamp"":{eventTimestamp},""value"":""Event2""}}],""tags"":{{""stringKey"":""value"",""longKey"":""1"",""longKey2"":""1"",""doubleKey"":""1"",""doubleKey2"":""1"",""longArrayKey"":""1,2"",""boolKey"":""True"",""http.host"":""http://localhost:44312/"",""library.name"":""CreateTestActivity""}}}}]",
                Responses[requestId]);
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
                { "longArrayKey", new long[] { 1, 2 } },
                { "boolKey", true },
                { "http.host", "http://localhost:44312/" }, // simulating instrumentation tag adding http.host
            };
            if (additionalAttributes != null)
            {
                foreach (var attribute in additionalAttributes)
                {
                    if (!attributes.ContainsKey(attribute.Key))
                    {
                        attributes.Add(attribute.Key, attribute.Value);
                    }
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
                    attributes.Select(kvp => new KeyValuePair<string, object>(kvp.Key, kvp.Value))
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
