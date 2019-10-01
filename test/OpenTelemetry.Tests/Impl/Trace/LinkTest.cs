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

namespace OpenTelemetry.Trace.Test
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Xunit;

    public class LinkTest : IDisposable
    {
        private readonly IDictionary<string, object> attributesMap = new Dictionary<string, object>();
        private readonly SpanContext spanContext;
          

        public LinkTest()
        {
            spanContext = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, Tracestate.Empty); ;

            attributesMap.Add("MyAttributeKey0", "MyStringAttribute");
            attributesMap.Add("MyAttributeKey1", 10L);
            attributesMap.Add("MyAttributeKey2", true);
            attributesMap.Add("MyAttributeKey3", 0.005);
        }

        [Fact]
        public void FromSpanContext()
        {
            var link = new Link(spanContext);
            Assert.Equal(spanContext.TraceId, link.Context.TraceId);
            Assert.Equal(spanContext.SpanId, link.Context.SpanId);
        }

        [Fact]
        public void FromSpanContext_WithAttributes()
        {
            var link = new Link(spanContext, attributesMap);
            Assert.Equal(spanContext.TraceId, link.Context.TraceId);
            Assert.Equal(spanContext.SpanId, link.Context.SpanId);
            Assert.Equal(attributesMap, link.Attributes);
        }

        public void Dispose()
        {
            Activity.Current = null;
        }
    }
}
