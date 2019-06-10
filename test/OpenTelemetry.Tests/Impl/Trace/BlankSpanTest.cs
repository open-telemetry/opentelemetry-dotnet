// <copyright file="BlankSpanTest.cs" company="OpenTelemetry Authors">
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
    using System.Collections.Generic;
    using OpenTelemetry.Trace.Internal;
    using Xunit;

    public class BlankSpanTest
    {
        [Fact]
        public void HasInvalidContextAndDefaultSpanOptions()
        {
            Assert.Equal(SpanContext.Blank, BlankSpan.Instance.Context);
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
            BlankSpan.Instance.SetAttribute(
                "MyStringAttributeKey2", AttributeValue<string>.Create("MyStringAttributeValue2"));
            foreach (var a in attributes)
            {
                BlankSpan.Instance.SetAttribute(a.Key, a.Value);
            }

            foreach (var a in multipleAttributes)
            {
                BlankSpan.Instance.SetAttribute(a.Key, a.Value);
            }

            BlankSpan.Instance.AddEvent("MyEvent");
            BlankSpan.Instance.AddEvent("MyEvent", attributes);
            BlankSpan.Instance.AddEvent("MyEvent", multipleAttributes);
            BlankSpan.Instance.AddEvent(Event.Create("MyEvent"));
            BlankSpan.Instance.AddLink(
                Link.FromSpanContext(SpanContext.Blank));
            BlankSpan.Instance.Status = Status.Ok;
            BlankSpan.Instance.End(EndSpanOptions.Default);
            BlankSpan.Instance.End();
        }
    }
}
