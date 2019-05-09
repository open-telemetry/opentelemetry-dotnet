// <copyright file="SerializationUtils.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Tags.Propagation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using OpenCensus.Internal;

    internal static class SerializationUtils
    {
        internal const int VersionId = 0;
        internal const int TagFieldId = 0;

        // This size limit only applies to the bytes representing tag keys and values.
        internal const int TagContextSerializedSizeLimit = 8192;

        // Serializes a TagContext to the on-the-wire format.
        // Encoded tags are of the form: <version_id><encoded_tags>
        internal static byte[] SerializeBinary(ITagContext tags)
        {
            // Use a ByteArrayDataOutput to avoid needing to handle IOExceptions.
            // ByteArrayDataOutput byteArrayDataOutput = ByteStreams.newDataOutput();
            MemoryStream byteArrayDataOutput = new MemoryStream();

            byteArrayDataOutput.WriteByte(VersionId);
            int totalChars = 0; // Here chars are equivalent to bytes, since we're using ascii chars.
            foreach (var tag in tags)
            {
                totalChars += tag.Key.Name.Length;
                totalChars += tag.Value.AsString.Length;
                EncodeTag(tag, byteArrayDataOutput);
            }

            // for (Iterator<Tag> i = InternalUtils.getTags(tags); i.hasNext();) {
            //    Tag tag = i.next();
            //    totalChars += tag.getKey().getName().length();
            //    totalChars += tag.getValue().asString().length();
            //    encodeTag(tag, byteArrayDataOutput);
            // }
            if (totalChars > TagContextSerializedSizeLimit)
            {
                throw new TagContextSerializationException(
                    "Size of TagContext exceeds the maximum serialized size "
                        + TagContextSerializedSizeLimit);
            }

            return byteArrayDataOutput.ToArray();
        }

        // Deserializes input to TagContext based on the binary format standard.
        // The encoded tags are of the form: <version_id><encoded_tags>
        internal static ITagContext DeserializeBinary(byte[] bytes)
        {
            try
            {
                if (bytes.Length == 0)
                {
                    // Does not allow empty byte array.
                    throw new TagContextDeserializationException("Input byte[] can not be empty.");
                }

                MemoryStream buffer = new MemoryStream(bytes);
                int versionId = buffer.ReadByte();
                if (versionId > VersionId || versionId < 0)
                {
                    throw new TagContextDeserializationException(
                        "Wrong Version ID: " + versionId + ". Currently supports version up to: " + VersionId);
                }

                return new TagContext(ParseTags(buffer));
            }
            catch (Exception exn)
            {
                throw new TagContextDeserializationException(exn.ToString()); // byte array format error.
            }
        }

        internal static IDictionary<ITagKey, ITagValue> ParseTags(MemoryStream buffer)
        {
            IDictionary<ITagKey, ITagValue> tags = new Dictionary<ITagKey, ITagValue>();
            long limit = buffer.Length;
            int totalChars = 0; // Here chars are equivalent to bytes, since we're using ascii chars.
            while (buffer.Position < limit)
            {
                int type = buffer.ReadByte();
                if (type == TagFieldId)
                {
                    ITagKey key = CreateTagKey(DecodeString(buffer));
                    ITagValue val = CreateTagValue(key, DecodeString(buffer));
                    totalChars += key.Name.Length;
                    totalChars += val.AsString.Length;
                    tags[key] = val;
                }
else
                {
                    // Stop parsing at the first unknown field ID, since there is no way to know its length.
                    // TODO(sebright): Consider storing the rest of the byte array in the TagContext.
                    break;
                }
            }

            if (totalChars > TagContextSerializedSizeLimit)
            {
                throw new TagContextDeserializationException(
                    "Size of TagContext exceeds the maximum serialized size "
                        + TagContextSerializedSizeLimit);
            }

            return tags;
        }

        // TODO(sebright): Consider exposing a TagKey name validation method to avoid needing to catch an
        // IllegalArgumentException here.
        private static ITagKey CreateTagKey(string name)
        {
            try
            {
                return TagKey.Create(name);
            }
            catch (Exception e)
            {
                throw new TagContextDeserializationException("Invalid tag key: " + name, e);
            }
        }

        // TODO(sebright): Consider exposing a TagValue validation method to avoid needing to catch
        // an IllegalArgumentException here.
        private static ITagValue CreateTagValue(ITagKey key, string value)
        {
            try
            {
                return TagValue.Create(value);
            }
            catch (Exception e)
            {
                throw new TagContextDeserializationException(
                    "Invalid tag value for key " + key + ": " + value, e);
            }
        }

        private static void EncodeTag(ITag tag, MemoryStream byteArrayDataOutput)
        {
            byteArrayDataOutput.WriteByte(TagFieldId);
            EncodeString(tag.Key.Name, byteArrayDataOutput);
            EncodeString(tag.Value.AsString, byteArrayDataOutput);
        }

        private static void EncodeString(string input, MemoryStream byteArrayDataOutput)
        {
            PutVarInt(input.Length, byteArrayDataOutput);
            var bytes = Encoding.UTF8.GetBytes(input);
            byteArrayDataOutput.Write(bytes, 0, bytes.Length);
        }

        private static void PutVarInt(int input, MemoryStream byteArrayDataOutput)
        {
            byte[] output = new byte[VarInt.VarIntSize(input)];
            VarInt.PutVarInt(input, output, 0);
            byteArrayDataOutput.Write(output, 0, output.Length);
        }

        private static string DecodeString(MemoryStream buffer)
        {
            int length = VarInt.GetVarInt(buffer);
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                builder.Append((char)buffer.ReadByte());
            }

            return builder.ToString();
        }
    }
}
