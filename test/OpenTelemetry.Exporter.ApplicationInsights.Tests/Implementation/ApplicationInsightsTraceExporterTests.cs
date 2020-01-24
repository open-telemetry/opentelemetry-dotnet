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
using OpenTelemetry.Resources;
using Moq;
using OpenTelemetry.Trace.Export;
using Xunit;
using Event = OpenTelemetry.Trace.Event;

namespace OpenTelemetry.Exporter.ApplicationInsights.Tests
{
    public class ApplicationInsightsTraceExporterTests
    {
        private const string TestTraceId = "d79bdda7eb9c4a9fa9bda52fe7b48b95";
        private const string TestParentSpanId = "9ba79c9fbd2fb495";
        private const string TestChannelEndpoint = "https://applicationinsights.com";

        private readonly byte[] testTraceIdBytes = { 0xd7, 0x9b, 0xdd, 0xa7, 0xeb, 0x9c, 0x4a, 0x9f, 0xa9, 0xbd, 0xa5, 0x2f, 0xe7, 0xb4, 0x8b, 0x95 };
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

        private ConcurrentQueue<ITelemetry> ConvertSpan(SpanData otSpan)
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
            exporter.ExportAsync(new List<SpanData> { otSpan }, CancellationToken.None).Wait();

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
            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions, tracestate, kind, status, 
                 new SpanCreationOptions { StartTimestamp = startTimestamp, }, endTimestamp);

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
            Assert.Equal("Ok", request.ResponseCode);

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

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
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

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
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

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
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

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = ConvertSpan(span);

            // ASSERT
            var request = (RequestTelemetry)sentItems.Single();

            Assert.True(request.Success.HasValue);
            Assert.True(request.Success.Value);
            Assert.Equal("Ok", request.ResponseCode); // this check doesn't match Local Forwarder Assert.IsTrue(string.IsNullOrEmpty(request.ResponseCode));
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestWithStatusAndDescription()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            status = Status.Ok.WithDescription("all good");

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = ConvertSpan(span);

            // ASSERT
            var request = (RequestTelemetry)sentItems.Single();

            Assert.True(request.Success.HasValue);
            Assert.True(request.Success.Value);
            Assert.Equal("Ok", request.ResponseCode);  // this check doesn't match Local Forwarder Assert.AreEqual("all good", request.ResponseCode);
            Assert.Equal("all good", request.Properties["statusDescription"]);  // this check doesn't match Local Forwarder
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestWithNonSuccessStatusAndDescription()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            status = Status.Cancelled.WithDescription("all bad");

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = ConvertSpan(span);

            // ASSERT
            var request = (RequestTelemetry)sentItems.Single();

            Assert.True(request.Success.HasValue);
            Assert.False(request.Success.Value);
            Assert.Equal("Cancelled", request.ResponseCode);  // this check doesn't match Local Forwarder Assert.AreEqual("all bad", request.ResponseCode);
            Assert.Equal("all bad", request.Properties["statusDescription"]);  // this check doesn't match Local Forwarder
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
            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, new SpanCreationOptions { StartTimestamp = startTimestamp, }, endTimestamp);

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

            Assert.Equal("Ok", dependency.ResultCode);
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
            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions, tracestate, kind, status,
                new SpanCreationOptions { StartTimestamp = startTimestamp, }, endTimestamp);

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

            Assert.Equal("Ok", dependency.ResultCode);
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

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.url"] = TestChannelEndpoint,
                    ["http.method"] = "POST",
                    ["http.status_code"] = 200,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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
            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions, tracestate, kind, status,
                new SpanCreationOptions { StartTimestamp = startTimestamp, }, endTimestamp);

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
            Assert.Equal("Ok", dependency.ResultCode);
            Assert.True(dependency.Success.HasValue);
            Assert.True(dependency.Success);

            // Assert.Equal("lf_unspecified-oc:0.0.0", dependency.Context.GetInternalContext().SdkVersion);

            Assert.Equal("Queue Message", dependency.Type);
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

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
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

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
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

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = ConvertSpan(span);

            // ASSERT
            var dependency = (DependencyTelemetry)sentItems.Single();

            Assert.True(dependency.Success.HasValue);
            Assert.True(dependency.Success.Value);
            Assert.Equal("Ok", dependency.ResultCode);
            Assert.False(dependency.Properties.ContainsKey("StatusDescription"));  // TODO: why it is upper case first letter?
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithStatusAndDescription()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Client;
            status = Status.Ok.WithDescription("all good");
            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = ConvertSpan(span);

            // ASSERT
            var dependency = (DependencyTelemetry)sentItems.Single();

            Assert.True(dependency.Success.HasValue);
            Assert.True(dependency.Success.Value);

            Assert.Equal("Ok", dependency.ResultCode);
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
            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            // ACT
            var sentItems = ConvertSpan(span);

            // ASSERT
            var dependency = (DependencyTelemetry)sentItems.Single();

            Assert.True(dependency.Success.HasValue);
            Assert.False(dependency.Success.Value);
            Assert.Equal("Cancelled", dependency.ResultCode);
            Assert.True(dependency.Properties.ContainsKey("statusDescription"));
            Assert.Equal("all bad", dependency.Properties["statusDescription"]);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestBasedOnClientSpanKindAttribute()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Client;

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["span.kind"] = "client",
                },
            };
            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

            var sentItems = ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestBasedOnProducerSpanKindAttribute()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Client;

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["span.kind"] = "producer",
                },
            };
            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

            var sentItems = ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestBasedOnConsumerSpanKindAttribute()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Consumer;

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            var sentItems = ConvertSpan(span);

            Assert.True(sentItems.Single() is RequestTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksRequestBasedOnSpanKindProperty()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            parentSpanId = ActivitySpanId.CreateFromBytes(testParentSpanIdBytes);
            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
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
            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            var sentItems = ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyBasedOnSpanKindAttribute()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Internal;

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["span.kind"] = "client",
                },
            };
            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

            var sentItems = ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyBasedOnSameProcessAsParentFlag()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Internal;
            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);
            var sentItems = ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyBasedOnSameProcessAsParentFlagNotSet()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Internal;
            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status);

            var sentItems = ConvertSpan(span);

            Assert.True(sentItems.Single() is DependencyTelemetry);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksDependencyWithoutKind()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Internal;
            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
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

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.url"] = url.ToString(),
                    ["http.method"] = "POST",
                    ["http.status_code"] = 409,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.url"] = url.LocalPath,
                    ["http.method"] = "POST",
                    ["http.status_code"] = 409,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.url"] = url.ToString(),
                    ["http.method"] = "POST",
                    ["http.route"] = "route",
                    ["http.status_code"] = 503,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.url"] = url.ToString(),
                    ["http.status_code"] = 200,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.url"] = url.ToString(),
                    ["http.method"] = "POST",
                    ["http.host"] = "another host",
                    ["http.path"] = "another path",
                    ["http.port"] = 8080,
                    ["http.status_code"] = 200,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.status_code"] = 201,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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
            
            name = "HttpIn";

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.method"] = "POST",
                    ["http.host"] = "host",
                    ["http.path"] = "path",
                    ["http.port"] = 123,
                    ["http.status_code"] = 200,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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
            name = "HttpIn";

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.method"] = "POST",
                    ["http.host"] = "",
                    ["http.path"] = "path",
                    ["http.port"] = 123,
                    ["http.status_code"] = 200,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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
            name = "HttpIn";

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.method"] = "POST",
                    ["http.host"] = "host",
                    ["http.path"] = "/path",
                    ["http.status_code"] = 200,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.method"] = "POST",
                    ["http.host"] = "host",
                    ["http.status_code"] = 200,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.method"] = "POST",
                    ["http.status_code"] = 200,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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
            name = "HttpOut";
            kind = SpanKind.Client;

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.status_code"] = 201,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.url"] = url.ToString(),
                    ["http.user_agent"] = userAgent,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.url"] = url.ToString(),
                    ["http.method"] = "POST",
                    ["http.status_code"] = 200,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

            var sentItems = ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal(url.ToString(), dependency.Data);
            Assert.Equal("POST /path", dependency.Name);
            Assert.Equal("200", dependency.ResultCode);
            Assert.Equal("host:123", dependency.Target);
            Assert.Equal("Http", dependency.Type);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpDependencyWithRelativeUrl()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            var url = new Uri("https://host:123/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.url"] = url.LocalPath,
                    ["http.method"] = "POST",
                    ["http.status_code"] = 200,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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
            var url = new Uri("https://host/path?query");
            name = "HttpOut";
            kind = SpanKind.Client;

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.url"] = url.ToString(),
                    ["http.method"] = "POST",
                    ["http.host"] = "another host",
                    ["http.path"] = "another path",
                    ["http.port"] = 8080,
                    ["http.status_code"] = 200,
                },
            };
            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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

            name = "HttpOut";
            kind = SpanKind.Client;

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.method"] = "POST",
                    ["http.host"] = "host",
                    ["http.path"] = "/path",
                    ["http.port"] = 123,
                    ["http.status_code"] = 200,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

            var sentItems = ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal("https://host:123/path", dependency.Data);
            Assert.Equal("POST /path", dependency.Name);
            Assert.Equal("200", dependency.ResultCode);
            Assert.Equal("host:123", dependency.Target);
            Assert.Equal("Http", dependency.Type);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpDependencyWithHostPort()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);

            name = "HttpOut";
            kind = SpanKind.Client;

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.status_code"] = 200,
                    ["http.method"] = "POST",
                    ["http.host"] = "host",
                    ["http.port"] = 123,
                },
            };
            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

            var sentItems = ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal("https://host:123", dependency.Data);
            Assert.Equal("POST", dependency.Name);
            Assert.Equal("200", dependency.ResultCode);
            Assert.Equal("host:123", dependency.Target);
            Assert.Equal("Http", dependency.Type);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpDependencyWithPathAndEmptyHost()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);

            name = "HttpOut";
            kind = SpanKind.Client;

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.status_code"] = 200,
                    ["http.method"] = "POST",
                    ["http.host"] = "",
                    ["http.path"] = "/path",
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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

            name = "HttpOut";
            kind = SpanKind.Client;

            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.method"] = "POST",
                    ["http.host"] = "host",
                    ["http.status_code"] = 200,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

            var sentItems = ConvertSpan(span);

            var dependency = sentItems.OfType<DependencyTelemetry>().Single();
            Assert.Equal("https://host", dependency.Data);
            Assert.Equal("POST", dependency.Name);
            Assert.Equal("200", dependency.ResultCode);
            Assert.Equal("host", dependency.Target);
            Assert.Equal("Http", dependency.Type);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksHttpDependencyWithMethod()
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);

            name = "HttpOut";
            kind = SpanKind.Client;
            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.method"] = "POST",
                    ["http.status_code"] = 200,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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

            name = "HttpOut";
            kind = SpanKind.Client;
            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["http.status_code"] = 200,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);


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
            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["custom.stringAttribute"] = "string",
                    ["custom.longAttribute"] = long.MaxValue,
                    ["custom.boolAttribute"] = true,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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
            var spanOptions = new SpanCreationOptions
            {
                Attributes = new Dictionary<string, object>
                {
                    ["custom.stringAttribute"] = "string",
                    ["custom.longAttribute"] = long.MaxValue,
                    ["custom.boolAttribute"] = true,
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, spanOptions);

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

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
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

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, new SpanCreationOptions { Links = new [] { parentLink }});

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

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
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

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, new SpanCreationOptions { Links = parentLinks });

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

            var now = DateTimeOffset.UtcNow.AddSeconds(-1);
            events = new List<Event> {
                new Event("test message1", now),
                new Event("test message2", DateTime.UtcNow, new Dictionary<string, object>
                {
                    { "custom.stringAttribute", "string" },
                    { "custom.longAttribute", long.MaxValue },
                    { "custom.boolAttribute", true },
                }),
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, null, default, events);

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

            var now = DateTimeOffset.UtcNow.AddSeconds(-1);
            events = new List<Event> {
                new Event("test message1", now),
                new Event("test message2", new Dictionary<string, object>
                {
                    { "custom.stringAttribute", "string" },
                    { "custom.longAttribute", long.MaxValue },
                    { "custom.boolAttribute", true },
                }),
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, kind, status, null, default, events);

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

        [Theory]
        [InlineData(SpanKind.Client, typeof(DependencyTelemetry))]
        [InlineData(SpanKind.Server, typeof(RequestTelemetry))]
        public void OpenTelemetryTelemetryConverterTests_TracksWithServiceName(SpanKind spanKind, Type telemetryType)
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            name = "spanName";
            
            var resource = new[] { new KeyValuePair<string, object>("service.name", "my-service") };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, spanKind, status, null, default, new[] { new Event("test message1") }, new Resource(resource));
            var sentItems = ConvertSpan(span);

            var requestOrDependency = sentItems.Single(t => t.GetType() == telemetryType);
            var log = sentItems.OfType<TraceTelemetry>().Single();

            Assert.Equal(telemetryType, requestOrDependency.GetType());
            Assert.Equal("my-service", requestOrDependency.Context.Cloud.RoleName);
            Assert.Equal("my-service", log.Context.Cloud.RoleName);

            Assert.Null(requestOrDependency.Context.Component.Version);
            Assert.Null(log.Context.Component.Version);
        }


        [Theory]
        [InlineData(SpanKind.Client, typeof(DependencyTelemetry))]
        [InlineData(SpanKind.Server, typeof(RequestTelemetry))]
        public void OpenTelemetryTelemetryConverterTests_TrackWithResource(SpanKind spanKind, Type telemetryType)
        {
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name,
                out var attributes, out var events, out var links, out var status, out var kind);
            name = "spanName";

            var resource = new[]
            {
                new KeyValuePair<string, object>("service.name", "my-service"),
                new KeyValuePair<string, object>("service.namespace", "my-service-namespace"),
                new KeyValuePair<string, object>("service.instance.id", "my-instance-id"),
                new KeyValuePair<string, object>("service.version", "my-service-version"),
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions,
                tracestate, spanKind, status, null, default, new[] {new Event("test message1")},
                new Resource(resource));
            var sentItems = ConvertSpan(span);

            var requestOrDependency = sentItems.Single(t => t.GetType() == telemetryType);
            var log = sentItems.OfType<TraceTelemetry>().Single();

            Assert.Equal("my-service-namespace.my-service", requestOrDependency.Context.Cloud.RoleName);
            Assert.Equal("my-service-namespace.my-service", log.Context.Cloud.RoleName);

            Assert.Equal("my-instance-id", requestOrDependency.Context.Cloud.RoleInstance);
            Assert.Equal("my-instance-id", log.Context.Cloud.RoleInstance);

            Assert.Equal("my-service-version", requestOrDependency.Context.Component.Version);
            Assert.Equal("my-service-version", log.Context.Component.Version);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksEventHubsRequestWithComponent()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);

            var endTimestamp = DateTimeOffset.UtcNow;
            var startTimestamp = endTimestamp.AddSeconds(-1);
            parentSpanId = default;

            var options = new SpanCreationOptions
            {
                StartTimestamp = startTimestamp,
                Attributes = new Dictionary<string, object>
                {
                    ["component"] = "eventhubs",
                    ["message_bus.destination"] = "queueName",
                    ["peer.address"] = "sb://endpoint.com/123",
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions, tracestate, kind, status, options, endTimestamp);

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
            Assert.Equal("Ok", request.ResponseCode);
            Assert.Equal("sb://endpoint.com/123 | queueName", request.Source);

            Assert.StartsWith("ot:", request.Context.GetInternalContext().SdkVersion);

            Assert.Empty(request.Properties);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksEventHubsRequestWithAzNamespace()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);

            var endTimestamp = DateTimeOffset.UtcNow;
            var startTimestamp = endTimestamp.AddSeconds(-1);
            parentSpanId = default;

            var options = new SpanCreationOptions
            {
                StartTimestamp = startTimestamp,
                Attributes = new Dictionary<string, object>
                {
                    ["az.namespace"] = "Microsoft.EventHub",
                    ["message_bus.destination"] = "queueName",
                    ["peer.address"] = "sb://endpoint.com/123",
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions, tracestate, kind, status, options, endTimestamp);

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
            Assert.Equal("Ok", request.ResponseCode);
            Assert.Equal("sb://endpoint.com/123 | queueName", request.Source);

            Assert.StartsWith("ot:", request.Context.GetInternalContext().SdkVersion);
            Assert.Empty(request.Properties);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksEventHubsDependencyWithComponent()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Client;

            var endTimestamp = DateTimeOffset.UtcNow;
            var startTimestamp = endTimestamp.AddSeconds(-1);
            parentSpanId = default;

            var options = new SpanCreationOptions
            {
                StartTimestamp = startTimestamp,
                Attributes = new Dictionary<string, object>
                {
                    ["component"] = "eventhubs",
                    ["message_bus.destination"] = "queueName",
                    ["peer.address"] = "sb://endpoint.com/123",
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions, tracestate, kind, status, options, endTimestamp);

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
            Assert.Equal(span.Context.SpanId.ToHexString(), dependency.Id);
            Assert.Null(dependency.Context.Operation.ParentId);

            Assert.True(dependency.Success);
            Assert.Equal("Ok", dependency.ResultCode);
            Assert.Equal("sb://endpoint.com/123 | queueName", dependency.Target);
            Assert.Equal("Azure Event Hubs", dependency.Type);

            Assert.StartsWith("ot:", dependency.Context.GetInternalContext().SdkVersion);
            Assert.Empty(dependency.Properties);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksEventHubsDependencyWithAzNamespace()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Client;

            var endTimestamp = DateTimeOffset.UtcNow;
            var startTimestamp = endTimestamp.AddSeconds(-1);
            parentSpanId = default;

            var options = new SpanCreationOptions
            {
                StartTimestamp = startTimestamp,
                Attributes = new Dictionary<string, object>
                {
                    ["az.namespace"] = "Microsoft.EventHub",
                    ["message_bus.destination"] = "queueName",
                    ["peer.address"] = "sb://endpoint.com/123",
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions, tracestate, kind, status, options, endTimestamp);

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
            Assert.Equal(span.Context.SpanId.ToHexString(), dependency.Id);
            Assert.Null(dependency.Context.Operation.ParentId);

            Assert.True(dependency.Success);
            Assert.Equal("Ok", dependency.ResultCode);
            Assert.Equal("sb://endpoint.com/123 | queueName", dependency.Target);
            Assert.Equal("Azure Event Hubs", dependency.Type);

            Assert.StartsWith("ot:", dependency.Context.GetInternalContext().SdkVersion);
            Assert.Empty(dependency.Properties);
        }

        [Fact]
        public void OpenTelemetryTelemetryConverterTests_TracksEventHubsDependencyProducer()
        {
            // ARRANGE
            GetDefaults(out var traceId, out var parentSpanId, out var traceOptions, out var tracestate, out var name, out var attributes, out var events, out var links, out var status, out var kind);
            kind = SpanKind.Producer;

            var endTimestamp = DateTimeOffset.UtcNow;
            var startTimestamp = endTimestamp.AddSeconds(-1);
            parentSpanId = default;

            var options = new SpanCreationOptions
            {
                StartTimestamp = startTimestamp,
                Attributes = new Dictionary<string, object>
                {
                    ["az.namespace"] = "Microsoft.EventHub",
                },
            };

            var span = CreateSpanData(name, traceId, parentSpanId, traceOptions, tracestate, kind, status, options, endTimestamp);

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
            Assert.Equal(span.Context.SpanId.ToHexString(), dependency.Id);
            Assert.Null(dependency.Context.Operation.ParentId);

            Assert.True(dependency.Success);
            Assert.Equal("Ok", dependency.ResultCode);
            Assert.Equal("Queue Message", dependency.Type);

            Assert.StartsWith("ot:", dependency.Context.GetInternalContext().SdkVersion);
            Assert.Empty(dependency.Properties);
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

        internal SpanData CreateSpanData(string name,
            ActivityTraceId traceId,
            ActivitySpanId parentSpanId,
            ActivityTraceFlags traceOptions,
            List<KeyValuePair<string, string>> tracestate,
            SpanKind kind,
            Status status,
            SpanCreationOptions options = null,
            DateTimeOffset endTimestamp = default,
            IEnumerable<Event> events = null,
            Resource resource = null)
        {
            var processor = new Mock<SpanProcessor>();

            processor.Setup(p => p.OnEnd(It.IsAny<SpanData>()));

            var tracer = TracerFactory.Create(b =>b
                    .SetResource(resource)
                    .AddProcessorPipeline(p => p.AddProcessor(_ => processor.Object)))
                .GetTracer(null);

            var parentContext = new SpanContext(traceId, parentSpanId, traceOptions, false, tracestate);
            var span = tracer.StartSpan(name, parentContext, kind, options);

            if (events != null)
            {
                foreach (var evnt in events)
                {
                    span.AddEvent(evnt);
                }
            }

            span.Status = status.IsValid ? status : Status.Ok;
            if (endTimestamp == default)
            {
                span.End();
            }
            else
            {
                span.End(endTimestamp);
            }

            return (SpanData)processor.Invocations[0].Arguments[0];
        }
    }
};
