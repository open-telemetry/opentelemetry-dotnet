// <copyright file="ApplicationInsightsTraceExporterTests.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of theLicense at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Threading;
using OpenTelemetry.Trace.Config;
using OpenTelemetry.Trace.Export;

namespace OpenTelemetry.Exporter.ApplicationInsights.Tests
{
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Newtonsoft.Json;
    using OpenTelemetry.Trace;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Xunit;

    public class ApplicationInsightsTraceExporterTests
    {
        private const string TestTraceId = "d79bdda7eb9c4a9fa9bda52fe7b48b95";
        private const string TestSpanId = "d7ddeb4aa9a5e78b";
        private const string TestParentSpanId = "9ba79c9fbd2fb495";
        private const string TestChannelEndpoint = "https://applicationinsights.com";

        private readonly byte[] testTraceIdBytes = { 0xd7, 0x9b, 0xdd, 0xa7, 0xeb, 0x9c, 0x4a, 0x9f, 0xa9, 0xbd, 0xa5, 0x2f, 0xe7, 0xb4, 0x8b, 0x95 };
        private readonly byte[] testSpanIdBytes = { 0xd7, 0xdd, 0xeb, 0x4a, 0xa9, 0xa5, 0xe7, 0x8b };
        private readonly byte[] testParentSpanIdBytes = { 0x9b, 0xa7, 0x9c, 0x9f, 0xbd, 0x2f, 0xb4, 0x95 };

        private readonly JsonSerializerSettings jsonSettingThrowOnError = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            ReferenceLoopHandling = ReferenceLoopHandling.Error,
            NullValueHandling = NullValueHandling.Include,
            DefaultValueHandling = DefaultValueHandling.Include,
        };

        private DateTime now;

        public ApplicationInsightsTraceExporterTests()
        {
            now = DateTime.UtcNow.AddSeconds(-1);
        }

        private ConcurrentQueue<ITelemetry> ConvertSpan(Span otSpan)
        {
            var sentItems = new ConcurrentQueue<ITelemetry>();
            var configuration = new TelemetryConfiguration();
            ITelemetryChannel channel = new StubTelemetryChannel
            {
                OnSend = t => sentItems.Enqueue(t),
                EndpointAddress = TestChannelEndpoint,
            };

            configuration.TelemetryChannel = channel;

            var exporter = new ApplicationInsightsTraceExporter(configuration);
            exporter.ExportAsync(new List<Span> { otSpan }, CancellationToken.None).Wait();

            return sentItems;
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequest()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);

            parentSpanId = default;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);
            span.End(endTimestamp);
            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            Assert.Single(sentItems);
            Assert.True(sentItems.Single() is RequestTelemetry);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal("spanName", request.Name);
            Assert.Equal(now.AddSeconds(-1), request.Timestamp);
            Assert.Equal(1, request.Duration.TotalSeconds);

            Assert.Equal(span.Context.TraceId.ToHexString(), request.Context.Operation.Id);

            Assert.Equal($"|{span.Context.TraceId.ToHexString()}.{span.Context.SpanId.ToHexString()}.", request.Id);
            Assert.Null(request.Context.Operation.ParentId);

            Assert.True(request.Success);
            Assert.Equal("0", request.ResponseCode);

            // TODO: implement this
            //Assert.Equal("lf_unspecified-oc:0.0.0", request.Context.GetInternalContext().SdkVersion);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestWithTracestate()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);

            tracestate = tracestate.ToBuilder()
                .Set("k1", "v1")
                .Set("k2", "v2")
                .Build();

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, SpanKind.Server, status);

            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            Assert.Single(sentItems);
            Assert.True(sentItems.Single() is RequestTelemetry);

            var request = sentItems.OfType<RequestTelemetry>().Single();

            Assert.Equal(2, request.Properties.Count);
            Assert.Equal("v1", request.Properties["k1"]);
            Assert.Equal("v2", request.Properties["k2"]);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestWithParent()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            parentSpanId = ActivitySpanId.CreateFromBytes(this.testParentSpanIdBytes);

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            Assert.Equal($"|{TestTraceId}.{TestParentSpanId}.", ((RequestTelemetry)sentItems.Single()).Context.Operation.ParentId);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestWithInvalidParent()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            parentSpanId = default;

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            Assert.Null(((RequestTelemetry)sentItems.Single()).Context.Operation.ParentId);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestWithStatus()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            status = Status.Ok;

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            var request = (RequestTelemetry)sentItems.Single();

            Assert.True(request.Success.HasValue);
            Assert.True(request.Success.Value);
            Assert.Equal("0", request.ResponseCode); // this check doesn't match Local Forwarder Assert.IsTrue(string.IsNullOrEmpty(request.ResponseCode));
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestWithStatusAndDescription()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            status = Status.Ok.WithDescription("all good");

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            var request = (RequestTelemetry)sentItems.Single();

            Assert.True(request.Success.HasValue);
            Assert.True(request.Success.Value);
            Assert.Equal("0", request.ResponseCode);  // this check doesn't match Local Forwarder Assert.AreEqual("all good", request.ResponseCode);
            Assert.Equal("all good", request.Properties["statusDescription"]);  // this check doesn't match Local Forwarder
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestWithNonSuccessStatusAndDescription()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            status = Status.Cancelled.WithDescription("all bad");

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            var request = (RequestTelemetry)sentItems.Single();

            Assert.True(request.Success.HasValue);
            Assert.False(request.Success.Value);
            Assert.Equal("1", request.ResponseCode);  // this check doesn't match Local Forwarder Assert.AreEqual("all bad", request.ResponseCode);
            Assert.Equal("all bad", request.Properties["statusDescription"]);  // this check doesn't match Local Forwarder
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestErrorAttribute()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("error", true);
            span.End(endTimestamp);

            var sentItems = this.ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.True(request.Success.HasValue);
            Assert.False(request.Success.Value);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksClientDependency()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            kind = SpanKind.Client;
            parentSpanId = default;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);
            span.End(endTimestamp);
            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            Assert.Single(sentItems);
            Assert.True(sentItems.Single() is DependencyTelemetry);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal("spanName", dependency.Name);
            Assert.Equal(now.AddSeconds(-1), dependency.Timestamp);
            Assert.Equal(1, dependency.Duration.TotalSeconds);

            Assert.Equal(span.Context.TraceId.ToHexString(), dependency.Context.Operation.Id);
            Assert.Null(dependency.Context.Operation.ParentId);
            Assert.Equal($"|{span.Context.TraceId.ToHexString()}.{span.Context.SpanId.ToHexString()}.", dependency.Id);

            Assert.Equal("0", dependency.ResultCode);
            Assert.True(dependency.Success.HasValue);
            Assert.True(dependency.Success);

            // Assert.Equal("lf_unspecified-oc:0.0.0", dependency.Context.GetInternalContext().SdkVersion);

            Assert.True(string.IsNullOrEmpty(dependency.Type));
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksInternalSpanAsDependency()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            kind = SpanKind.Internal;

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);
            span.End(endTimestamp);
            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            Assert.Single(sentItems);
            Assert.True(sentItems.Single() is DependencyTelemetry);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal("spanName", dependency.Name);
            Assert.Equal("InProc", dependency.Type);
            Assert.Equal(now.AddSeconds(-1), dependency.Timestamp);
            Assert.Equal(1, dependency.Duration.TotalSeconds);

            Assert.Equal(TestTraceId, dependency.Context.Operation.Id);

            Assert.Equal($"|{TestTraceId}.{span.Context.SpanId.ToHexString()}.", dependency.Id);
            Assert.Equal($"|{TestTraceId}.{parentSpanId.ToHexString()}.", dependency.Context.Operation.ParentId);

            Assert.Equal("0", dependency.ResultCode);
            Assert.True(dependency.Success.HasValue);
            Assert.True(dependency.Success);

            // Assert.Equal("lf_unspecified-oc:0.0.0", dependency.Context.GetInternalContext().SdkVersion);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_DoesNotTrackCallToAppInsights()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            kind = SpanKind.Client;

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", TestChannelEndpoint);
            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.status_code", 200);
            span.End(endTimestamp);

            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            Assert.Empty(sentItems);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksProducerDependency()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            kind = SpanKind.Producer;

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.End(endTimestamp);

            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            Assert.Single(sentItems);
            Assert.True(sentItems.Single() is DependencyTelemetry);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal("spanName", dependency.Name);
            Assert.Equal(now.AddSeconds(-1), dependency.Timestamp);
            Assert.Equal(1, dependency.Duration.TotalSeconds);

            Assert.Equal(TestTraceId, dependency.Context.Operation.Id);

            Assert.Equal($"|{TestTraceId}.{span.Context.SpanId.ToHexString()}.", dependency.Id);
            Assert.Equal($"|{TestTraceId}.{ActivitySpanId.CreateFromBytes(this.testParentSpanIdBytes)}.", dependency.Context.Operation.ParentId);
            Assert.Equal("0", dependency.ResultCode);
            Assert.True(dependency.Success.HasValue);
            Assert.True(dependency.Success);

            // Assert.Equal("lf_unspecified-oc:0.0.0", dependency.Context.GetInternalContext().SdkVersion);

            Assert.True(string.IsNullOrEmpty(dependency.Type));
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithTracestate()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            tracestate = tracestate.ToBuilder()
                .Set("k1", "v1")
                .Set("k2", "v2")
                .Build();

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, SpanKind.Client, status);

            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            Assert.Single(sentItems);
            Assert.True(sentItems.Single() is DependencyTelemetry);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();

            Assert.Equal(2, dependency.Properties.Count);
            Assert.Equal("v1", dependency.Properties["k1"]);
            Assert.Equal("v2", dependency.Properties["k2"]);
            // Assert.Equal("lf_unspecified-oc:0.0.0", dependency.Context.GetInternalContext().SdkVersion);
        }

        /*
        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithAnnotationsAndNode()
        {
            // ARRANGE
            var now = DateTime.UtcNow;

            this.GetDefaults(out var context, out var parentSpanId, out var resource, out var name, out var startTimestamp, out var attributes, out var events,  out var links, out var childSpanCount, out var status, out var kind, out var endTimestamp);

            kind = SpanKind.Client;

            var span = SpanData.Create(context, parentSpanId, resource, name, startTimestamp, attributes, events, links, childSpanCount, status, kind, endTimestamp);


            span.TimeEvents = new Span.Types.TimeEvents
            {
                TimeEvent =
                        {
                            new Span.Types.TimeEvent
                            {
                                Time = now.ToTimestamp(),
                                Annotation = new Span.Types.TimeEvent.Types.Annotation
                                {
                                    Description = new TruncatableString {Value = "test message1"},
                                },
                            },
                            new Span.Types.TimeEvent
                            {
                                Time = now.ToTimestamp(),
                                MessageEvent = new Span.Types.TimeEvent.Types.MessageEvent
                                {
                                    Id = 1,
                                    CompressedSize = 2,
                                    UncompressedSize = 3,
                                    Type = Span.Types.TimeEvent.Types.MessageEvent.Types.Type.Received,
                                },
                            },
                        },
            };


            string hostName = "host", serviceName = "tests", version = "1.2.3.4.5";
            uint pid = 12345;
            var lang = LibraryInfo.Types.Language.CSharp;

            var node = CreateBasicNode(hostName, pid, lang, version, serviceName);
            node.Attributes.Add("a", "b");

            // ACT
            var sentItems = this.ConvertSpan(span, node, string.Empty);

            // ASSERT
            Assert.Equal(3, sentItems.Count);
            Assert.Single(sentItems.OfType<DependencyTelemetry>());
            Assert.Equal(2, sentItems.OfType<TraceTelemetry>().Count());

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            var trace1 = sentItems.OfType<TraceTelemetry>().First();
            var trace2 = sentItems.OfType<TraceTelemetry>().Last();
            Assert.Equal(serviceName, dependency.Context.Cloud.RoleName);
            Assert.Equal(serviceName, trace1.Context.Cloud.RoleName);
            Assert.Equal(serviceName, trace2.Context.Cloud.RoleName);

            Assert.Equal($"{hostName}.{pid}", dependency.Context.Cloud.RoleInstance);
            Assert.Equal($"{hostName}.{pid}", trace1.Context.Cloud.RoleInstance);
            Assert.Equal($"{hostName}.{pid}", trace2.Context.Cloud.RoleInstance);

            Assert.Equal(0, dependency.Properties.Count);
            Assert.Equal(0, trace1.Properties.Count);
            Assert.Equal(0, trace2.Properties.Count);
        }
        */

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithParent()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            kind = SpanKind.Client;
            parentSpanId = ActivitySpanId.CreateFromBytes(this.testParentSpanIdBytes);

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal($"|{TestTraceId}.{TestParentSpanId}.", dependency.Context.Operation.ParentId);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithStatus()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            kind = SpanKind.Client;
            status = Status.Ok;

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            var dependency = (DependencyTelemetry)sentItems.Single();

            Assert.True(dependency.Success.HasValue);
            Assert.True(dependency.Success.Value);
            Assert.Equal("0", dependency.ResultCode);
            Assert.False(dependency.Properties.ContainsKey("StatusDescription"));  // TODO: why it is upper case first letter?
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithStatusAndDescription()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            kind = SpanKind.Client;
            status = Status.Ok.WithDescription("all good");
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            var dependency = (DependencyTelemetry)sentItems.Single();

            Assert.True(dependency.Success.HasValue);
            Assert.True(dependency.Success.Value);

            Assert.Equal("0", dependency.ResultCode);
            Assert.True(dependency.Properties.ContainsKey("statusDescription"));
            Assert.Equal("all good", dependency.Properties["statusDescription"]);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithNonSuccessStatusAndDescription()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            kind = SpanKind.Client;
            status = Status.Cancelled.WithDescription("all bad");
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            var dependency = (DependencyTelemetry)sentItems.Single();

            Assert.True(dependency.Success.HasValue);
            Assert.False(dependency.Success.Value);
            Assert.Equal("1", dependency.ResultCode);
            Assert.True(dependency.Properties.ContainsKey("statusDescription"));
            Assert.Equal("all bad", dependency.Properties["statusDescription"]);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyErrorAttribute()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            kind = SpanKind.Client;

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);
            span.SetAttribute("error", true);
            span.End(endTimestamp);

            var sentItems = this.ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.True(dependency.Success.HasValue);
            Assert.False(dependency.Success.Value);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestBasedOnClientSpanKindAttribute()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            kind = SpanKind.Client;

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);
            span.SetAttribute("span.kind", "client");
            span.End(endTimestamp);

            var sentItems = this.ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestBasedOnProducerSpanKindAttribute()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            kind = SpanKind.Client;

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("span.kind", "producer");
            var sentItems = this.ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestBasedOnConsumerSpanKindAttribute()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            kind = SpanKind.Consumer;

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            var sentItems = this.ConvertSpan(span);

            Assert.True(sentItems.Single() is RequestTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestBasedOnSpanKindProperty()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            parentSpanId = ActivitySpanId.CreateFromBytes(this.testParentSpanIdBytes);
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            var sentItems = this.ConvertSpan(span);

            Assert.True(sentItems.Single() is RequestTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyBasedOnSpanKindProperty()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            kind = SpanKind.Client;
            parentSpanId = ActivitySpanId.CreateFromBytes(this.testParentSpanIdBytes);
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            var sentItems = this.ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyBasedOnSpanKindAttribute()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            kind = SpanKind.Internal;

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("span.kind", "client");
            span.End(endTimestamp);

            var sentItems = this.ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyBasedOnSameProcessAsParentFlag()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            kind = SpanKind.Internal;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);
            var sentItems = this.ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyBasedOnSameProcessAsParentFlagNotSet()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            kind = SpanKind.Internal;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            var sentItems = this.ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        // TODO: should we allow null dates? There is no reason to not allow it
        //[Fact]
        //public void OpenTelemetryTelemetryConverterTests_TracksRequestWithoutName()
        //{
        //    this.GetDefaults(out var context, out var parentSpanId, out var resource, out var name, out var startTimestamp, out var attributes, out var events,  out var links, out var childSpanCount, out var status, out var kind, out var endTimestamp);
        //    name = null;
        //    var span = SpanData.Create(context, parentSpanId, resource, name, startTimestamp, attributes, events, links, childSpanCount, status, kind, endTimestamp);

        //    var sentItems = this.ConvertSpan(span);

        //    Assert.Null(sentItems.OfType<RequestTelemetry>().Single().Name);
        //}

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithoutKind()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            kind = SpanKind.Internal;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            var sentItems = this.ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        // TODO: should we allow null dates? There is no reason to not allow it
        //[Fact]
        //public void OpenTelemetryTelemetryConverterTests_TracksRequestWithoutStartAndEndTime()
        //{
        //    this.GetDefaults(out var context, out var parentSpanId, out var resource, out var name, out var startTimestamp, out var attributes, out var events,  out var links, out var childSpanCount, out var status, out var kind, out var endTimestamp);
        //    startTimestamp = null;
        //    endTimestamp = null;
        //    var span = SpanData.Create(context, parentSpanId, resource, name, startTimestamp, attributes, events, links, childSpanCount, status, kind, endTimestamp);

        //    var sentItems = this.ConvertSpan(span);

        //    var request = sentItems.OfType<RequestTelemetry>().Single();
        //    Assert.True(Math.Abs((request.Timestamp - DateTime.UtcNow).TotalSeconds) < 1);
        //    Assert.Equal(0, request.Duration.TotalSeconds);
        //}

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestWithUrl()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpIn";
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);
            span.SetAttribute("http.url", url.ToString());
            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.status_code", 409);

            var sentItems = this.ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal(url.ToString(), request.Url.ToString());
            Assert.Equal("POST /path", request.Name);
            Assert.Equal("409", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestWithRelativeUrl()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpIn";

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", url.LocalPath);
            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.status_code", 409);

            var sentItems = this.ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal("/path", request.Url.ToString()); // This check doesn't match Local Forwarder Assert.Null(request.Url);
            Assert.Equal("POST /path", request.Name);
            Assert.Equal("409", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestWithUrlAndRoute()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpIn";
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", url.ToString());
            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.route", "route");
            span.SetAttribute("http.status_code", 503);

            var sentItems = this.ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal(url.ToString(), request.Url.ToString());
            Assert.Equal("POST route", request.Name);
            Assert.Equal("503", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestWithUrlAndNoMethod()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpIn";
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", url.ToString());
            span.SetAttribute("http.status_code", 200);
            var sentItems = this.ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal(url.ToString(), request.Url.ToString());
            Assert.Equal("/path", request.Name);
            Assert.Equal("200", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestWithUrlOtherAttributesAreIgnored()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpIn";

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", url.ToString());
            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.path", "another path");
            span.SetAttribute("http.host", "another host");
            span.SetAttribute("http.port", 8080);
            span.SetAttribute("http.status_code", 200);

            var sentItems = this.ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal(url.ToString(), request.Url.ToString());
            Assert.Equal("POST another path", request.Name); // This check doesn't match Local Forwarder Assert.AreEqual("POST /path", request.Name);
            Assert.Equal("200", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestWithStringStatusCode()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);

            name = "HttpIn";
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.status_code", 201);

            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            var request = (RequestTelemetry)sentItems.Single();

            Assert.Equal("201", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestHostPortPathAttributes()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpIn";

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.path", "path");
            span.SetAttribute("http.host", "host");
            span.SetAttribute("http.port", 123);
            span.SetAttribute("http.status_code", 200);

            var sentItems = this.ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal("https://host:123/path", request.Url.ToString());
            Assert.Equal("POST path", request.Name);
            Assert.Equal("200", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestPortPathAndEmptyHostAttributes()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpIn";

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.path", "path");
            span.SetAttribute("http.host", "");
            span.SetAttribute("http.port", 123);
            span.SetAttribute("http.status_code", 200);

            var sentItems = this.ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal("/path", request.Url.ToString());  // This check doesn't match Local Forwarder Assert.IsNull(request.Url);
            Assert.Equal("POST path", request.Name);
            Assert.Equal("200", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestHostPathAttributes()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpIn";

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.path", "/path");
            span.SetAttribute("http.host", "host");
            span.SetAttribute("http.status_code", 200);

            var sentItems = this.ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal("https://host/path", request.Url.ToString());
            Assert.Equal("POST /path", request.Name);
            Assert.Equal("200", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestHostAttributes()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            name = "HttpIn";

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.host", "host");
            span.SetAttribute("http.status_code", 200);

            var sentItems = this.ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal("https://host/", request.Url.ToString());
            Assert.Equal("POST", request.Name);
            Assert.Equal("200", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestOnlyMethodAttributes()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            name = "HttpIn";

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.status_code", 200);

            var sentItems = this.ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Null(request.Url);
            Assert.Equal("POST", request.Name);
            Assert.Equal("200", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithStringStatusCode()
        {
            // ARRANGE
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.status_code", 201);

            // ACT
            var sentItems = this.ConvertSpan(span);

            // ASSERT
            var dependency = (DependencyTelemetry)sentItems.Single();

            Assert.Equal("201", dependency.ResultCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestUserAgent()
        {
            var url = new Uri("https://host/path");
            var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36";
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            name = "HttpIn";

            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", url.ToString());
            span.SetAttribute("http.user_agent", userAgent);

            var sentItems = this.ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal(userAgent, request.Context.User.UserAgent);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpDependencyWithUrl()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", url.ToString());
            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.status_code", 200);

            var sentItems = this.ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal(url.ToString(), dependency.Data);
            Assert.Equal("POST /path", dependency.Name);
            Assert.Equal("200", dependency.ResultCode);
            Assert.Equal("host", dependency.Target);
            Assert.Equal("Http", dependency.Type);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpDependencyWithRelativeUrl()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", url.LocalPath);
            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.status_code", 200);

            var sentItems = this.ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal(url.LocalPath, dependency.Data);
            Assert.Equal("POST /path", dependency.Name);
            Assert.Equal("200", dependency.ResultCode);
            Assert.Null(dependency.Target);
            Assert.Equal("Http", dependency.Type);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpDependencyWithUrlIgnoresHostPortPath()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", url.ToString());
            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.path", "another path");
            span.SetAttribute("http.port", 8080);
            span.SetAttribute("http.host", "another host");
            span.SetAttribute("http.status_code", 200);

            var sentItems = this.ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal(url.ToString(), dependency.Data);
            Assert.Equal("POST another path", dependency.Name);  // This check doesn't match Local Forwarder Assert.AreEqual("POST /path", dependency.Name);
            Assert.Equal("200", dependency.ResultCode);
            Assert.Equal("host", dependency.Target);
            Assert.Equal("Http", dependency.Type);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpDependencyWithHostPortPath()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.path", "/path");
            span.SetAttribute("http.host", "host");
            span.SetAttribute("http.port", 123);
            span.SetAttribute("http.status_code", 200);

            var sentItems = this.ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal("https://host:123/path", dependency.Data);
            Assert.Equal("POST /path", dependency.Name);
            Assert.Equal("200", dependency.ResultCode);
            Assert.Equal("host", dependency.Target);
            Assert.Equal("Http", dependency.Type);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpDependencyWithHostPort()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.host", "host");
            span.SetAttribute("http.port", 123);
            span.SetAttribute("http.status_code", 200);

            var sentItems = this.ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal("https://host:123/", dependency.Data);
            Assert.Equal("POST", dependency.Name);
            Assert.Equal("200", dependency.ResultCode);
            Assert.Equal("host", dependency.Target);
            Assert.Equal("Http", dependency.Type);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpDependencyWithPathAndEmptyHost()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.host", "");
            span.SetAttribute("http.path", "/path");
            span.SetAttribute("http.status_code", 200);

            var sentItems = this.ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal("/path", dependency.Data);  // This check doesn't match Local Forwarder Assert.IsNull(dependency.Data);
            Assert.Equal("POST /path", dependency.Name);
            Assert.Equal("200", dependency.ResultCode);
            Assert.True(string.IsNullOrEmpty(dependency.Target));
            Assert.Equal("Http", dependency.Type);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpDependencyWithHost()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.host", "host");
            span.SetAttribute("http.status_code", 200);

            var sentItems = this.ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal("https://host/", dependency.Data);
            Assert.Equal("POST", dependency.Name);
            Assert.Equal("200", dependency.ResultCode);
            Assert.Equal("host", dependency.Target);
            Assert.Equal("Http", dependency.Type);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpDependencyWithMethod()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.status_code", 200);

            var sentItems = this.ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Null(dependency.Data);
            Assert.Equal("POST", dependency.Name);
            Assert.Equal("200", dependency.ResultCode);
            Assert.Null(dependency.Target);
            Assert.Equal("Http", dependency.Type);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpDependencyWithStatusCodeOnly()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.status_code", 200);

            var sentItems = this.ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Null(dependency.Data);
            Assert.Equal("HttpOut", dependency.Name);  // This check doesn't match Local Forwarder
            Assert.Null(dependency.Target);
            Assert.Equal("Http", dependency.Type);
            Assert.Equal("200", dependency.ResultCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithCustomAttributes()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            name = "spanName";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("custom.stringAttribute", "string");
            span.SetAttribute("custom.longAttribute", long.MaxValue);
            span.SetAttribute("custom.boolAttribute", true);

            var sentItems = this.ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal("spanName", dependency.Name);
            Assert.Equal(3, dependency.Properties.Count);
            Assert.True(dependency.Properties.ContainsKey("custom.stringAttribute"));
            Assert.Equal("string", dependency.Properties["custom.stringAttribute"]);

            Assert.True(dependency.Properties.ContainsKey("custom.longAttribute"));
            Assert.Equal(long.MaxValue.ToString(), dependency.Properties["custom.longAttribute"]);

            Assert.True(dependency.Properties.ContainsKey("custom.boolAttribute"));
            Assert.Equal(bool.TrueString, dependency.Properties["custom.boolAttribute"]);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestsWithCustomAttributes()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            name = "spanName";
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("custom.stringAttribute", "string");
            span.SetAttribute("custom.longAttribute", long.MaxValue);
            span.SetAttribute("custom.boolAttribute", true);

            var sentItems = this.ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal("spanName", request.Name);
            Assert.Equal(3, request.Properties.Count);
            Assert.True(request.Properties.ContainsKey("custom.stringAttribute"));
            Assert.Equal("string", request.Properties["custom.stringAttribute"]);

            Assert.True(request.Properties.ContainsKey("custom.longAttribute"));
            Assert.Equal(long.MaxValue.ToString(), request.Properties["custom.longAttribute"]);

            Assert.True(request.Properties.ContainsKey("custom.boolAttribute"));
            Assert.Equal(bool.TrueString, request.Properties["custom.boolAttribute"]);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithLinks()
        {
            var (link0TraceId, link0TraceIdBytes) = GenerateRandomId(16);
            var (link1TraceId, link1TraceIdBytes) = GenerateRandomId(16);
            var (link2TraceId, link2TraceIdBytes) = GenerateRandomId(16);

            var (link0SpanId, link0SpanIdBytes) = GenerateRandomId(8);
            var (link1SpanId, link1SpanIdBytes) = GenerateRandomId(8);
            var (link2SpanId, link2SpanIdBytes) = GenerateRandomId(8);

            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            name = "spanName";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);
            span.AddLink(
                    Link.FromSpanContext(
                        new SpanContext(
                            ActivityTraceId.CreateFromBytes(link0TraceIdBytes),
                            ActivitySpanId.CreateFromBytes(link0SpanIdBytes),
                            ActivityTraceFlags.None,
                            Tracestate.Empty)));

            span.AddLink(
                Link.FromSpanContext(
                    new SpanContext(
                        ActivityTraceId.CreateFromBytes(link1TraceIdBytes),
                        ActivitySpanId.CreateFromBytes(link1SpanIdBytes),
                        ActivityTraceFlags.Recorded,
                        Tracestate.Empty)));

            span.AddLink(
                Link.FromSpanContext(
                    new SpanContext(
                        ActivityTraceId.CreateFromBytes(link2TraceIdBytes),
                        ActivitySpanId.CreateFromBytes(link2SpanIdBytes),
                        ActivityTraceFlags.None,
                        Tracestate.Empty)));

            var sentItems = this.ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Single(dependency.Properties);
            Assert.True(dependency.Properties.TryGetValue("_MS.links", out var linksStr));

            // does not throw
            var actualLinks = JsonConvert.DeserializeObject<ApplicationInsightsLink[]>(linksStr, jsonSettingThrowOnError);

            Assert.NotNull(actualLinks);
            Assert.Equal(3, actualLinks.Length);

            Assert.Equal(link0TraceId, actualLinks[0].operation_Id);
            Assert.Equal(link1TraceId, actualLinks[1].operation_Id);
            Assert.Equal(link2TraceId, actualLinks[2].operation_Id);

            Assert.Equal($"|{link0TraceId}.{link0SpanId}.", actualLinks[0].id);
            Assert.Equal($"|{link1TraceId}.{link1SpanId}.", actualLinks[1].id);
            Assert.Equal($"|{link2TraceId}.{link2SpanId}.", actualLinks[2].id);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithLinksAndAttributes()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            name = "spanName";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.AddLink(Link.FromSpanContext(
                        new SpanContext(
                            ActivityTraceId.CreateFromBytes(GenerateRandomId(16).Item2),
                            ActivitySpanId.CreateFromBytes(GenerateRandomId(8).Item2),
                            ActivityTraceFlags.None,
                            Tracestate.Empty),
                        new Dictionary<string, object>()
                        {
                            { "some.str.attribute", "foo" },
                            { "some.int.attribute", 1 },
                            { "some.bool.attribute", true },
                        }));

            var sentItems = this.ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();

            // attributes are ignored
            Assert.Single(dependency.Properties);
            Assert.True(dependency.Properties.TryGetValue("_MS.links", out var linksStr));

            // does not throw
            var actualLinks = JsonConvert.DeserializeObject<ApplicationInsightsLink[]>(linksStr, jsonSettingThrowOnError);

            Assert.NotNull(actualLinks);
            Assert.Single(actualLinks);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestWithLinks()
        {
            var (link0TraceId, link0TraceIdBytes) = GenerateRandomId(16);
            var (link1TraceId, link1TraceIdBytes) = GenerateRandomId(16);
            var (link2TraceId, link2TraceIdBytes) = GenerateRandomId(16);

            var (link0SpanId, link0SpanIdBytes) = GenerateRandomId(8);
            var (link1SpanId, link1SpanIdBytes) = GenerateRandomId(8);
            var (link2SpanId, link2SpanIdBytes) = GenerateRandomId(8);

            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            name = "spanName";
            kind = SpanKind.Server;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.AddLink(Link.FromSpanContext(
                new SpanContext(ActivityTraceId.CreateFromBytes(link0TraceIdBytes),
                    ActivitySpanId.CreateFromBytes(link0SpanIdBytes), ActivityTraceFlags.None, Tracestate.Empty)));

            span.AddLink(Link.FromSpanContext(
                new SpanContext(ActivityTraceId.CreateFromBytes(link1TraceIdBytes),
                    ActivitySpanId.CreateFromBytes(link1SpanIdBytes), ActivityTraceFlags.None, Tracestate.Empty)));

            span.AddLink(Link.FromSpanContext(
                new SpanContext(ActivityTraceId.CreateFromBytes(link2TraceIdBytes),
                    ActivitySpanId.CreateFromBytes(link2SpanIdBytes), ActivityTraceFlags.None, Tracestate.Empty)));

            var sentItems = this.ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Single(request.Properties);
            Assert.True(request.Properties.TryGetValue("_MS.links", out var linksStr));

            // does not throw
            var actualLinks = JsonConvert.DeserializeObject<ApplicationInsightsLink[]>(linksStr, jsonSettingThrowOnError);

            Assert.NotNull(actualLinks);
            Assert.Equal(3, actualLinks.Length);

            Assert.Equal(link0TraceId, actualLinks[0].operation_Id);
            Assert.Equal(link1TraceId, actualLinks[1].operation_Id);
            Assert.Equal(link2TraceId, actualLinks[2].operation_Id);

            Assert.Equal($"|{link0TraceId}.{link0SpanId}.", actualLinks[0].id);
            Assert.Equal($"|{link1TraceId}.{link1SpanId}.", actualLinks[1].id);
            Assert.Equal($"|{link2TraceId}.{link2SpanId}.", actualLinks[2].id);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestWithLinksAndAttributes()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            name = "spanName";
            kind = SpanKind.Server;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.AddLink(Link.FromSpanContext(
                        new SpanContext(
                            ActivityTraceId.CreateFromBytes(GenerateRandomId(16).Item2),
                            ActivitySpanId.CreateFromBytes(GenerateRandomId(8).Item2),
                            ActivityTraceFlags.None,
                            Tracestate.Empty),
                        new Dictionary<string, object>()
                        {
                            { "some.str.attribute", "foo" },
                            { "some.int.attribute", 1 },
                            { "some.bool.attribute", true },
                        }));

            var sentItems = this.ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            // attributes are ignored
            Assert.Single(request.Properties);
            Assert.True(request.Properties.TryGetValue("_MS.links", out var linksStr));

            // does not throw
            var actualLinks = JsonConvert.DeserializeObject<ApplicationInsightsLink[]>(linksStr, jsonSettingThrowOnError);

            Assert.NotNull(actualLinks);
            Assert.Single(actualLinks);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestWithEvents()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            name = "spanName";
            kind = SpanKind.Server;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.AddEvent(Event.Create("test message1", now));
            span.AddEvent(Event.Create("test message2", DateTime.UtcNow, new Dictionary<string, object>()
                        {
                            { "custom.stringAttribute", "string" },
                            { "custom.longAttribute", long.MaxValue },
                            { "custom.boolAttribute", true },
                        }));

            var sentItems = this.ConvertSpan(span);

            Assert.Equal(3, sentItems.Count);
            Assert.Single(sentItems.OfType<RequestTelemetry>());
            Assert.Equal(2, sentItems.OfType<TraceTelemetry>().Count());

            var request = sentItems.OfType<RequestTelemetry>().Single();
            var trace1 = sentItems.OfType<TraceTelemetry>().First();
            var trace2 = sentItems.OfType<TraceTelemetry>().Last();

            Assert.Equal(request.Context.Operation.Id, trace1.Context.Operation.Id);
            Assert.Equal(request.Context.Operation.Id, trace2.Context.Operation.Id);
            Assert.Equal(request.Id, trace1.Context.Operation.ParentId);
            Assert.Equal(request.Id, trace2.Context.Operation.ParentId);

            Assert.Equal("test message1", trace1.Message);
            Assert.Equal("test message2", trace2.Message);

            Assert.Equal(now, trace1.Timestamp);
            Assert.NotEqual(now, trace2.Timestamp);
            Assert.True(Math.Abs((DateTime.UtcNow - trace2.Timestamp).TotalSeconds) < 1);

            Assert.False(trace1.Properties.Any());
            Assert.Equal(3, trace2.Properties.Count);
            Assert.True(trace2.Properties.ContainsKey("custom.stringAttribute"));
            Assert.Equal("string", trace2.Properties["custom.stringAttribute"]);

            Assert.True(trace2.Properties.ContainsKey("custom.longAttribute"));
            Assert.Equal(long.MaxValue.ToString(), trace2.Properties["custom.longAttribute"]);

            Assert.True(trace2.Properties.ContainsKey("custom.boolAttribute"));
            Assert.Equal(bool.TrueString, trace2.Properties["custom.boolAttribute"]);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependenciesWithEvents()
        {
            this.GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var startTimestamp, out var attributes, out var events, out var links, out var status, out var kind, out var endTimestamp);
            name = "spanName";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, startTimestamp, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.AddEvent(Event.Create("test message1", now));
            span.AddEvent("test message2", new Dictionary<string, object>()
            {
                { "custom.stringAttribute", "string" },
                { "custom.longAttribute", long.MaxValue },
                { "custom.boolAttribute", true },
            });

            var sentItems = this.ConvertSpan(span);

            Assert.Equal(3, sentItems.Count);
            Assert.Single(sentItems.OfType<DependencyTelemetry>());
            Assert.Equal(2, sentItems.OfType<TraceTelemetry>().Count());

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            var trace1 = sentItems.OfType<TraceTelemetry>().First();
            var trace2 = sentItems.OfType<TraceTelemetry>().Last();

            Assert.Equal(dependency.Context.Operation.Id, trace1.Context.Operation.Id);
            Assert.Equal(dependency.Context.Operation.Id, trace2.Context.Operation.Id);
            Assert.Equal(dependency.Id, trace1.Context.Operation.ParentId);
            Assert.Equal(dependency.Id, trace2.Context.Operation.ParentId);

            Assert.Equal("test message1", trace1.Message);
            Assert.Equal("test message2", trace2.Message);

            Assert.Equal(now, trace1.Timestamp);
            Assert.NotEqual(now, trace2.Timestamp);
            Assert.True(Math.Abs((DateTime.UtcNow - trace2.Timestamp).TotalSeconds) < 1);

            Assert.False(trace1.Properties.Any());
            Assert.Equal(3, trace2.Properties.Count);
            Assert.True(trace2.Properties.ContainsKey("custom.stringAttribute"));
            Assert.Equal("string", trace2.Properties["custom.stringAttribute"]);

            Assert.True(trace2.Properties.ContainsKey("custom.longAttribute"));
            Assert.Equal(long.MaxValue.ToString(), trace2.Properties["custom.longAttribute"]);

            Assert.True(trace2.Properties.ContainsKey("custom.boolAttribute"));
            Assert.Equal(bool.TrueString, trace2.Properties["custom.boolAttribute"]);
        }

        private static (string, byte[]) GenerateRandomId(int byteCount)
        {
            var idBytes = new byte[byteCount];
            Rand.NextBytes(idBytes);

            var idString = BitConverter.ToString(idBytes).Replace("-", "").ToLower();

            return (idString, idBytes);
        }

        private static readonly Random Rand = new Random();

        private void GetDefaults(
            out ActivityTraceId traceId,
            out ActivitySpanId parentSpanId,
            out ActivityTraceFlags traceOptions,
            out Tracestate tracestate,
            out string name,
            out DateTime startTimestamp,
            out Dictionary<string, object> attributes,
            out List<IEvent> events,
            out List<ILink> links,
            out Status status,
            out SpanKind kind,
            out DateTime endTimestamp)
        {
            traceId = ActivityTraceId.CreateFromBytes(this.testTraceIdBytes);
            traceOptions = ActivityTraceFlags.Recorded;
            tracestate = Tracestate.Empty;
            parentSpanId = ActivitySpanId.CreateFromBytes(this.testParentSpanIdBytes);
            name = "spanName";
            startTimestamp = now.AddSeconds(-1);
            attributes = null;
            events = null;
            links = null;
            status = default;
            kind = SpanKind.Server;
            endTimestamp = now;
        }

        private class ApplicationInsightsLink
        {
            public string operation_Id { get; set; }

            public string id { get; set; }
        }

        internal static Span CreateTestSpan(string name,
            DateTime startTimestamp,
            ActivityTraceId traceId,
            ActivitySpanId parentSpanId,
            ActivityTraceFlags traceOptions,
            Tracestate tracestate,
            SpanKind kind,
            Status status)
        {
            var spanBuilder = Tracing.Tracer
                .SpanBuilder(name);

            if (parentSpanId != default)
            {
                spanBuilder.SetParent(new SpanContext(traceId, parentSpanId, traceOptions, tracestate));
            }
            var span = (Span)spanBuilder.SetSpanKind(kind)
                .SetStartTimestamp(startTimestamp)
                .StartSpan();

            if (status.IsValid)
            {
                span.Status = status;
            }

            return span;
        }
    }
};
