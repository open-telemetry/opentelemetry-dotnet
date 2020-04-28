// <copyright file="NoOpSpanTest.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Trace.Test
{
    public class NoOpSpanTest
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
            var noOpSpan = new NoOpSpan();
            noOpSpan.SetAttribute(
                "MyStringAttributeKey2", "MyStringAttributeValue2");
            foreach (var a in attributes)
            {
                noOpSpan.SetAttribute(a.Key, a.Value);
            }

            foreach (var a in multipleAttributes)
            {
                noOpSpan.SetAttribute(a.Key, a.Value);
            }

            noOpSpan.AddEvent("MyEvent");
            noOpSpan.AddEvent(new Event("MyEvent", attributes));
            noOpSpan.AddEvent(new Event("MyEvent", multipleAttributes));
            noOpSpan.AddEvent(new Event("MyEvent"));

            Assert.False(noOpSpan.Context.IsValid);
            Assert.False(noOpSpan.IsRecording);

            noOpSpan.Status = Status.Ok;
            noOpSpan.End();
        }

        [Fact]
        public void BadArguments_DoesNotThrow()
        {
            var noOpSpan = new NoOpSpan();
            noOpSpan.Status = new Status();
            noOpSpan.UpdateName(null);
            noOpSpan.SetAttribute(null, string.Empty);
            noOpSpan.SetAttribute(string.Empty, null);
            noOpSpan.SetAttribute(null, "foo");
            noOpSpan.SetAttribute(null, 1L);
            noOpSpan.SetAttribute(null, 0.1d);
            noOpSpan.SetAttribute(null, true);
            noOpSpan.AddEvent((string)null);
            noOpSpan.AddEvent((Event)null);
        }
    }
}
