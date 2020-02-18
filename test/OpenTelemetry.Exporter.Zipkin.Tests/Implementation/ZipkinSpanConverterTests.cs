// <copyright file="ZipkinSpanConverterTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.Zipkin.Implementation;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Tests.Implementation
{
    public class ZipkinTraceExporterRemoteEndpointTests
    {
        private static readonly ZipkinEndpoint DefaultZipkinEndpoint = new ZipkinEndpoint
        {
            ServiceName = "TestService",
        };

        [Fact]
        public void ZipkinSpanConverterTest_GenerateSpan_RemoteEndpointOmittedByDefault()
        {
            // Arrange
            var span = CreateTestSpan();

            // Act & Assert
            var zipkinSpan = ZipkinConversionExtensions.ToZipkinSpan(span, DefaultZipkinEndpoint);

            Assert.Null(zipkinSpan.RemoteEndpoint);
        }

        [Fact]
        public void ZipkinSpanConverterTest_GenerateSpan_RemoteEndpointResolution()
        {
            // Arrange
            var span = CreateTestSpan(
                additionalAttributes: new Dictionary<string, object>
                {
                    ["net.peer.name"] = "RemoteServiceName",
                });

            // Act & Assert
            var zipkinSpan = ZipkinConversionExtensions.ToZipkinSpan(span, DefaultZipkinEndpoint);

            Assert.NotNull(zipkinSpan.RemoteEndpoint);
            Assert.Equal("RemoteServiceName", zipkinSpan.RemoteEndpoint.ServiceName);
        }

        [Fact]
        public void ZipkinSpanConverterTest_GenerateSpan_RemoteEndpointResolutionPriority()
        {
            // Arrange
            var span = CreateTestSpan(
                additionalAttributes: new Dictionary<string, object>
                {
                    ["http.host"] = "DiscardedRemoteServiceName",
                    ["net.peer.name"] = "RemoteServiceName",
                    ["peer.hostname"] = "DiscardedRemoteServiceName",
                });

            // Act & Assert
            var zipkinSpan = ZipkinConversionExtensions.ToZipkinSpan(span, DefaultZipkinEndpoint);

            Assert.NotNull(zipkinSpan.RemoteEndpoint);
            Assert.Equal("RemoteServiceName", zipkinSpan.RemoteEndpoint.ServiceName);
        }

        internal SpanData CreateTestSpan(
            bool setAttributes = true,
            Dictionary<string, object> additionalAttributes = null,
            bool addEvents = true,
            bool addLinks = true)
        {
            var startTimestamp = DateTime.UtcNow;
            var endTimestamp = startTimestamp.AddSeconds(60);
            var eventTimestamp = DateTime.UtcNow;
            var traceId = ActivityTraceId.CreateFromString("e8ea7e9ac72de94e91fabc613f9686b2".AsSpan());

            var spanId = ActivitySpanId.CreateRandom();
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
            if (additionalAttributes != null)
            {
                foreach (var attribute in additionalAttributes)
                {
                    attributes.Add(attribute.Key, attribute.Value);
                }
            }

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

            return new SpanData(
                "Name",
                new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded),
                parentSpanId,
                SpanKind.Client,
                startTimestamp,
                setAttributes ? attributes : null,
                addEvents ? events : null,
                addLinks ? new[] { new Link(new SpanContext(
                        traceId,
                        linkedSpanId,
                        ActivityTraceFlags.Recorded)), } : null,
                null,
                Status.Ok,
                endTimestamp);
        }
    }
}
