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
using System;
using System.Collections.Generic;
using OpenTelemetry.Utils;
using Xunit;

namespace OpenTelemetry.Trace.Test
{
    public class EventTest
    {
        [Fact]
        public void FromDescription_NullDescription()
        {
            Assert.Throws<ArgumentNullException>(() => new Event(null));
        }

        [Fact]
        public void FromDescription()
        {
            var approxTimestamp = PreciseTimestamp.GetUtcNow();
            var @event = new Event("MyEventText");
            Assert.Equal("MyEventText", @event.Name);
            Assert.Equal(0, @event.Attributes.Count);
            Assert.InRange(Math.Abs((approxTimestamp - @event.Timestamp).TotalMilliseconds), double.Epsilon, 20);
        }

        [Fact]
        public void FromDescriptionAndDefaultTimestamp()
        {
            var approxTimestamp = PreciseTimestamp.GetUtcNow();
            var @event = new Event("MyEventText", default);
            Assert.Equal("MyEventText", @event.Name);
            Assert.Equal(0, @event.Attributes.Count);
            Assert.InRange(Math.Abs((approxTimestamp - @event.Timestamp).TotalMilliseconds), double.Epsilon, 20);
        }

        [Fact]
        public void FromDescriptionAndTimestamp()
        {
            var exactTimestamp = DateTime.UtcNow.AddSeconds(-100);
            var @event = new Event("MyEventText", exactTimestamp);
            Assert.Equal("MyEventText", @event.Name);
            Assert.Equal(0, @event.Attributes.Count);
            Assert.Equal(exactTimestamp, @event.Timestamp);
        }

        [Fact]
        public void FromDescriptionAndAttributes_NullDescription()
        {
            Assert.Throws<ArgumentNullException>(() => new Event(null, DateTime.UtcNow, new Dictionary<string, object>()));
        }

        [Fact]
        public void FromDescriptionAndAttributes_NullAttributes()
        {
            Assert.Throws<ArgumentNullException>(() => new Event("", DateTime.UtcNow, null));
        }

        [Fact]
        public void FromDescriptionTimestampAndAttributes()
        {
            var timestamp = DateTime.UtcNow;
            var attributes = new Dictionary<string, object>();
            attributes.Add(
                "MyStringAttributeKey", "MyStringAttributeValue");
            var @event = new Event("MyEventText", timestamp, attributes);
            Assert.Equal("MyEventText", @event.Name);
            Assert.Equal(attributes, @event.Attributes);
            Assert.Equal(timestamp, @event.Timestamp);
        }

        [Fact]
        public void FromDescriptionDefaultTimestampAndAttributes()
        {
            var approxTimestamp = PreciseTimestamp.GetUtcNow();
            var attributes = new Dictionary<string, object>();
            attributes.Add(
                "MyStringAttributeKey", "MyStringAttributeValue");
            var @event = new Event("MyEventText", default, attributes);
            Assert.Equal("MyEventText", @event.Name);
            Assert.Equal(attributes, @event.Attributes);
            Assert.InRange(Math.Abs((approxTimestamp - @event.Timestamp).TotalMilliseconds), double.Epsilon, 20);
        }

        [Fact]
        public void FromDescriptionAndAttributes_EmptyAttributes()
        {
            var @event =
                new Event(
                    "MyEventText", DateTime.UtcNow, new Dictionary<string, object>());
            Assert.Equal("MyEventText", @event.Name);
            Assert.Equal(0, @event.Attributes.Count);
        }
    }
}
