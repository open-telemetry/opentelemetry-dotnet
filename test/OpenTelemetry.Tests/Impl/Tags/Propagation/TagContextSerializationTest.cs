// <copyright file="TagContextSerializationTest.cs" company="OpenTelemetry Authors">
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OpenTelemetry.Internal;
using Xunit;

namespace OpenTelemetry.Tags.Propagation.Test
{
    public class TagContextSerializationTest
    {
        private static readonly string K1 = "k1";
        private static readonly string K2 = "k2";
        private static readonly string K3 = "k3";
        private static readonly string K4 = "k4";

        private static readonly string V1 = "v1";
        private static readonly string V2 = "v2";
        private static readonly string V3 = "v3";
        private static readonly string V4 = "v4";

        private static readonly DistributedContextEntry T1 = new DistributedContextEntry(K1, V1);
        private static readonly DistributedContextEntry T2 = new DistributedContextEntry(K2, V2);
        private static readonly DistributedContextEntry T3 = new DistributedContextEntry(K3, V3);
        private static readonly DistributedContextEntry T4 = new DistributedContextEntry(K4, V4);

        private readonly CurrentTaggingState state;
        private readonly ITagger tagger;
        private readonly ITagContextBinarySerializer serializer;

        public TagContextSerializationTest()
        {
            state = new CurrentTaggingState();
            tagger = new Tagger(state);
            serializer = new TagContextBinarySerializer(state);
        }

        [Fact]
        public void TestSerializeDefault()
        {
            TestSerialize();
        }

        [Fact]
        public void TestSerializeWithOneTag()
        {
            TestSerialize(T1);
        }

        [Fact]
        public void TestSerializeWithMultipleTags()
        {
            TestSerialize(T1, T2, T3, T4);
        }

        [Fact]
        public void TestSerializeTooLargeTagContext()
        {
            var builder = tagger.EmptyBuilder;
            for (var i = 0; i < SerializationUtils.TagContextSerializedSizeLimit / 8 - 1; i++) {
                // Each tag will be with format {key : "0123", value : "0123"}, so the length of it is 8.
                String str;
                if (i < 10)
                {
                    str = "000" + i;
                }
                else if (i < 100)
                {
                    str = "00" + i;
                }
                else if (i < 1000)
                {
                    str = "0" + i;
                }
                else
                {
                    str = i.ToString();
                }
                builder.Put(str, str);
            }
            // The last tag will be of size 9, so the total size of the TagContext (8193) will be one byte
            // more than limit.
            builder.Put("last", "last1");

            var tagContext = builder.Build();

            Assert.Throws<TagContextSerializationException>(() => serializer.ToByteArray(tagContext));
        }

        private void TestSerialize(params DistributedContextEntry[] tags)
        {
            var builder = tagger.EmptyBuilder;
            foreach (var tag in tags)
            {
                builder.Put(tag.Key, tag.Value);
            }

            var actual = serializer.ToByteArray(builder.Build());
            var tagsList = tags.ToList();
            var tagPermutation = Permutate(tagsList, tagsList.Count);
            ISet<String> possibleOutPuts = new HashSet<String>();
            foreach (List<DistributedContextEntry> list in tagPermutation) {
                var expected = new MemoryStream();
                expected.WriteByte(SerializationUtils.VersionId);
                foreach (var tag in list) {
                    expected.WriteByte(SerializationUtils.TagFieldId);
                    EncodeString(tag.Key, expected);
                    EncodeString(tag.Value, expected);
                }
                var bytes = expected.ToArray();
                possibleOutPuts.Add(Encoding.UTF8.GetString(bytes));
            }
            var exp = Encoding.UTF8.GetString(actual);
            Assert.Contains(exp, possibleOutPuts);
        }

        private static void EncodeString(String input, MemoryStream byteArrayOutPutStream)
        {
            VarInt.PutVarInt(input.Length, byteArrayOutPutStream);
            var inpBytes = Encoding.UTF8.GetBytes(input);
            byteArrayOutPutStream.Write(inpBytes, 0, inpBytes.Length);
        }

        internal static void RotateRight(IList sequence, int count)
        {
            var tmp = sequence[count - 1];
            sequence.RemoveAt(count - 1);
            sequence.Insert(0, tmp);
        }

        internal static IEnumerable<IList> Permutate(IList sequence, int count)
        {
            if (count == 0)
            {
                yield return sequence;
            }
            else
            {
                for (var i = 0; i < count; i++)
                {
                    foreach (var perm in Permutate(sequence, count - 1))
                    {
                        yield return perm;
                    }

                    RotateRight(sequence, count);
                }
            }
        }
    }
}
