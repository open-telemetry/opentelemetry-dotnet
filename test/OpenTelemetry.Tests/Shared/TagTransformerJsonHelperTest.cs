// <copyright file="TagTransformerJsonHelperTest.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Internal;
using Xunit;

namespace OpenTelemetry.Tests.Shared
{
    public class TagTransformerJsonHelperTest
    {
        [Theory]
        [InlineData(new object[] { new char[] { } })]
        [InlineData(new object[] { new char[] { 'a' } })]
        [InlineData(new object[] { new char[] { '1', '2', '3' } })]
        public void CharArray(char[] data)
        {
            VerifySerialization(data);
        }

        [Theory]
        [InlineData(new object[] { new string[] { } })]
        [InlineData(new object[] { new string[] { "one" } })]
        [InlineData(new object[] { new string[] { "" } })]
        [InlineData(new object[] { new string[] { "a", "b", "c", "d" } })]
        [InlineData(new object[] { new string[] { "\r\n", "\t", "\"" } })]
        public void StringArray(string[] data)
        {
            VerifySerialization(data);
        }

        [Theory]
        [InlineData(new object[] { new bool[] { } })]
        [InlineData(new object[] { new bool[] { true } })]
        [InlineData(new object[] { new bool[] { true, false, false, true } })]
        public void BooleanArray(bool[] data)
        {
            VerifySerialization(data);
        }

        [Theory]
        [InlineData(new object[] { new byte[] { } })]
        [InlineData(new object[] { new byte[] { 0 } })]
        [InlineData(new object[] { new byte[] { 255, 1, 4, 13 } })]
        public void ByteArray(byte[] data)
        {
            VerifySerialization(data);
        }

        private static void VerifySerialization(Array data)
        {
            var reflectionBasedResult = System.Text.Json.JsonSerializer.Serialize(data);
            var rawResult = TagTransformerJsonHelper.JsonSerializeArrayTag(data);

            Assert.Equal(reflectionBasedResult, rawResult);
        }
    }
}
