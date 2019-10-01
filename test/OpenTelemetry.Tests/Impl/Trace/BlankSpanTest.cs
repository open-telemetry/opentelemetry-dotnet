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

using System;
using System.Diagnostics;

namespace OpenTelemetry.Trace.Test
{
    using System.Collections.Generic;
    using Xunit;

    public class BlankSpanTest
    {
        [Fact]
        public void DoNotCrash()
        {
            IDictionary<string, object> attributes = new Dictionary<string, object>();
            attributes.Add(
                "MyStringAttributeKey", "MyStringAttributeValue");
            IDictionary<string, object> multipleAttributes = new Dictionary<string, object>();
            multipleAttributes.Add(
                "MyStringAttributeKey", "MyStringAttributeValue");
            multipleAttributes.Add("MyBooleanAttributeKey", true);
            multipleAttributes.Add("MyLongAttributeKey", 123);
            multipleAttributes.Add("MyDoubleAttributeKey", 0.005);
            // Tests only that all the methods are not crashing/throwing errors.
            BlankSpan.Instance.SetAttribute(
                "MyStringAttributeKey2", "MyStringAttributeValue2");
            foreach (var a in attributes)
            {
                BlankSpan.Instance.SetAttribute(a);
            }

            foreach (var a in multipleAttributes)
            {
                BlankSpan.Instance.SetAttribute(a);
            }

            BlankSpan.Instance.AddEvent("MyEvent");
            BlankSpan.Instance.AddEvent("MyEvent", attributes);
            BlankSpan.Instance.AddEvent("MyEvent", multipleAttributes);
            BlankSpan.Instance.AddEvent(new Event("MyEvent"));
            BlankSpan.Instance.AddLink(new Link(new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, Tracestate.Empty)));

            Assert.False(BlankSpan.Instance.Context.IsValid);
            Assert.False(BlankSpan.Instance.IsRecordingEvents);
            Assert.Equal(Status.Ok, BlankSpan.Instance.Status);
            BlankSpan.Instance.Status = Status.Ok;
            BlankSpan.Instance.End();
        }

        [Fact]
        public void BadArguments()
        {
            Assert.Throws<ArgumentException>(() => BlankSpan.Instance.Status = new Status());
            Assert.Throws<ArgumentNullException>(() => BlankSpan.Instance.UpdateName(null));
            Assert.Throws<ArgumentNullException>(() => BlankSpan.Instance.SetAttribute(null, string.Empty));
            Assert.Throws<ArgumentNullException>(() => BlankSpan.Instance.SetAttribute(string.Empty, null));
            Assert.Throws<ArgumentNullException>(() => BlankSpan.Instance.SetAttribute(null, "foo"));
            Assert.Throws<ArgumentNullException>(() => BlankSpan.Instance.SetAttribute(null, 1L));
            Assert.Throws<ArgumentNullException>(() => BlankSpan.Instance.SetAttribute(null, 0.1d));
            Assert.Throws<ArgumentNullException>(() => BlankSpan.Instance.SetAttribute(null, true));
            Assert.Throws<ArgumentNullException>(() => BlankSpan.Instance.AddEvent((string)null));
            Assert.Throws<ArgumentNullException>(() => BlankSpan.Instance.AddEvent((Event)null));
            Assert.Throws<ArgumentNullException>(() => BlankSpan.Instance.AddLink(null));
            Assert.Throws<ArgumentException>(() => BlankSpan.Instance.AddLink(new Link(SpanContext.Blank)));
        }
    }
}
