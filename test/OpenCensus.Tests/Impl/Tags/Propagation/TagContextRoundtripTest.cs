// <copyright file="TagContextRoundtripTest.cs" company="OpenCensus Authors">
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
    using Xunit;

    public class TagContextRoundtripTest
    {

        private static readonly ITagKey K1 = TagKey.Create("k1");
        private static readonly ITagKey K2 = TagKey.Create("k2");
        private static readonly ITagKey K3 = TagKey.Create("k3");

        private static readonly ITagValue V_EMPTY = TagValue.Create("");
        private static readonly ITagValue V1 = TagValue.Create("v1");
        private static readonly ITagValue V2 = TagValue.Create("v2");
        private static readonly ITagValue V3 = TagValue.Create("v3");

        private readonly TagsComponent tagsComponent = new TagsComponent();
        private readonly ITagContextBinarySerializer serializer;
        private readonly ITagger tagger;

        public TagContextRoundtripTest()
        {
            serializer = tagsComponent.TagPropagationComponent.BinarySerializer;
            tagger = tagsComponent.Tagger;
        }

        [Fact]
        public void TestRoundtripSerialization_NormalTagContext()
        {
            TestRoundtripSerialization(tagger.Empty);
            TestRoundtripSerialization(tagger.EmptyBuilder.Put(K1, V1).Build());
            TestRoundtripSerialization(tagger.EmptyBuilder.Put(K1, V1).Put(K2, V2).Put(K3, V3).Build());
            TestRoundtripSerialization(tagger.EmptyBuilder.Put(K1, V_EMPTY).Build());
        }

        [Fact]
        public void TestRoundtrip_TagContextWithMaximumSize()
        {
            ITagContextBuilder builder = tagger.EmptyBuilder;
            for (int i = 0; i < SerializationUtils.TagContextSerializedSizeLimit / 8; i++)
            {
                // Each tag will be with format {key : "0123", value : "0123"}, so the length of it is 8.
                // Add 1024 tags, the total size should just be 8192.
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
                    str = "" + i;
                }
                builder.Put(TagKey.Create(str), TagValue.Create(str));
            }
            TestRoundtripSerialization(builder.Build());
        }

        private void TestRoundtripSerialization(ITagContext expected)
        {
            byte[] bytes = serializer.ToByteArray(expected);
            ITagContext actual = serializer.FromByteArray(bytes);
            Assert.Equal(expected, actual);
        }
    }
}
