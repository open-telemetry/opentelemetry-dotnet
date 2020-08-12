// <copyright file="DistributedContextDeserializationTest.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenTelemetry.Internal;
using Xunit;

namespace OpenTelemetry.Context.Propagation.Tests
{
    public class DistributedContextDeserializationTest
    {
        private readonly DistributedContextBinarySerializer serializer;

        public DistributedContextDeserializationTest()
        {
            DistributedContext.Carrier = AsyncLocalDistributedContextCarrier.Instance;
            this.serializer = new DistributedContextBinarySerializer();
        }

        [Fact]
        public void TestConstants()
        {
            // Refer to the JavaDoc on SerializationUtils for the definitions on these constants.
            Assert.Equal(0, SerializationUtils.VersionId);
            Assert.Equal(0, SerializationUtils.TagFieldId);
            Assert.Equal(8192, SerializationUtils.TagContextSerializedSizeLimit);
        }

        [Fact]
        public void TestNoTagsSerialization()
        {
            var dc = this.serializer.FromByteArray(this.serializer.ToByteArray(DistributedContext.Empty));
            Assert.Empty(dc.CorrelationContext.Entries);

            dc = this.serializer.FromByteArray(new byte[] { SerializationUtils.VersionId }); // One byte that represents Version ID.
            Assert.Empty(dc.CorrelationContext.Entries);
        }

        [Fact]
        public void TestDeserializeEmptyByteArrayThrowException()
        {
            Assert.Equal(DistributedContext.Empty, this.serializer.FromByteArray(new byte[0]));
        }

        [Fact]
        public void TestDeserializeTooLargeByteArrayThrowException()
        {
            var output = new MemoryStream();
            output.WriteByte(SerializationUtils.VersionId);

            for (var i = 0; i < (SerializationUtils.TagContextSerializedSizeLimit / 8) - 1; i++)
            {
                // Each tag will be with format {key : "0123", value : "0123"}, so the length of it is 8.
                string str = i.ToString("0000");
                EncodeTagToOutPut(str, str, output);
            }

            // The last tag will be of size 9, so the total size of the TagContext (8193) will be one byte
            // more than limit.
            EncodeTagToOutPut("last", "last1", output);

            var bytes = output.ToArray();
            Assert.Equal(DistributedContext.Empty, this.serializer.FromByteArray(bytes));
        }

        // Deserializing this inPut should cause an error, even though it represents a relatively small
        // TagContext.
        [Fact]
        public void TestDeserializeTooLargeByteArrayThrowException_WithDuplicateTagKeys()
        {
            var output = new MemoryStream();
            output.WriteByte(SerializationUtils.VersionId);

            for (var i = 0; i < (SerializationUtils.TagContextSerializedSizeLimit / 8) - 1; i++)
            {
                // Each tag will be with format {key : "key_", value : "0123"}, so the length of it is 8.
                EncodeTagToOutPut("key_", i.ToString("0000"), output);
            }

            // The last tag will be of size 9, so the total size of the TagContext (8193) will be one byte
            // more than limit.
            EncodeTagToOutPut("key_", "last1", output);

            var bytes = output.ToArray();
            Assert.Equal(DistributedContext.Empty, this.serializer.FromByteArray(bytes));
        }

        [Fact]
        public void TestDeserializeOneTag()
        {
            var output = new MemoryStream();
            output.WriteByte(SerializationUtils.VersionId);
            EncodeTagToOutPut("Key", "Value", output);

            var expected = DistributedContextBuilder.CreateContext("Key", "Value");
            Assert.Equal(expected, this.serializer.FromByteArray(output.ToArray()));
        }

        [Fact]
        public void TestDeserializeMultipleTags()
        {
            var output = new MemoryStream();
            output.WriteByte(SerializationUtils.VersionId);
            EncodeTagToOutPut("Key1", "Value1", output);
            EncodeTagToOutPut("Key2", "Value2", output);

            var expected = DistributedContextBuilder.CreateContext(
                new List<CorrelationContextEntry>(2) { new CorrelationContextEntry("Key1", "Value1"), new CorrelationContextEntry("Key2", "Value2") });
            Assert.Equal(expected, this.serializer.FromByteArray(output.ToArray()));
        }

        [Fact]
        public void TestDeserializeDuplicateKeys()
        {
            var output = new MemoryStream();
            output.WriteByte(SerializationUtils.VersionId);
            EncodeTagToOutPut("Key1", "Value1", output);
            EncodeTagToOutPut("Key1", "Value2", output);

            var expected = DistributedContextBuilder.CreateContext("Key1", "Value2");
            Assert.Equal(expected, this.serializer.FromByteArray(output.ToArray()));
        }

        [Fact]
        public void TestDeserializeNonConsecutiveDuplicateKeys()
        {
            var output = new MemoryStream();
            output.WriteByte(SerializationUtils.VersionId);
            EncodeTagToOutPut("Key1", "Value1", output);
            EncodeTagToOutPut("Key2", "Value2", output);
            EncodeTagToOutPut("Key3", "Value3", output);
            EncodeTagToOutPut("Key1", "Value4", output);
            EncodeTagToOutPut("Key2", "Value5", output);

            var expected = DistributedContextBuilder.CreateContext(
                new List<CorrelationContextEntry>(3)
                {
                    new CorrelationContextEntry("Key1", "Value4"),
                    new CorrelationContextEntry("Key2", "Value5"),
                    new CorrelationContextEntry("Key3", "Value3"),
                });
            Assert.Equal(expected, this.serializer.FromByteArray(output.ToArray()));
        }

        [Fact]
        public void TestDeserializeDuplicateTags()
        {
            var output = new MemoryStream();
            output.WriteByte(SerializationUtils.VersionId);
            EncodeTagToOutPut("Key1", "Value1", output);
            EncodeTagToOutPut("Key1", "Value2", output);

            var expected = DistributedContextBuilder.CreateContext("Key1", "Value2");
            Assert.Equal(expected, this.serializer.FromByteArray(output.ToArray()));
        }

        [Fact]
        public void TestDeserializeNonConsecutiveDuplicateTags()
        {
            var output = new MemoryStream();
            output.WriteByte(SerializationUtils.VersionId);
            EncodeTagToOutPut("Key1", "Value1", output);
            EncodeTagToOutPut("Key2", "Value2", output);
            EncodeTagToOutPut("Key3", "Value3", output);
            EncodeTagToOutPut("Key1", "Value1", output);
            EncodeTagToOutPut("Key2", "Value2", output);

            var expected = DistributedContextBuilder.CreateContext(
                new List<CorrelationContextEntry>(3)
                {
                    new CorrelationContextEntry("Key1", "Value1"),
                    new CorrelationContextEntry("Key2", "Value2"),
                    new CorrelationContextEntry("Key3", "Value3"),
                });
            Assert.Equal(expected, this.serializer.FromByteArray(output.ToArray()));
        }

        [Fact]
        public void StopParsingAtUnknownField()
        {
            var output = new MemoryStream();
            output.WriteByte(SerializationUtils.VersionId);
            EncodeTagToOutPut("Key1", "Value1", output);
            EncodeTagToOutPut("Key2", "Value2", output);

            // Write unknown field ID 1.
            output.WriteByte(1);
            output.Write(new byte[] { 1, 2, 3, 4 }, 0, 4);

            EncodeTagToOutPut("Key3", "Value3", output);

            var expected = DistributedContextBuilder.CreateContext(
                new List<CorrelationContextEntry>(2)
                {
                    new CorrelationContextEntry("Key1", "Value1"),
                    new CorrelationContextEntry("Key2", "Value2"),
                });
            Assert.Equal(expected, this.serializer.FromByteArray(output.ToArray()));
        }

        [Fact]
        public void StopParsingAtUnknownTagAtStart()
        {
            var output = new MemoryStream();
            output.WriteByte(SerializationUtils.VersionId);

            // Write unknown field ID 1.
            output.WriteByte(1);
            output.Write(new byte[] { 1, 2, 3, 4 }, 0, 4);

            EncodeTagToOutPut("Key", "Value", output);
            Assert.Equal(DistributedContext.Empty, this.serializer.FromByteArray(output.ToArray()));
        }

        [Fact]
        public void TestDeserializeWrongFormat()
        {
            // encoded tags should follow the format <version_id>(<tag_field_id><tag_encoding>)*
            Assert.Equal(DistributedContext.Empty, this.serializer.FromByteArray(new byte[3]));
        }

        [Fact]
        public void TestDeserializeWrongVersionId()
        {
            Assert.Equal(DistributedContext.Empty, this.serializer.FromByteArray(new byte[] { SerializationUtils.VersionId + 1 }));
        }

        [Fact]
        public void TestDeserializeNegativeVersionId()
        {
            Assert.Equal(DistributedContext.Empty, this.serializer.FromByteArray(new byte[] { 0xff }));
        }

        // <tag_encoding> ==
        //       <tag_key_len><tag_key><tag_val_len><tag_val>
        //         <tag_key_len> == varint encoded integer
        //         <tag_key> == tag_key_len bytes comprising tag key name
        //         <tag_val_len> == varint encoded integer
        //         <tag_val> == tag_val_len bytes comprising UTF-8 string
        private static void EncodeTagToOutPut(string key, string value, MemoryStream output)
        {
            output.WriteByte(SerializationUtils.TagFieldId);
            EncodeString(key, output);
            EncodeString(value, output);
        }

        private static void EncodeString(string input, MemoryStream output)
        {
            var length = input.Length;
            var bytes = new byte[VarInt.VarIntSize(length)];
            VarInt.PutVarInt(length, bytes, 0);
            output.Write(bytes, 0, bytes.Length);
            var inPutBytes = Encoding.UTF8.GetBytes(input);
            output.Write(inPutBytes, 0, inPutBytes.Length);
        }
    }
}
