﻿// <copyright file="SerializationUtils.cs" company="OpenTelemetry Authors">
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
using System.IO;
using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context.Propagation
{
    internal static class SerializationUtils
    {
        internal const int VersionId = 0;
        internal const int TagFieldId = 0;

        // This size limit only applies to the bytes representing tag keys and values.
        internal const int TagContextSerializedSizeLimit = 8192;

        // Serializes a DistributedContext to the on-the-wire format.
        // Encoded tags are of the form: <version_id><encoded_tags>
        internal static byte[] SerializeBinary(DistributedContext dc)
        {
            // Use a ByteArrayDataOutput to avoid needing to handle IOExceptions.
            // ByteArrayDataOutput byteArrayDataOutput = ByteStreams.newDataOutput();
            var byteArrayDataOutput = new MemoryStream();

            byteArrayDataOutput.WriteByte(VersionId);
            var totalChars = 0; // Here chars are equivalent to bytes, since we're using ascii chars.
            foreach (var tag in dc.Entries)
            {
                totalChars += tag.Key.Length;
                totalChars += tag.Value.Length;
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
                throw new DistributedContextSerializationException(
                    "Size of DistributedContext exceeds the maximum serialized size "
                        + TagContextSerializedSizeLimit);
            }

            return byteArrayDataOutput.ToArray();
        }

        // Deserializes input to DistributedContext based on the binary format standard.
        // The encoded tags are of the form: <version_id><encoded_tags>
        internal static DistributedContext DeserializeBinary(byte[] bytes)
        {
            try
            {
                if (bytes.Length == 0)
                {
                    // Does not allow empty byte array.
                    throw new DistributedContextDeserializationException("Input byte[] can not be empty.");
                }

                var buffer = new MemoryStream(bytes);
                var versionId = buffer.ReadByte();
                if (versionId > VersionId || versionId < 0)
                {
                    throw new DistributedContextDeserializationException(
                        "Wrong Version ID: " + versionId + ". Currently supports version up to: " + VersionId);
                }

                return new DistributedContext(ParseTags(buffer));
            }
            catch (Exception exn)
            {
                throw new DistributedContextDeserializationException(exn.ToString()); // byte array format error.
            }
        }

        internal static IEnumerable<DistributedContextEntry> ParseTags(MemoryStream buffer)
        {
            List<DistributedContextEntry> tags = new List<DistributedContextEntry>();
            var limit = buffer.Length;
            var totalChars = 0; // Here chars are equivalent to bytes, since we're using ascii chars.
            while (buffer.Position < limit)
            {
                var type = buffer.ReadByte();
                if (type == TagFieldId)
                {
                    var key = CreateTagKey(DecodeString(buffer));
                    var val = CreateTagValue(key, DecodeString(buffer));
                    totalChars += key.Length;
                    totalChars += val.Length;
                    tags.Add(new DistributedContextEntry(key, val));
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
                throw new DistributedContextDeserializationException(
                    "Size of TagContext exceeds the maximum serialized size "
                        + TagContextSerializedSizeLimit);
            }

            return tags;
        }

        // TODO(sebright): Consider exposing a string name validation method to avoid needing to catch an
        // IllegalArgumentException here.
        private static string CreateTagKey(string name)
        {
            try
            {
                return name;
            }
            catch (Exception e)
            {
                throw new DistributedContextDeserializationException("Invalid tag key: " + name, e);
            }
        }

        // TODO(sebright): Consider exposing a string validation method to avoid needing to catch
        // an IllegalArgumentException here.
        private static string CreateTagValue(string key, string value)
        {
            try
            {
                return value;
            }
            catch (Exception e)
            {
                throw new DistributedContextDeserializationException(
                    "Invalid tag value for key " + key + ": " + value, e);
            }
        }

        private static void EncodeTag(DistributedContextEntry tag, MemoryStream byteArrayDataOutput)
        {
            byteArrayDataOutput.WriteByte(TagFieldId);
            EncodeString(tag.Key, byteArrayDataOutput);
            EncodeString(tag.Value, byteArrayDataOutput);
        }

        private static void EncodeString(string input, MemoryStream byteArrayDataOutput)
        {
            PutVarInt(input.Length, byteArrayDataOutput);
            var bytes = Encoding.UTF8.GetBytes(input);
            byteArrayDataOutput.Write(bytes, 0, bytes.Length);
        }

        private static void PutVarInt(int input, MemoryStream byteArrayDataOutput)
        {
            var output = new byte[VarInt.VarIntSize(input)];
            VarInt.PutVarInt(input, output, 0);
            byteArrayDataOutput.Write(output, 0, output.Length);
        }

        private static string DecodeString(MemoryStream buffer)
        {
            var length = VarInt.GetVarInt(buffer);
            var builder = new StringBuilder();
            for (var i = 0; i < length; i++)
            {
                builder.Append((char)buffer.ReadByte());
            }

            return builder.ToString();
        }
    }
}
