// <copyright file="JaegerUdpBatcherTests.cs" company="OpenTelemetry Authors">
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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using OpenTelemetry.Exporter.Jaeger.Implementation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Xunit;

namespace OpenTelemetry.Exporter.Jaeger.Tests.Implementation
{
    public class JaegerUdpBatcherTests
    {
        public const string TestPayloadBase64 = "goEBCWVtaXRCYXRjaBwcGAx0ZXN0IHByb2Nlc3MZHBgQdGVzdF9wcm9jZXNzX3RhZxUAGAp0ZXN0X3ZhbHVlAAAZHBab5cuG2OehhdwBFuPakI2n2cCVLhaAjfWp6NHt6dQBFrK5moSni5GXGBgETmFtZRkcFQAWm+XLhtjnoYXcARbj2pCNp9nAlS4W/Y6j+bqS9fbuAQAVABaAgLPexpa/BRYAGZwYCXN0cmluZ0tleRUAGAV2YWx1ZQAYB2xvbmdLZXkVABgBMQAYCGxvbmdLZXkyFQAYATEAGAlkb3VibGVLZXkVABgBMQAYCmRvdWJsZUtleTIVABgBMQAYB2Jvb2xLZXkVABgEVHJ1ZQAYDm90LnN0YXR1c19jb2RlFQAYAk9rABgJc3Bhbi5raW5kFQAYBmNsaWVudAAYDGxpYnJhcnkubmFtZRUAGBtDcmVhdGVUZXN0UGF5bG9hZEphZWdlclNwYW4AGSwWgICz3saWvwUZLBgDa2V5FQAYBXZhbHVlABgHbWVzc2FnZRUAGAZFdmVudDEAABaAgLPexpa/BRksGANrZXkVABgFdmFsdWUAGAdtZXNzYWdlFQAYBkV2ZW50MgAAAAAA";

        static JaegerUdpBatcherTests()
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

        internal static Process TestProcess { get; } = new Process("test process", new Dictionary<string, object> { { "test_process_tag", "test_value" } });

        [Fact]
        public async Task JaegerUdpBatcherTests_BuildBatchesToTransmit_DefaultBatch()
        {
            // Arrange
            var options = new JaegerExporterOptions { ServiceName = "TestService", MaxFlushInterval = TimeSpan.FromHours(1) };

            var jaegerUdpBatcher = new JaegerUdpBatcher(options);

            // Act
            await jaegerUdpBatcher.AppendAsync(CreateTestJaegerSpan(), CancellationToken.None).ConfigureAwait(false);
            await jaegerUdpBatcher.AppendAsync(CreateTestJaegerSpan(), CancellationToken.None).ConfigureAwait(false);
            await jaegerUdpBatcher.AppendAsync(CreateTestJaegerSpan(), CancellationToken.None).ConfigureAwait(false);

            var batches = jaegerUdpBatcher.CurrentBatches.Values;

            // Assert
            Assert.Single(batches);
            Assert.Equal("TestService", batches.First().Process.ServiceName);
            Assert.Equal(3, batches.First().SpanMessages.Count());
        }

        [Fact]
        public async Task JaegerUdpBatcherTests_BuildBatchesToTransmit_MultipleBatches()
        {
            // Arrange
            var options = new JaegerExporterOptions { ServiceName = "TestService", MaxFlushInterval = TimeSpan.FromHours(1) };

            var jaegerUdpBatcher = new JaegerUdpBatcher(options);

            // Act
            await jaegerUdpBatcher.AppendAsync(CreateTestJaegerSpan(), CancellationToken.None).ConfigureAwait(false);
            await jaegerUdpBatcher.AppendAsync(
                CreateTestJaegerSpan(
                    additionalAttributes: new Dictionary<string, object>
                    {
                        ["peer.service"] = "MySQL",
                    }),
                CancellationToken.None).ConfigureAwait(false);
            await jaegerUdpBatcher.AppendAsync(CreateTestJaegerSpan(), CancellationToken.None).ConfigureAwait(false);

            var batches = jaegerUdpBatcher.CurrentBatches.Values;

            // Assert
            Assert.Equal(2, batches.Count());

            var primaryBatch = batches.Where(b => b.Process.ServiceName == "TestService");
            Assert.Single(primaryBatch);
            Assert.Equal(2, primaryBatch.First().SpanMessages.Count());

            var mySQLBatch = batches.Where(b => b.Process.ServiceName == "MySQL");
            Assert.Single(mySQLBatch);
            Assert.Single(mySQLBatch.First().SpanMessages);
        }

        [Fact]
        public async Task JaegerUdpBatcherTests_BuildBatchesToTransmit_FlushedBatch()
        {
            // Arrange
            var options = new JaegerExporterOptions { ServiceName = "TestService", MaxFlushInterval = TimeSpan.FromHours(1), MaxPacketSize = 750 };

            var jaegerUdpBatcher = new JaegerUdpBatcher(options);

            // Act
            await jaegerUdpBatcher.AppendAsync(CreateTestJaegerSpan(), CancellationToken.None).ConfigureAwait(false);
            await jaegerUdpBatcher.AppendAsync(CreateTestJaegerSpan(), CancellationToken.None).ConfigureAwait(false);
            var flushCount = await jaegerUdpBatcher.AppendAsync(CreateTestJaegerSpan(), CancellationToken.None).ConfigureAwait(false);

            var batches = jaegerUdpBatcher.CurrentBatches.Values;

            // Assert
            Assert.Equal(2, flushCount);
            Assert.Single(batches);
            Assert.Equal("TestService", batches.First().Process.ServiceName);
            Assert.Single(batches.First().SpanMessages);
        }

        [Fact]
        public async Task JaegerUdpBatcher_IntegrationTest()
        {
            var validJaegerThriftPayload = Convert.FromBase64String(TestPayloadBase64);

            var memoryTransport = new InMemoryTransport();

            using var jaegerUdpBatcher = new JaegerUdpBatcher(
                new JaegerExporterOptions { MaxFlushInterval = TimeSpan.FromHours(1) },
                memoryTransport);
            jaegerUdpBatcher.Process = TestProcess;

            await jaegerUdpBatcher.AppendAsync(CreateTestPayloadJaegerSpan(), CancellationToken.None).ConfigureAwait(false);

            await jaegerUdpBatcher.FlushAsync(CancellationToken.None).ConfigureAwait(false);

            Assert.Equal(Convert.ToBase64String(validJaegerThriftPayload), Convert.ToBase64String(memoryTransport.ToArray()));

            memoryTransport.Reset();

            await jaegerUdpBatcher.AppendAsync(CreateTestPayloadJaegerSpan(), CancellationToken.None).ConfigureAwait(false);

            await jaegerUdpBatcher.FlushAsync(CancellationToken.None).ConfigureAwait(false);

            // SeqNo is the second byte.
            validJaegerThriftPayload[2]++;

            Assert.Equal(Convert.ToBase64String(validJaegerThriftPayload), Convert.ToBase64String(memoryTransport.ToArray()));
        }

        internal static JaegerSpan CreateTestPayloadJaegerSpan()
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

            var activitySource = new ActivitySource(nameof(CreateTestPayloadJaegerSpan));

            var tags = attributes.Select(kvp => new KeyValuePair<string, string>(kvp.Key, kvp.Value.ToString()));

            var links = new[]
            {
                new ActivityLink(new ActivityContext(
                    traceId,
                    linkedSpanId,
                    ActivityTraceFlags.Recorded)),
            };

            Activity activity = activitySource.StartActivity(
                "Name",
                ActivityKind.Client,
                parentContext: default,
                tags,
                links,
                startTime: startTimestamp);

            typeof(Activity).GetField("_traceId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(activity, traceId.ToHexString());
            typeof(Activity).GetField("_spanId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(activity, spanId.ToHexString());
            typeof(Activity).GetField("_parentSpanId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(activity, parentSpanId.ToHexString());

            foreach (var @event in events)
            {
                activity.AddEvent(@event);
            }

            activity.SetStatus(Status.Ok);

            return activity.ToJaegerSpan();
        }

        internal static JaegerSpan CreateTestJaegerSpan(
            bool setAttributes = true,
            Dictionary<string, object> additionalAttributes = null,
            bool addEvents = true,
            bool addLinks = true,
            Resource resource = null,
            ActivityKind kind = ActivityKind.Client)
        {
            return JaegerActivityConversionTest.CreateTestActivity(
                setAttributes, additionalAttributes, addEvents, addLinks, resource, kind).ToJaegerSpan();
        }
    }
}
