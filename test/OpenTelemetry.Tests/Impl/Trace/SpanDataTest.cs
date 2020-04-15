// <copyright file="SpanDataTest.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using OpenTelemetry.Trace.Export;
using Xunit;

namespace OpenTelemetry.Tests.Impl.Trace
{
    public class SpanDataTest
    {
        [Fact]
        public void SpanData_FromSpan()
        {
            var resource = new Resource(new[] { new KeyValuePair<string, object>("resourceKey", "resourceValue") });
            var tracer = TracerFactory.Create(b => b.SetResource(resource)).GetTracer(null);

            var tracestate = new KeyValuePair<string, string>[0];
            var parentContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.Recorded, true, tracestate);

            var attributes = new Dictionary<string, object> { ["key"] = "value", };
            var link = new Link(parentContext);
            var evnt = new Event("event");

            var startTime = DateTimeOffset.UtcNow.AddSeconds(-2);
            var endTime = DateTimeOffset.UtcNow.AddSeconds(-1);
            var span = tracer.StartSpan("name", parentContext, SpanKind.Producer,
                new SpanCreationOptions { Attributes = attributes, Links = new[] { link }, StartTimestamp = startTime, });

            span.AddEvent(evnt);
            span.Status = Status.FailedPrecondition;
            span.End(endTime);

            var spanData = new SpanData((SpanSdk)span);

            Assert.Equal("name", spanData.Name);
            Assert.Equal(SpanKind.Producer, spanData.Kind);
            Assert.Equal(parentContext.SpanId, spanData.ParentSpanId);
            Assert.Equal(startTime, spanData.StartTimestamp);
            Assert.Equal(endTime, spanData.EndTimestamp);
            Assert.Equal(Status.FailedPrecondition, spanData.Status);
            Assert.Same(resource, spanData.LibraryResource);

            Assert.Equal(parentContext.TraceId, spanData.Context.TraceId);
            Assert.Equal(span.Context.SpanId, spanData.Context.SpanId);
            Assert.Equal(ActivityTraceFlags.Recorded, spanData.Context.TraceOptions);
            Assert.False(spanData.Context.IsRemote);
            Assert.Empty(spanData.Context.Tracestate);

            Assert.Single(spanData.Attributes);
            Assert.Equal("key", spanData.Attributes.Single().Key);
            Assert.Equal("value", spanData.Attributes.Single().Value);

            Assert.Single(spanData.Events);
            Assert.Same(evnt, spanData.Events.Single());

            Assert.Single(spanData.Links);
            Assert.Equal(link, spanData.Links.Single());
        }

        [Fact]
        public void SpanData_FromSpan_Defaults()
        {
            var tracer = TracerFactory.Create(b => { }).GetTracer(null);

            var span = (SpanSdk)tracer.StartSpan("name");
            span.End();

            var spanData = new SpanData(span);

            Assert.Equal("name", spanData.Name);
            Assert.Equal(SpanKind.Internal, spanData.Kind);
            Assert.Equal(default, spanData.ParentSpanId);
            Assert.Equal(span.StartTimestamp, spanData.StartTimestamp);
            Assert.Equal(span.EndTimestamp, spanData.EndTimestamp);
            Assert.Equal(Status.Ok, spanData.Status);
            Assert.Same(Resource.Empty, spanData.LibraryResource);

            Assert.Equal(span.Context, spanData.Context);

            Assert.Empty(spanData.Attributes);
            Assert.Empty(spanData.Events);
            Assert.Empty(spanData.Links);
        }

        [Fact]
        public void SpanData_FromSpan_ReflectsSpanChanges()
        {
            var tracer = TracerFactory.Create(b => { }).GetTracer(null);

            var span = (SpanSdk)tracer.StartSpan("name");
            var spanData = new SpanData(span);
            Assert.Empty(spanData.Attributes);
            Assert.Empty(spanData.Events);
            Assert.Equal(default, spanData.EndTimestamp);

            span.AddEvent(new Event("event"));
            span.SetAttribute("key", "value");

            Assert.Single(spanData.Attributes);
            Assert.Single(spanData.Events);
            span.End();
            Assert.NotEqual(default, spanData.EndTimestamp);
            Assert.Equal(Status.Ok, spanData.Status);
        }

        [Fact]
        public void SpanData_FromParameters()
        {
            var resource = new Resource(new[] { new KeyValuePair<string, object>("resourceKey", "resourceValue") });

            var tracestate = new KeyValuePair<string, string>[0];
            var parentSpanId = ActivitySpanId.CreateRandom();
            var context = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.Recorded, true, tracestate);

            var attributes = new Dictionary<string, object> { ["key"] = "value", };
            var links = new[] { new Link(context) };
            var events = new[] { new Event("event") };

            var startTime = DateTimeOffset.UtcNow.AddSeconds(-2);
            var endTime = DateTimeOffset.UtcNow.AddSeconds(-1);

            var spanData = new SpanData("name", context, parentSpanId, SpanKind.Client, startTime, attributes, events, links, resource, Status.DataLoss, endTime);

            Assert.Equal("name", spanData.Name);
            Assert.Equal(SpanKind.Client, spanData.Kind);
            Assert.Equal(parentSpanId, spanData.ParentSpanId);
            Assert.Equal(startTime, spanData.StartTimestamp);
            Assert.Equal(endTime, spanData.EndTimestamp);
            Assert.Equal(Status.DataLoss, spanData.Status);
            Assert.Same(resource, spanData.LibraryResource);

            Assert.Equal(context, spanData.Context);

            Assert.Equal(attributes, spanData.Attributes);
            Assert.Equal(events, spanData.Events);
            Assert.Equal(links, spanData.Links);
        }

        [Fact]
        public void SpanData_FromParameters_Defaults()
        {
            var startTime = DateTimeOffset.UtcNow.AddSeconds(-2);
            var endTime = DateTimeOffset.UtcNow.AddSeconds(-1);
            var context = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

            var spanData = new SpanData("name", context, default, SpanKind.Client, startTime, null, null, null, null, Status.Cancelled, endTime);

            Assert.Equal("name", spanData.Name);
            Assert.Equal(SpanKind.Client, spanData.Kind);
            Assert.Equal(default, spanData.ParentSpanId);
            Assert.Equal(startTime, spanData.StartTimestamp);
            Assert.Equal(endTime, spanData.EndTimestamp);
            Assert.Equal(Status.Cancelled, spanData.Status);
            Assert.Same(Resource.Empty, spanData.LibraryResource);
            Assert.Equal(context, spanData.Context);
            Assert.Empty(spanData.Attributes);
            Assert.Empty(spanData.Events);
            Assert.Empty(spanData.Links);
        }

        [Fact]
        public void SpanData_FromSpan_Equal()
        {
            var tracer = TracerFactory.Create(b => { }).GetTracer(null);

            var span1 = (SpanSdk)tracer.StartSpan("name1");
            var span2 = (SpanSdk)tracer.StartSpan("name2");

            Assert.Equal(new SpanData(span1), new SpanData(span1));
            Assert.NotEqual(new SpanData(span1), new SpanData(span2));
        }

        [Fact]
        public void SpanData_FromParameters_NotEqual()
        {
            var resource = new Resource(new[] { new KeyValuePair<string, object>("resourceKey", "resourceValue") });
            var otherResource = new Resource(new[] { new KeyValuePair<string, object>("resourceKey1", "resourceValue1") });

            var tracestate = new KeyValuePair<string, string>[0];
            var parentSpanId = ActivitySpanId.CreateRandom();

            var context = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.Recorded, true, tracestate);
            var otherContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.Recorded, true, tracestate);

            var attributes = new Dictionary<string, object> { ["key"] = "value", };
            var otherAttributes = new Dictionary<string, object> { ["key1"] = "value1", };
            var links = new[] { new Link(context) };
            var otherLinks = new[] { new Link(otherContext) };
            var events = new[] { new Event("event") };
            var otherEvents = new[] { new Event("event1") };

            var startTime = DateTimeOffset.UtcNow.AddSeconds(-2);
            var otherStartTime = DateTimeOffset.UtcNow.AddSeconds(-3);
            var endTime = DateTimeOffset.UtcNow.AddSeconds(-1);
            var otherEndTime = DateTimeOffset.UtcNow;

            var spanData1 = new SpanData("name", context, parentSpanId, SpanKind.Client, startTime, attributes, events, links, resource, Status.DataLoss, endTime);
            var spanData2 = new SpanData("name1", context, parentSpanId, SpanKind.Client, startTime, attributes, events, links, resource, Status.DataLoss, endTime);
            var spanData3 = new SpanData("name", otherContext, parentSpanId, SpanKind.Client, startTime, attributes, events, links, resource, Status.DataLoss, endTime);
            var spanData4 = new SpanData("name", context, default, SpanKind.Client, startTime, attributes, events, links, resource, Status.DataLoss, endTime);
            var spanData5 = new SpanData("name", context, parentSpanId, SpanKind.Server, startTime, attributes, events, links, resource, Status.DataLoss, endTime);
            var spanData6 = new SpanData("name", context, parentSpanId, SpanKind.Client, otherStartTime, attributes, events, links, resource, Status.DataLoss, endTime);
            var spanData7 = new SpanData("name", context, parentSpanId, SpanKind.Client, startTime, otherAttributes, events, links, resource, Status.DataLoss, endTime);
            var spanData8 = new SpanData("name", context, parentSpanId, SpanKind.Client, startTime, attributes, otherEvents, links, resource, Status.DataLoss, endTime);
            var spanData9 = new SpanData("name", context, parentSpanId, SpanKind.Client, startTime, attributes, events, otherLinks, resource, Status.DataLoss, endTime);
            var spanData10 = new SpanData("name", context, parentSpanId, SpanKind.Client, startTime, attributes, events, links, otherResource, Status.DataLoss, endTime);
            var spanData11 = new SpanData("name", context, parentSpanId, SpanKind.Client, startTime, attributes, events, links, resource, Status.AlreadyExists, endTime);
            var spanData12 = new SpanData("name", context, parentSpanId, SpanKind.Client, startTime, attributes, events, links, resource, Status.DataLoss, otherEndTime);

            Assert.NotEqual(spanData1, spanData2);
            Assert.NotEqual(spanData1, spanData3);
            Assert.NotEqual(spanData1, spanData4);
            Assert.NotEqual(spanData1, spanData5);
            Assert.NotEqual(spanData1, spanData6);
            Assert.NotEqual(spanData1, spanData7);
            Assert.NotEqual(spanData1, spanData8);
            Assert.NotEqual(spanData1, spanData9);
            Assert.NotEqual(spanData1, spanData10);
            Assert.NotEqual(spanData1, spanData11);
            Assert.NotEqual(spanData1, spanData12);
        }
    }
}
