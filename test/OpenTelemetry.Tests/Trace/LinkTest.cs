﻿// <copyright file="LinkTest.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class LinkTest : IDisposable
    {
        private readonly IDictionary<string, object> attributesMap = new Dictionary<string, object>();
        private readonly SpanContext spanContext;
        private readonly ActivityTagsCollection tags;

        public LinkTest()
        {
            this.spanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);

            this.attributesMap.Add("MyAttributeKey0", "MyStringAttribute");
            this.attributesMap.Add("MyAttributeKey1", 10L);
            this.attributesMap.Add("MyAttributeKey2", true);
            this.attributesMap.Add("MyAttributeKey3", 0.005);
            this.tags = new ActivityTagsCollection(this.attributesMap);
        }

        [Fact]
        public void FromSpanContext()
        {
            var link = new Link(this.spanContext);
            Assert.Equal(this.spanContext.TraceId, link.Context.TraceId);
            Assert.Equal(this.spanContext.SpanId, link.Context.SpanId);
        }

        [Fact]
        public void FromSpanContext_WithAttributes()
        {
            var link = new Link(this.spanContext, this.tags);
            Assert.Equal(this.spanContext.TraceId, link.Context.TraceId);
            Assert.Equal(this.spanContext.SpanId, link.Context.SpanId);
            Assert.Equal(this.attributesMap, link.Attributes);
        }

        [Fact]
        public void Equality()
        {
            var link1 = new Link(this.spanContext);
            var link2 = new Link(this.spanContext);

            Assert.Equal(link1, link2);
            Assert.True(link1 == link2);
        }

        [Fact]
        public void Equality_WithAttributes()
        {
            var link1 = new Link(this.spanContext, this.tags);
            var link2 = new Link(this.spanContext, this.tags);

            Assert.Equal(link1, link2);
            Assert.True(link1 == link2);
        }

        [Fact]
        public void NotEquality()
        {
            var link1 = new Link(new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None));
            var link2 = new Link(new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None));

            Assert.NotEqual(link1, link2);
            Assert.True(link1 != link2);
        }

        [Fact]
        public void NotEquality_WithAttributes()
        {
            var tag1 = new ActivityTagsCollection(new Dictionary<string, object>());
            var tag2 = this.tags;
            var link1 = new Link(this.spanContext, tag1);
            var link2 = new Link(this.spanContext, tag2);

            Assert.NotEqual(link1, link2);
            Assert.True(link1 != link2);
        }

        public void Dispose()
        {
            Activity.Current = null;
        }
    }
}
