﻿// <copyright file="JaegerThriftIntegrationTest.cs" company="OpenTelemetry Authors">
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


using OpenTelemetry.Trace.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Reflection;
using OpenTelemetry.Exporter.Jaeger.Implementation;
using OpenTelemetry.Trace;
using Thrift.Protocols;
using Xunit;
using Process = OpenTelemetry.Exporter.Jaeger.Implementation.Process;

namespace OpenTelemetry.Exporter.Jaeger.Tests.Implementation
{
    public class JaegerThriftIntegrationTest
    {
        private readonly Tracer tracer;

        public JaegerThriftIntegrationTest()
        {
            tracer = TracerFactory.Create(b => { }).GetTracer(null);
        }

        [Fact]
        public async void JaegerThriftIntegrationTest_TAbstractBaseGeneratesConsistentThriftPayload()
        {
            var validJaegerThriftPayload = Convert.FromBase64String("goEBCWVtaXRCYXRjaBwcGAx0ZXN0IHByb2Nlc3MZHBgQdGVzdF9wcm9jZXNzX3RhZxUAGAp0ZXN0X3ZhbHVlAAAZHBab5cuG2OehhdwBFuPakI2n2cCVLhbUpdv9yJDPo4EBFpjckNKFzqHOsgEYBE5hbWUZHBUAFpvly4bY56GF3AEW49qQjafZwJUuFpCmrOGWyrWcgwEAFQIWgICz3saWvwUWgJycORl8GAlzdHJpbmdLZXkVABgFdmFsdWUAGAdsb25nS2V5FQZGAgAYCGxvbmdLZXkyFQZGAgAYCWRvdWJsZUtleRUCJwAAAAAAAPA/ABgKZG91YmxlS2V5MhUCJwAAAAAAAPA/ABgHYm9vbEtleRUEMQAYCXNwYW4ua2luZBUAGAZjbGllbnQAGSwWgICz3saWvwUZLBgDa2V5FQAYBXZhbHVlABgLZGVzY3JpcHRpb24VABgGRXZlbnQxAAAWgICz3saWvwUZLBgDa2V5FQAYBXZhbHVlABgLZGVzY3JpcHRpb24VABgGRXZlbnQyAAAAAAA=");
            
            using (var memoryTransport = new InMemoryTransport())
            {
                var protocolFactory = new TCompactProtocol.Factory();
                var thriftClient = new JaegerThriftClient(protocolFactory.GetProtocol(memoryTransport));
                var spanData = CreateTestSpan();
                var span = spanData.ToJaegerSpan();
                var process = new Process("test process", new Dictionary<string, object> { { "test_process_tag", "test_value" } });
                var batch = new Batch(process, new List<JaegerSpan> { span });

                await thriftClient.EmitBatchAsync(batch, CancellationToken.None);

                var buff = memoryTransport.GetBuffer();

                Assert.Equal(validJaegerThriftPayload, buff);
            }
        }


        private Span CreateTestSpan()
        {
            var startTimestamp = new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var endTimestamp = startTimestamp.AddSeconds(60);
            var eventTimestamp = new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero);

            var traceId = ActivityTraceId.CreateFromString("e8ea7e9ac72de94e91fabc613f9686b2".AsSpan());
            var spanId = "6a69db47429ea340";
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

            var span = (Span)tracer
                .StartSpan("Name",  new SpanContext(traceId, parentSpanId, ActivityTraceFlags.Recorded), SpanKind.Client,
                    new SpanCreationOptions
                    {
                        StartTimestamp = startTimestamp,
                        Links = new[] { link },
                    });

            var spanContextSetter = typeof(Span).GetMethod("set_Context", BindingFlags.Instance | BindingFlags.NonPublic);
            spanContextSetter.Invoke(span, new []{ new SpanContext(traceId, ActivitySpanId.CreateFromString(spanId.AsSpan()), ActivityTraceFlags.Recorded) });

            foreach (var attribute in attributes)
            {
                span.SetAttribute(attribute);
            }

            foreach (var evnt in events)
            {
                span.AddEvent(evnt);
            }

            span.Status = Status.Ok;

            span.End(endTimestamp);
            return span;
        }
    }
}
