// <copyright file="LinkTest.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace.Test
{
    using System.Collections.Generic;
    using OpenCensus.Trace.Internal;
    using OpenCensus.Utils;
    using Xunit;

    public class LinkTest
    {
        private readonly IDictionary<string, IAttributeValue> attributesMap = new Dictionary<string, IAttributeValue>();
        private readonly IRandomGenerator random = new RandomGenerator(1234);
        private readonly ISpanContext spanContext;
          

        public LinkTest()
        {
            spanContext = SpanContext.Create(TraceId.GenerateRandomId(random), SpanId.GenerateRandomId(random), TraceOptions.Default, Tracestate.Empty); ;
            attributesMap.Add("MyAttributeKey0", AttributeValue<string>.Create("MyStringAttribute"));
            attributesMap.Add("MyAttributeKey1", AttributeValue<long>.Create(10));
            attributesMap.Add("MyAttributeKey2", AttributeValue<bool>.Create(true));
            attributesMap.Add("MyAttributeKey3", AttributeValue<double>.Create(0.005));
        }

        [Fact]
        public void FromSpanContext_ChildLink()
        {
            ILink link = Link.FromSpanContext(spanContext, LinkType.ChildLinkedSpan);
            Assert.Equal(spanContext.TraceId, link.TraceId);
            Assert.Equal(spanContext.SpanId, link.SpanId);
            Assert.Equal(LinkType.ChildLinkedSpan, link.Type);
        }

        [Fact]
        public void FromSpanContext_ChildLink_WithAttributes()
        {
            ILink link = Link.FromSpanContext(spanContext, LinkType.ChildLinkedSpan, attributesMap);
            Assert.Equal(spanContext.TraceId, link.TraceId);
            Assert.Equal(spanContext.SpanId, link.SpanId);
            Assert.Equal(LinkType.ChildLinkedSpan, link.Type);
            Assert.Equal(attributesMap, link.Attributes);
        }

        [Fact]
        public void FromSpanContext_ParentLink()
        {
            ILink link = Link.FromSpanContext(spanContext, LinkType.ParentLinkedSpan);
            Assert.Equal(spanContext.TraceId, link.TraceId);
            Assert.Equal(spanContext.SpanId, link.SpanId);
            Assert.Equal(LinkType.ParentLinkedSpan, link.Type);
        }

        [Fact]
        public void FromSpanContext_ParentLink_WithAttributes()
        {
            ILink link = Link.FromSpanContext(spanContext, LinkType.ParentLinkedSpan, attributesMap);
            Assert.Equal(spanContext.TraceId, link.TraceId);
            Assert.Equal(spanContext.SpanId, link.SpanId);
            Assert.Equal(LinkType.ParentLinkedSpan, link.Type);
            Assert.Equal(attributesMap, link.Attributes);
        }

        [Fact]
        public void Link_EqualsAndHashCode()
        {
            // EqualsTester tester = new EqualsTester();
            // tester
            //    .addEqualityGroup(
            //        Link.fromSpanContext(spanContext, Type.PARENT_LINKED_SPAN),
            //        Link.fromSpanContext(spanContext, Type.PARENT_LINKED_SPAN))
            //    .addEqualityGroup(
            //        Link.fromSpanContext(spanContext, Type.CHILD_LINKED_SPAN),
            //        Link.fromSpanContext(spanContext, Type.CHILD_LINKED_SPAN))
            //    .addEqualityGroup(Link.fromSpanContext(SpanContext.INVALID, Type.CHILD_LINKED_SPAN))
            //    .addEqualityGroup(Link.fromSpanContext(SpanContext.INVALID, Type.PARENT_LINKED_SPAN))
            //    .addEqualityGroup(
            //        Link.fromSpanContext(spanContext, Type.PARENT_LINKED_SPAN, attributesMap),
            //        Link.fromSpanContext(spanContext, Type.PARENT_LINKED_SPAN, attributesMap));
            // tester.testEquals();


        }

        [Fact]
        public void Link_ToString()
        {
            ILink link = Link.FromSpanContext(spanContext, LinkType.ChildLinkedSpan, attributesMap);
            Assert.Contains(spanContext.TraceId.ToString(), link.ToString());
            Assert.Contains(spanContext.SpanId.ToString(), link.ToString());
            Assert.Contains("ChildLinkedSpan", link.ToString());
            Assert.Contains(Collections.ToString(attributesMap), link.ToString());
            link = Link.FromSpanContext(spanContext, LinkType.ParentLinkedSpan, attributesMap);
            Assert.Contains(spanContext.TraceId.ToString(), link.ToString());
            Assert.Contains(spanContext.SpanId.ToString(), spanContext.SpanId.ToString());
            Assert.Contains("ParentLinkedSpan", link.ToString());
            Assert.Contains(Collections.ToString(attributesMap), link.ToString());
        }
    }
}
