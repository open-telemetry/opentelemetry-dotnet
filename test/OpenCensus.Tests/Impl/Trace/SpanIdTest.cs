// <copyright file="SpanIdTest.cs" company="OpenCensus Authors">
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
    using Xunit;

    public class SpanIdTest
    {
        private static readonly byte[] firstBytes = new byte[] { 0, 0, 0, 0, 0, 0, 0, (byte)'a' };
        private static readonly byte[] secondBytes = new byte[] { 0xFF, 0, 0, 0, 0, 0, 0, (byte)'A' };
        private static readonly ISpanId first = SpanId.FromBytes(firstBytes);
        private static readonly ISpanId second = SpanId.FromBytes(secondBytes);

        [Fact]
        public void invalidSpanId()
        {
            Assert.Equal(new byte[8], SpanId.Invalid.Bytes);
        }

        [Fact]
        public void IsValid()
        {
            Assert.False(SpanId.Invalid.IsValid);
            Assert.True(first.IsValid);
            Assert.True(second.IsValid);
        }

        [Fact]
        public void FromLowerBase16()
        {
            Assert.Equal(SpanId.Invalid, SpanId.FromLowerBase16("0000000000000000"));
            Assert.Equal(first, SpanId.FromLowerBase16("0000000000000061"));
            Assert.Equal(second, SpanId.FromLowerBase16("ff00000000000041"));
        }

        [Fact]
        public void ToLowerBase16()
        {
            Assert.Equal("0000000000000000", SpanId.Invalid.ToLowerBase16());
            Assert.Equal("0000000000000061", first.ToLowerBase16());
            Assert.Equal("ff00000000000041", second.ToLowerBase16());
        }

        [Fact]
        public void Bytes()
        {
            Assert.Equal(firstBytes, first.Bytes);
            Assert.Equal(secondBytes, second.Bytes);

        }

        [Fact]
        public void SpanId_CompareTo()
        {
            Assert.Equal(1, first.CompareTo(second));
            Assert.Equal(-1, second.CompareTo(first));
            Assert.Equal(0, first.CompareTo(SpanId.FromBytes(firstBytes)));
        }

        [Fact]
        public void SpanId_EqualsAndHashCode()
        {
            // EqualsTester tester = new EqualsTester();
            // tester.addEqualityGroup(SpanId.INVALID, SpanId.INVALID);
            // tester.addEqualityGroup(first, SpanId.fromBytes(Arrays.copyOf(firstBytes, firstBytes.length)));
            // tester.addEqualityGroup(
            //    second, SpanId.fromBytes(Arrays.copyOf(secondBytes, secondBytes.length)));
            // tester.testEquals();
        }

        [Fact]
        public void SpanId_ToString()
        {
            Assert.Contains("0000000000000000", SpanId.Invalid.ToString());
            Assert.Contains("0000000000000061", first.ToString());
            Assert.Contains("ff00000000000041", second.ToString());
        }
    }
}
