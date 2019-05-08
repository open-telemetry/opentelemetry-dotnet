// <copyright file="BlankSpanTest.cs" company="OpenCensus Authors">
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
    using Xunit;

    public class BlankSpanTest
    {
        [Fact]
        public void HasInvalidContextAndDefaultSpanOptions()
        {
            Assert.Equal(SpanContext.Invalid, BlankSpan.Instance.Context);
            Assert.True(BlankSpan.Instance.Options.HasFlag(SpanOptions.None));
        }

        [Fact]
        public void DoNotCrash()
        {
            IDictionary<string, IAttributeValue> attributes = new Dictionary<string, IAttributeValue>();
            attributes.Add(
                "MyStringAttributeKey", AttributeValue<string>.Create("MyStringAttributeValue"));
            IDictionary<string, IAttributeValue> multipleAttributes = new Dictionary<string, IAttributeValue>();
            multipleAttributes.Add(
                "MyStringAttributeKey", AttributeValue<string>.Create("MyStringAttributeValue"));
            multipleAttributes.Add("MyBooleanAttributeKey", AttributeValue<bool>.Create(true));
            multipleAttributes.Add("MyLongAttributeKey", AttributeValue<long>.Create(123));
            multipleAttributes.Add("MyDoubleAttributeKey", AttributeValue<double>.Create(0.005));
            // Tests only that all the methods are not crashing/throwing errors.
            BlankSpan.Instance.PutAttribute(
                "MyStringAttributeKey2", AttributeValue<string>.Create("MyStringAttributeValue2"));
            BlankSpan.Instance.PutAttributes(attributes);
            BlankSpan.Instance.PutAttributes(multipleAttributes);
            BlankSpan.Instance.AddAnnotation("MyAnnotation");
            BlankSpan.Instance.AddAnnotation("MyAnnotation", attributes);
            BlankSpan.Instance.AddAnnotation("MyAnnotation", multipleAttributes);
            BlankSpan.Instance.AddAnnotation(Annotation.FromDescription("MyAnnotation"));
            // BlankSpan.Instance.addNetworkEvent(NetworkEvent.builder(NetworkEvent.Type.SENT, 1L).build());
            BlankSpan.Instance.AddMessageEvent(MessageEvent.Builder(MessageEventType.Sent, 1L).Build());
            BlankSpan.Instance.AddLink(
                Link.FromSpanContext(SpanContext.Invalid, LinkType.ChildLinkedSpan));
            BlankSpan.Instance.Status = Status.Ok;
            BlankSpan.Instance.End(EndSpanOptions.Default);
            BlankSpan.Instance.End();
        }
    }
}
