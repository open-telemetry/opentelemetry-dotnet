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

using OpenTelemetry.Trace.Configuration;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Newtonsoft.Json;
using OpenTelemetry.Trace;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Xunit;

namespace OpenTelemetry.Exporter.ApplicationInsights.Tests
{
    public class ApplicationInsightsTraceExporterTests
    {
        private const string TestTraceId = "d79bdda7eb9c4a9fa9bda52fe7b48b95";
        private const string TestSpanId = "d7ddeb4aa9a5e78b";
        private const string TestParentSpanId = "9ba79c9fbd2fb495";
        private const string TestChannelEndpoint = "https://applicationinsights.com";

        private readonly byte[] testTraceIdBytes = { 0xd7, 0x9b, 0xdd, 0xa7, 0xeb, 0x9c, 0x4a, 0x9f, 0xa9, 0xbd, 0xa5, 0x2f, 0xe7, 0xb4, 0x8b, 0x95 };
        private readonly byte[] testSpanIdBytes = { 0xd7, 0xdd, 0xeb, 0x4a, 0xa9, 0xa5, 0xe7, 0x8b };
        private readonly byte[] testParentSpanIdBytes = { 0x9b, 0xa7, 0x9c, 0x9f, 0xbd, 0x2f, 0xb4, 0x95 };
        private readonly Tracer tracer;
        private readonly JsonSerializerSettings jsonSettingThrowOnError = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            ReferenceLoopHandling = ReferenceLoopHandling.Error,
            NullValueHandling = NullValueHandling.Include,
            DefaultValueHandling = DefaultValueHandling.Include,
        };

        public ApplicationInsightsTraceExporterTests()
        {
            tracer = TracerFactory.Create(_ => { }).GetTracer(null);
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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);

            var endTimestamp = DateTimeOffset.UtcNow;
            var startTimestamp = endTimestamp.AddSeconds(-1);
            parentSpanId = default;
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions, tracestate, kind, status, 
                new SpanCreationOptions
                {
                    StartTimestamp = startTimestamp,
                });

            span.End(endTimestamp);
            // ACT
            var sentItems = ConvertSpan(span);

            // ASSERT
            Assert.Single(sentItems);
            Assert.True(sentItems.Single() is RequestTelemetry);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal("spanName", request.Name);
            Assert.Equal(startTimestamp, request.Timestamp);
            Assert.Equal(1, request.Duration.TotalSeconds);

            Assert.Equal(span.Context.TraceId.ToHexString(), request.Context.Operation.Id);

            Assert.Equal(span.Context.SpanId.ToHexString(), request.Id);
            Assert.Null(request.Context.Operation.ParentId);

            Assert.True(request.Success);
            Assert.Equal("0", request.ResponseCode);

            Assert.StartsWith("ot:", request.Context.GetInternalContext().SdkVersion);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestWithTracestate()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);

            tracestate = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("k1", "v1"),
                new KeyValuePair<string, string>("k2", "v2"),
            };

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, SpanKind.Server, status);

            // ACT
            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            parentSpanId = ActivitySpanId.CreateFromBytes(testParentSpanIdBytes);

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = ConvertSpan(span);

            // ASSERT
            Assert.Equal(TestParentSpanId, ((RequestTelemetry)sentItems.Single()).Context.Operation.ParentId);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestWithInvalidParent()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            parentSpanId = default;

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = ConvertSpan(span);

            // ASSERT
            Assert.Null(((RequestTelemetry)sentItems.Single()).Context.Operation.ParentId);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestWithStatus()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            status = Status.Ok;

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            status = Status.Ok.WithDescription("all good");

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            status = Status.Cancelled.WithDescription("all bad");

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("error", true);
            span.End();

            var sentItems = ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.True(request.Success.HasValue);
            Assert.False(request.Success.Value);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksClientDependency()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Client;
            parentSpanId = default;

            var endTimestamp = DateTimeOffset.UtcNow;
            var startTimestamp = endTimestamp.AddSeconds(-1);
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, new SpanCreationOptions
                {
                    StartTimestamp = startTimestamp,
                });
            span.End(endTimestamp);
            // ACT
            var sentItems = ConvertSpan(span);

            // ASSERT
            Assert.Single(sentItems);
            Assert.True(sentItems.Single() is DependencyTelemetry);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal("spanName", dependency.Name);
            Assert.Equal(startTimestamp, dependency.Timestamp);
            Assert.Equal(1, dependency.Duration.TotalSeconds);

            Assert.Equal(span.Context.TraceId.ToHexString(), dependency.Context.Operation.Id);
            Assert.Null(dependency.Context.Operation.ParentId);
            Assert.Equal(span.Context.SpanId.ToHexString(), dependency.Id);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Internal;

            var endTimestamp = DateTimeOffset.UtcNow;
            var startTimestamp = endTimestamp.AddSeconds(-1);
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions, tracestate, kind, status, new SpanCreationOptions
            {
                StartTimestamp = startTimestamp,
            });
            span.End(endTimestamp);
            // ACT
            var sentItems = ConvertSpan(span);

            // ASSERT
            Assert.Single(sentItems);
            Assert.True(sentItems.Single() is DependencyTelemetry);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal("spanName", dependency.Name);
            Assert.Equal("InProc", dependency.Type);
            Assert.Equal(startTimestamp, dependency.Timestamp);
            Assert.Equal(1, dependency.Duration.TotalSeconds);

            Assert.Equal(TestTraceId, dependency.Context.Operation.Id);

            Assert.Equal(span.Context.SpanId.ToHexString(), dependency.Id);
            Assert.Equal(parentSpanId.ToHexString(), dependency.Context.Operation.ParentId);

            Assert.Equal("0", dependency.ResultCode);
            Assert.True(dependency.Success.HasValue);
            Assert.True(dependency.Success);

            // Assert.Equal("lf_unspecified-oc:0.0.0", dependency.Context.GetInternalContext().SdkVersion);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_DoesNotTrackCallToAppInsights()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Client;

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", TestChannelEndpoint);
            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.status_code", 200);
            span.End();

            // ACT
            var sentItems = ConvertSpan(span);

            // ASSERT
            Assert.Empty(sentItems);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksProducerDependency()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Producer;

            var endTimestamp = DateTimeOffset.UtcNow;
            var startTimestamp = endTimestamp.AddSeconds(-1);
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions, tracestate, kind, status, new SpanCreationOptions
            {
                StartTimestamp = startTimestamp,
            });

            span.End(endTimestamp);

            // ACT
            var sentItems = ConvertSpan(span);

            // ASSERT
            Assert.Single(sentItems);
            Assert.True(sentItems.Single() is DependencyTelemetry);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal("spanName", dependency.Name);
            Assert.Equal(startTimestamp, dependency.Timestamp);
            Assert.Equal(1, dependency.Duration.TotalSeconds);

            Assert.Equal(TestTraceId, dependency.Context.Operation.Id);

            Assert.Equal(span.Context.SpanId.ToHexString(), dependency.Id);
            Assert.Equal(ActivitySpanId.CreateFromBytes(testParentSpanIdBytes).ToHexString(), dependency.Context.Operation.ParentId);
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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            tracestate = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("k1", "v1"),
                new KeyValuePair<string, string>("k2", "v2"),
            };

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, SpanKind.Client, status);

            // ACT
            var sentItems = ConvertSpan(span);

            // ASSERT
            Assert.Single(sentItems);
            Assert.True(sentItems.Single() is DependencyTelemetry);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();

            Assert.Equal(2, dependency.Properties.Count);
            Assert.Equal("v1", dependency.Properties["k1"]);
            Assert.Equal("v2", dependency.Properties["k2"]);
            // Assert.Equal("lf_unspecified-oc:0.0.0", dependency.Context.GetInternalContext().SdkVersion);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithParent()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Client;
            parentSpanId = ActivitySpanId.CreateFromBytes(testParentSpanIdBytes);

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = ConvertSpan(span);

            // ASSERT
            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal(TestParentSpanId, dependency.Context.Operation.ParentId);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithStatus()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Client;
            status = Status.Ok;

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Client;
            status = Status.Ok.WithDescription("all good");
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Client;
            status = Status.Cancelled.WithDescription("all bad");
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Client;

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);
            span.SetAttribute("error", true);
            span.End();

            var sentItems = ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.True(dependency.Success.HasValue);
            Assert.False(dependency.Success.Value);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestBasedOnClientSpanKindAttribute()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Client;

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);
            span.SetAttribute("span.kind", "client");
            span.End();

            var sentItems = ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestBasedOnProducerSpanKindAttribute()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Client;

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("span.kind", "producer");
            var sentItems = ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestBasedOnConsumerSpanKindAttribute()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Consumer;

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            var sentItems = ConvertSpan(span);

            Assert.True(sentItems.Single() is RequestTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestBasedOnSpanKindProperty()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            parentSpanId = ActivitySpanId.CreateFromBytes(testParentSpanIdBytes);
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            var sentItems = ConvertSpan(span);

            Assert.True(sentItems.Single() is RequestTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyBasedOnSpanKindProperty()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Client;
            parentSpanId = ActivitySpanId.CreateFromBytes(testParentSpanIdBytes);
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            var sentItems = ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyBasedOnSpanKindAttribute()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Internal;

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("span.kind", "client");
            span.End();

            var sentItems = ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyBasedOnSameProcessAsParentFlag()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Internal;
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);
            var sentItems = ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyBasedOnSameProcessAsParentFlagNotSet()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Internal;
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            var sentItems = ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithoutKind()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Internal;
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            var sentItems = ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestWithUrl()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpIn";
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);
            span.SetAttribute("http.url", url.ToString());
            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.status_code", 409);

            var sentItems = ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal(url.ToString(), request.Url.ToString());
            Assert.Equal("POST /path", request.Name);
            Assert.Equal("409", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestWithRelativeUrl()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpIn";

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", url.LocalPath);
            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.status_code", 409);

            var sentItems = ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal("/path", request.Url.ToString()); // This check doesn't match Local Forwarder Assert.Null(request.Url);
            Assert.Equal("POST /path", request.Name);
            Assert.Equal("409", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestWithUrlAndRoute()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpIn";
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", url.ToString());
            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.route", "route");
            span.SetAttribute("http.status_code", 503);

            var sentItems = ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal(url.ToString(), request.Url.ToString());
            Assert.Equal("POST route", request.Name);
            Assert.Equal("503", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestWithUrlAndNoMethod()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpIn";
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", url.ToString());
            span.SetAttribute("http.status_code", 200);
            var sentItems = ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal(url.ToString(), request.Url.ToString());
            Assert.Equal("/path", request.Name);
            Assert.Equal("200", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestWithUrlOtherAttributesAreIgnored()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpIn";

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", url.ToString());
            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.path", "another path");
            span.SetAttribute("http.host", "another host");
            span.SetAttribute("http.port", 8080);
            span.SetAttribute("http.status_code", 200);

            var sentItems = ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal(url.ToString(), request.Url.ToString());
            Assert.Equal("POST another path", request.Name); // This check doesn't match Local Forwarder Assert.AreEqual("POST /path", request.Name);
            Assert.Equal("200", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestWithStringStatusCode()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);

            name = "HttpIn";
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.status_code", 201);

            // ACT
            var sentItems = ConvertSpan(span);

            // ASSERT
            var request = (RequestTelemetry)sentItems.Single();

            Assert.Equal("201", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestHostPortPathAttributes()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpIn";

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.path", "path");
            span.SetAttribute("http.host", "host");
            span.SetAttribute("http.port", 123);
            span.SetAttribute("http.status_code", 200);

            var sentItems = ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal("https://host:123/path", request.Url.ToString());
            Assert.Equal("POST path", request.Name);
            Assert.Equal("200", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestPortPathAndEmptyHostAttributes()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpIn";

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.path", "path");
            span.SetAttribute("http.host", "");
            span.SetAttribute("http.port", 123);
            span.SetAttribute("http.status_code", 200);

            var sentItems = ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal("/path", request.Url.ToString());  // This check doesn't match Local Forwarder Assert.IsNull(request.Url);
            Assert.Equal("POST path", request.Name);
            Assert.Equal("200", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestHostPathAttributes()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpIn";

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.path", "/path");
            span.SetAttribute("http.host", "host");
            span.SetAttribute("http.status_code", 200);

            var sentItems = ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal("https://host/path", request.Url.ToString());
            Assert.Equal("POST /path", request.Name);
            Assert.Equal("200", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestHostAttributes()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            name = "HttpIn";

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.host", "host");
            span.SetAttribute("http.status_code", 200);

            var sentItems = ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal("https://host/", request.Url.ToString());
            Assert.Equal("POST", request.Name);
            Assert.Equal("200", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestOnlyMethodAttributes()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            name = "HttpIn";

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.status_code", 200);

            var sentItems = ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Null(request.Url);
            Assert.Equal("POST", request.Name);
            Assert.Equal("200", request.ResponseCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithStringStatusCode()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.status_code", 201);

            // ACT
            var sentItems = ConvertSpan(span);

            // ASSERT
            var dependency = (DependencyTelemetry)sentItems.Single();

            Assert.Equal("201", dependency.ResultCode);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpRequestUserAgent()
        {
            var url = new Uri("https://host/path");
            var userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/67.0.3396.99 Safari/537.36";
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            name = "HttpIn";

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", url.ToString());
            span.SetAttribute("http.user_agent", userAgent);

            var sentItems = ConvertSpan(span);

            var request = sentItems.OfType<RequestTelemetry>().Single();
            Assert.Equal(userAgent, request.Context.User.UserAgent);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpDependencyWithUrl()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", url.ToString());
            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.status_code", 200);

            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", url.LocalPath);
            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.status_code", 200);

            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.url", url.ToString());
            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.path", "another path");
            span.SetAttribute("http.port", 8080);
            span.SetAttribute("http.host", "another host");
            span.SetAttribute("http.status_code", 200);

            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.path", "/path");
            span.SetAttribute("http.host", "host");
            span.SetAttribute("http.port", 123);
            span.SetAttribute("http.status_code", 200);

            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.host", "host");
            span.SetAttribute("http.port", 123);
            span.SetAttribute("http.status_code", 200);

            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.host", "");
            span.SetAttribute("http.path", "/path");
            span.SetAttribute("http.status_code", 200);

            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.host", "host");
            span.SetAttribute("http.status_code", 200);

            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.method", "POST");
            span.SetAttribute("http.status_code", 200);

            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("http.status_code", 200);

            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            name = "spanName";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("custom.stringAttribute", "string");
            span.SetAttribute("custom.longAttribute", long.MaxValue);
            span.SetAttribute("custom.boolAttribute", true);

            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            name = "spanName";
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            span.SetAttribute("custom.stringAttribute", "string");
            span.SetAttribute("custom.longAttribute", long.MaxValue);
            span.SetAttribute("custom.boolAttribute", true);

            var sentItems = ConvertSpan(span);

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

            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            name = "spanName";
            kind = SpanKind.Client;

            var parentLinks = new[]
            {
                new Link(
                    new SpanContext(
                        ActivityTraceId.CreateFromBytes(link0TraceIdBytes),
                        ActivitySpanId.CreateFromBytes(link0SpanIdBytes),
                        ActivityTraceFlags.None)),
                new Link(
                    new SpanContext(
                        ActivityTraceId.CreateFromBytes(link1TraceIdBytes),
                        ActivitySpanId.CreateFromBytes(link1SpanIdBytes),
                        ActivityTraceFlags.Recorded)),
                new Link(
                    new SpanContext(
                        ActivityTraceId.CreateFromBytes(link2TraceIdBytes),
                        ActivitySpanId.CreateFromBytes(link2SpanIdBytes),
                        ActivityTraceFlags.None)),
            };

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, new SpanCreationOptions { Links = parentLinks });

            var sentItems = ConvertSpan(span);

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

            Assert.Equal(link0SpanId, actualLinks[0].id);
            Assert.Equal(link1SpanId, actualLinks[1].id);
            Assert.Equal(link2SpanId, actualLinks[2].id);
            
            Assert.Equal($"[{{\"operation_Id\":\"{link0TraceId}\",\"id\":\"{link0SpanId}\"}},{{\"operation_Id\":\"{link1TraceId}\",\"id\":\"{link1SpanId}\"}},{{\"operation_Id\":\"{link2TraceId}\",\"id\":\"{link2SpanId}\"}}]", linksStr);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithLinksAndAttributes()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            name = "spanName";
            kind = SpanKind.Client;

            var parentLink = new Link(
                new SpanContext(
                    ActivityTraceId.CreateFromBytes(GenerateRandomId(16).Item2),
                    ActivitySpanId.CreateFromBytes(GenerateRandomId(8).Item2),
                    ActivityTraceFlags.None),
                new Dictionary<string, object>()
                {
                    {"some.str.attribute", "foo"}, {"some.int.attribute", 1}, {"some.bool.attribute", true},
                });

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, new SpanCreationOptions { LinksFactory = () => new [] {parentLink}});

            var sentItems = ConvertSpan(span);

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

            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            name = "spanName";
            kind = SpanKind.Server;

            var parentLinks = new[]
            {
                new Link(
                    new SpanContext(ActivityTraceId.CreateFromBytes(link0TraceIdBytes),
                        ActivitySpanId.CreateFromBytes(link0SpanIdBytes), ActivityTraceFlags.None)),
                new Link(
                    new SpanContext(ActivityTraceId.CreateFromBytes(link1TraceIdBytes),
                        ActivitySpanId.CreateFromBytes(link1SpanIdBytes), ActivityTraceFlags.None)),
                new Link(
                    new SpanContext(ActivityTraceId.CreateFromBytes(link2TraceIdBytes),
                        ActivitySpanId.CreateFromBytes(link2SpanIdBytes), ActivityTraceFlags.None)),
            };

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, new SpanCreationOptions { Links = parentLinks });

            var sentItems = ConvertSpan(span);

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

            Assert.Equal(link0SpanId, actualLinks[0].id);
            Assert.Equal(link1SpanId, actualLinks[1].id);
            Assert.Equal(link2SpanId, actualLinks[2].id);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestWithLinksAndAttributes()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            name = "spanName";
            kind = SpanKind.Server;

            var parentLinks = new[]
            {
                new Link(
                    new SpanContext(
                        ActivityTraceId.CreateFromBytes(GenerateRandomId(16).Item2),
                        ActivitySpanId.CreateFromBytes(GenerateRandomId(8).Item2),
                        ActivityTraceFlags.None),
                    new Dictionary<string, object>()
                    {
                        {"some.str.attribute", "foo"}, {"some.int.attribute", 1}, {"some.bool.attribute", true},
                    }),
            };

            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, new SpanCreationOptions { LinksFactory = () => parentLinks });

            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            name = "spanName";
            kind = SpanKind.Server;
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            var now = DateTimeOffset.UtcNow.AddSeconds(-1);
            span.AddEvent(new Event("test message1", now));
            span.AddEvent(new Event("test message2", DateTime.UtcNow, new Dictionary<string, object>()
                        {
                            { "custom.stringAttribute", "string" },
                            { "custom.longAttribute", long.MaxValue },
                            { "custom.boolAttribute", true },
                        }));

            var sentItems = ConvertSpan(span);

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
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            name = "spanName";
            kind = SpanKind.Client;
            var span = CreateTestSpan(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            var now = DateTimeOffset.UtcNow.AddSeconds(-1);

            span.AddEvent(new Event("test message1", now));
            span.AddEvent(new Event("test message2", new Dictionary<string, object>()
            {
                { "custom.stringAttribute", "string" },
                { "custom.longAttribute", long.MaxValue },
                { "custom.boolAttribute", true },
            }));

            var sentItems = ConvertSpan(span);

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
            out List<KeyValuePair<string, string>> tracestate,
            out string name,
            out Dictionary<string, object> attributes,
            out List<Event> events,
            out List<Link> links,
            out Status status,
            out SpanKind kind)
        {
            traceId = ActivityTraceId.CreateFromBytes(testTraceIdBytes);
            traceOptions = ActivityTraceFlags.Recorded;
            tracestate = null;
            parentSpanId = ActivitySpanId.CreateFromBytes(testParentSpanIdBytes);
            name = "spanName";
            attributes = null;
            events = null;
            links = null;
            status = default;
            kind = SpanKind.Server;
        }

        private class ApplicationInsightsLink
        {
            public string operation_Id { get; set; }

            public string id { get; set; }
        }

        internal Span CreateTestSpan(string name,
            ActivityTraceId traceId,
            ActivitySpanId parentSpanId,
            ActivityTraceFlags traceOptions,
            List<KeyValuePair<string, string>> tracestate,
            SpanKind kind,
            Status status,
            SpanCreationOptions options = null)
        {
            var span = parentSpanId == default ? 
                tracer.StartRootSpan(name, kind, options) :
                tracer.StartSpan(name, new SpanContext(traceId, parentSpanId, traceOptions, false, tracestate), kind, options);

            if (status.IsValid)
            {
                span.Status = status;
            }

            return (Span)span;
        }
    }
};
