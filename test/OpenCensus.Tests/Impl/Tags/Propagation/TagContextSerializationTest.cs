// <copyright file="TagContextSerializationTest.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Tags.Propagation.Test
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using OpenCensus.Internal;
    using Xunit;

    public class TagContextSerializationTest
    {
        private static readonly ITagKey K1 = TagKey.Create("k1");
        private static readonly ITagKey K2 = TagKey.Create("k2");
        private static readonly ITagKey K3 = TagKey.Create("k3");
        private static readonly ITagKey K4 = TagKey.Create("k4");

        private static readonly ITagValue V1 = TagValue.Create("v1");
        private static readonly ITagValue V2 = TagValue.Create("v2");
        private static readonly ITagValue V3 = TagValue.Create("v3");
        private static readonly ITagValue V4 = TagValue.Create("v4");

        private static readonly ITag T1 = Tag.Create(K1, V1);
        private static readonly ITag T2 = Tag.Create(K2, V2);
        private static readonly ITag T3 = Tag.Create(K3, V3);
        private static readonly ITag T4 = Tag.Create(K4, V4);

        private readonly TagsComponent tagsComponent = new TagsComponent();
        private readonly ITagContextBinarySerializer serializer;
        private readonly ITagger tagger;

        public TagContextSerializationTest()
        {
            serializer = tagsComponent.TagPropagationComponent.BinarySerializer;
            tagger = tagsComponent.Tagger;
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
            ITagContextBuilder builder = tagger.EmptyBuilder;
            for (int i = 0; i < SerializationUtils.TagContextSerializedSizeLimit / 8 - 1; i++) {
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
                builder.Put(TagKey.Create(str), TagValue.Create(str));
            }
            // The last tag will be of size 9, so the total size of the TagContext (8193) will be one byte
            // more than limit.
            builder.Put(TagKey.Create("last"), TagValue.Create("last1"));

            ITagContext tagContext = builder.Build();

            Assert.Throws<TagContextSerializationException>(() => serializer.ToByteArray(tagContext));
        }

        private void TestSerialize(params ITag[] tags)
        {
            ITagContextBuilder builder = tagger.EmptyBuilder;
            foreach (var tag in tags)
            {
                builder.Put(tag.Key, tag.Value);
            }

            byte[] actual = serializer.ToByteArray(builder.Build());
            var tagsList = tags.ToList();
            var tagPermutation = Permutate(tagsList, tagsList.Count);
            ISet<String> possibleOutPuts = new HashSet<String>();
            foreach (List<ITag> list in tagPermutation) {
                MemoryStream expected = new MemoryStream();
                expected.WriteByte(SerializationUtils.VersionId);
                foreach (ITag tag in list) {
                    expected.WriteByte(SerializationUtils.TagFieldId);
                    EncodeString(tag.Key.Name, expected);
                    EncodeString(tag.Value.AsString, expected);
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
            object tmp = sequence[count - 1];
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
                for (int i = 0; i < count; i++)
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
