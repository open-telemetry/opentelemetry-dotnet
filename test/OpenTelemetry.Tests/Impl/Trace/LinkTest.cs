// <copyright file="LinkTest.cs" company="OpenTelemetry Authors">
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

using System.Linq;

namespace OpenTelemetry.Trace.Test
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using OpenTelemetry.Utils;
    using Xunit;

    public class LinkTest
    {
        private readonly IDictionary<string, object> attributesMap = new Dictionary<string, object>();
        private readonly SpanContext spanContext;
          

        public LinkTest()
        {
            // TODO: remove with next DiagnosticSource preview, switch to Activity setidformat
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            spanContext = SpanContext.Create(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, Tracestate.Empty); ;
            attributesMap.Add("MyAttributeKey0", "MyStringAttribute");
            attributesMap.Add("MyAttributeKey1", 10L);
            attributesMap.Add("MyAttributeKey2", true);
            attributesMap.Add("MyAttributeKey3", 0.005);
        }

        [Fact]
        public void FromSpanContext_ChildLink()
        {
            var link = Link.FromSpanContext(spanContext);
            Assert.Equal(spanContext.TraceId, link.Context.TraceId);
            Assert.Equal(spanContext.SpanId, link.Context.SpanId);
        }

        [Fact]
        public void FromSpanContext_ChildLink_WithAttributes()
        {
            var link = Link.FromSpanContext(spanContext, attributesMap);
            Assert.Equal(spanContext.TraceId, link.Context.TraceId);
            Assert.Equal(spanContext.SpanId, link.Context.SpanId);
            Assert.Equal(attributesMap, link.Attributes);
        }

        [Fact]
        public void FromSpanContext_ParentLink()
        {
            var link = Link.FromSpanContext(spanContext);
            Assert.Equal(spanContext.TraceId, link.Context.TraceId);
            Assert.Equal(spanContext.SpanId, link.Context.SpanId);
        }

        [Fact]
        public void FromSpanContext_ParentLink_WithAttributes()
        {
            var link = Link.FromSpanContext(spanContext, attributesMap);
            Assert.Equal(spanContext.TraceId, link.Context.TraceId);
            Assert.Equal(spanContext.SpanId, link.Context.SpanId);
            Assert.Equal(attributesMap, link.Attributes);
        }

        [Fact]
        public void Link_ToString()
        {
            var link = Link.FromSpanContext(spanContext, attributesMap);
            Assert.Contains(spanContext.TraceId.ToString(), link.ToString());
            Assert.Contains(spanContext.SpanId.ToString(), link.ToString());
            Assert.Contains(string.Join(" ", attributesMap.Select(kvp => $"{kvp.Key}={kvp.Value}")), link.ToString());
            link = Link.FromSpanContext(spanContext, attributesMap);
            Assert.Contains(spanContext.TraceId.ToString(), link.ToString());
            Assert.Contains(spanContext.SpanId.ToString(), spanContext.SpanId.ToString());
            Assert.Contains(string.Join(" ", attributesMap.Select(kvp => $"{kvp.Key}={kvp.Value}")), link.ToString());
        }

        [Fact]
        public void FromSpanContext_FromActivity()
        {
            var activity = new Activity("foo").Start();
            activity.TraceStateString = "k1=v1, k2=v2";

            var link = Link.FromActivity(activity);
            Assert.Equal(activity.TraceId, link.Context.TraceId);
            Assert.Equal(activity.SpanId, link.Context.SpanId);

            var entries = link.Context.Tracestate.Entries.ToArray();
            Assert.Equal(2, entries.Length);
            Assert.Equal("k1", entries[0].Key);
            Assert.Equal("v1", entries[0].Value);
            Assert.Equal("k2", entries[1].Key);
            Assert.Equal("v2", entries[1].Value);
        }
    }
}
