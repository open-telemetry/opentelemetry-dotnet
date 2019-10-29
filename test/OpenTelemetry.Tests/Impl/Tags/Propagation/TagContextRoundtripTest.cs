// <copyright file="TagContextRoundtripTest.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Tags.Propagation.Test
{
    public class TagContextRoundtripTest
    {

        private static readonly string K1 = "k1";
        private static readonly string K2 = "k2";
        private static readonly string K3 = "k3";

        private static readonly string V_EMPTY = "";
        private static readonly string V1 = "v1";
        private static readonly string V2 = "v2";
        private static readonly string V3 = "v3";

        private readonly CurrentTaggingState state;
        private readonly ITagger tagger;
        private readonly ITagContextBinarySerializer serializer;

        public TagContextRoundtripTest()
        {
            state = new CurrentTaggingState();
            tagger = new Tagger(state);
            serializer = new TagContextBinarySerializer(state);
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
            var builder = tagger.EmptyBuilder;
            for (var i = 0; i < SerializationUtils.TagContextSerializedSizeLimit / 8; i++)
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
                builder.Put(str, str);
            }
            TestRoundtripSerialization(builder.Build());
        }

        private void TestRoundtripSerialization(ITagContext expected)
        {
            var bytes = serializer.ToByteArray(expected);
            var actual = serializer.FromByteArray(bytes);
            Assert.Equal(expected, actual);
        }
    }
}
