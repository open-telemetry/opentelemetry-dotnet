// <copyright file="CorrelationContextEntryTest.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Context.Tests
{
    public class CorrelationContextEntryTest
    {
        [Fact]
        public void TestGetKey()
        {
            Assert.Equal("k", new CorrelationContextEntry("k", "v").Key);
        }

        [Fact]
        public void TestTagEquals()
        {
            var tag1 = new CorrelationContextEntry("Key", "foo");
            var tag2 = new CorrelationContextEntry("Key", "foo");
            var tag3 = new CorrelationContextEntry("Key", "bar");
            var tag4 = new CorrelationContextEntry("Key2", "foo");
            Assert.Equal(tag1, tag2);
            Assert.NotEqual(tag1, tag3);
            Assert.NotEqual(tag1, tag4);
            Assert.NotEqual(tag2, tag3);
            Assert.NotEqual(tag2, tag4);
            Assert.NotEqual(tag3, tag4);
        }

        [Fact]
        public void TestNullKeyNullValue()
        {
            var entry = new CorrelationContextEntry(null, null);
            Assert.Empty(entry.Key);
            Assert.Empty(entry.Value);
        }

        [Fact]
        public void TestNullKey()
        {
            var entry = new CorrelationContextEntry(null, "foo");
            Assert.Empty(entry.Key);
            Assert.Equal("foo", entry.Value);
        }

        [Fact]
        public void TestNullValue()
        {
            var entry = new CorrelationContextEntry("foo", null);
            Assert.Equal("foo", entry.Key);
            Assert.Empty(entry.Value);
        }

        [Fact]
        public void TestEquality()
        {
            var entry1 = new CorrelationContextEntry("key", "value1");
            var entry2 = new CorrelationContextEntry("key", "value1");
            object entry3 = entry2;
            var entry4 = new CorrelationContextEntry("key", "value2");

            Assert.True(entry1 == entry2);
            Assert.True(entry1.Equals(entry2));
            Assert.True(entry1.Equals(entry3));

            Assert.True(entry1 != entry4);
        }

        [Fact]
        public void TestToString()
        {
            var entry1 = new CorrelationContextEntry("key1", "value1");
            Assert.Equal("CorrelationContextEntry{Key=key1, Value=value1}", entry1.ToString());

            var entry2 = new CorrelationContextEntry(null, "value1");
            Assert.Equal("CorrelationContextEntry{Key=, Value=value1}", entry2.ToString());
        }

        [Fact]
        public void TestGetHashCode()
        {
            var entry1 = new CorrelationContextEntry("key1", "value1");
            Assert.NotEqual(0, entry1.GetHashCode());

            var entry2 = new CorrelationContextEntry(null, "value1");
            Assert.NotEqual(0, entry2.GetHashCode());
        }
    }
}
