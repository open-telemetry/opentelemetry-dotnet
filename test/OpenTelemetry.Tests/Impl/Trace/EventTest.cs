// <copyright file="EventTest.cs" company="OpenTelemetry Authors">
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
    using OpenTelemetry.Utils;
    using Xunit;

    public class EventTest
    {
        [Fact]
        public void FromDescription_NullDescription()
        {
            Assert.Throws<ArgumentNullException>(() => Event.Create(null));
        }

        [Fact]
        public void FromDescription()
        {
            var @event = Event.Create("MyEventText");
            Assert.Equal("MyEventText", @event.Name);
            Assert.Equal(0, @event.Attributes.Count);
        }

        [Fact]
        public void FromDescriptionAndAttributes_NullDescription()
        {
            Assert.Throws<ArgumentNullException>(() => Event.Create(null, new Dictionary<string, object>()));
        }

        [Fact]
        public void FromDescriptionAndAttributes_NullAttributes()
        {
            Assert.Throws<ArgumentNullException>(() => Event.Create("", null));
        }

        [Fact]
        public void FromDescriptionAndAttributes()
        {
            var attributes = new Dictionary<string, object>();
            attributes.Add(
                "MyStringAttributeKey", "MyStringAttributeValue");
            var @event = Event.Create("MyEventText", attributes);
            Assert.Equal("MyEventText", @event.Name);
            Assert.Equal(attributes, @event.Attributes);
        }

        [Fact]
        public void FromDescriptionAndAttributes_EmptyAttributes()
        {
            var @event =
                Event.Create(
                    "MyEventText", new Dictionary<string, object>());
            Assert.Equal("MyEventText", @event.Name);
            Assert.Equal(0, @event.Attributes.Count);
        }

        [Fact]
        public void Event_EqualsAndHashCode()
        {
            // EqualsTester tester = new EqualsTester();
            // Map<String, AttributeValue> attributes = new HashMap<String, AttributeValue>();
            // attributes.put(
            //    "MyStringAttributeKey", AttributeValue.stringAttributeValue("MyStringAttributeValue"));
            // tester
            //    .addEqualityGroup(
            //        Event.Create("MyEventText"),
            //        Event.Create(
            //            "MyEventText", Collections.< String, AttributeValue > emptyMap()))
            //    .addEqualityGroup(
            //        Event.Create("MyEventText", attributes),
            //        Event.Create("MyEventText", attributes))
            //    .addEqualityGroup(Event.Create("MyEventText2"));
            // tester.testEquals();
        }

        [Fact]
        public void Event_ToString()
        {
            var @event = Event.Create("MyEventText");
            Assert.Contains("MyEventText", @event.ToString());
            var attributes = new Dictionary<string, object>();
            attributes.Add(
                "MyStringAttributeKey", "MyStringAttributeValue");
            @event = Event.Create("MyEventText2", attributes);
            Assert.Contains("MyEventText2", @event.ToString());
            Assert.Contains(string.Join(",", attributes.Select(kvp => $"{kvp.Key}={kvp.Value}")), @event.ToString());
        }
    }
}
