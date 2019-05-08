// <copyright file="TagContextTest.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Tags.Test
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    public class TagContextTest
    {
        private readonly ITagger tagger = new Tagger(new CurrentTaggingState());

        private static readonly ITagKey K1 = TagKey.Create("k1");
        private static readonly ITagKey K2 = TagKey.Create("k2");

        private static readonly ITagValue V1 = TagValue.Create("v1");
        private static readonly ITagValue V2 = TagValue.Create("v2");


        [Fact]
        public void getTags_empty()
        {
            TagContext tags = new TagContext(new Dictionary<ITagKey, ITagValue>());
            Assert.Empty(tags.Tags);
        }

        [Fact]
        public void getTags_nonEmpty()
        {
            TagContext tags = new TagContext(new Dictionary<ITagKey, ITagValue>() { { K1, V1 }, { K2, V2 } });
            Assert.Equal(new Dictionary<ITagKey, ITagValue>() { { K1, V1 }, { K2, V2 } }, tags.Tags);
        }

        [Fact]
        public void Put_NewKey()
        {
            TagContext tags = new TagContext(new Dictionary<ITagKey, ITagValue>() { { K1, V1 } });
            Assert.Equal(new Dictionary<ITagKey, ITagValue>() { { K1, V1 }, { K2, V2 } },
                ((TagContext)tagger.ToBuilder(tags).Put(K2, V2).Build()).Tags);
        }

        [Fact]
        public void Put_ExistingKey()
        {
            TagContext tags = new TagContext(new Dictionary<ITagKey, ITagValue>() { { K1, V1 } });
            Assert.Equal(new Dictionary<ITagKey, ITagValue>() { { K1, V2 } },
                ((TagContext)tagger.ToBuilder(tags).Put(K1, V2).Build()).Tags);
        }

        [Fact]
        public void Put_NullKey()
        {
            TagContext tags = new TagContext(new Dictionary<ITagKey, ITagValue>() { { K1, V1 } });
            ITagContextBuilder builder = tagger.ToBuilder(tags);
            Assert.Throws<ArgumentNullException>(() => builder.Put(null, V2));
        }

        [Fact]
        public void Put_NullValue()
        {
            TagContext tags = new TagContext(new Dictionary<ITagKey, ITagValue>() { { K1, V1 } });
            ITagContextBuilder builder = tagger.ToBuilder(tags);
            Assert.Throws<ArgumentNullException>(() => builder.Put(K2, null));
        }

        [Fact]
        public void Remove_ExistingKey()
        {
            TagContext tags = new TagContext(new Dictionary<ITagKey, ITagValue>() { { K1, V1 }, { K2, V2 } });
            Assert.Equal(new Dictionary<ITagKey, ITagValue>() { { K2, V2 } }, ((TagContext)tagger.ToBuilder(tags).Remove(K1).Build()).Tags);
        }

        [Fact]
        public void Remove_DifferentKey()
        {
            TagContext tags = new TagContext(new Dictionary<ITagKey, ITagValue>() { { K1, V1 } });
            Assert.Equal(new Dictionary<ITagKey, ITagValue>() { { K1, V1 } }, ((TagContext)tagger.ToBuilder(tags).Remove(K2).Build()).Tags);
        }

        [Fact]
        public void Remove_NullKey()
        {
            TagContext tags = new TagContext(new Dictionary<ITagKey, ITagValue>() { { K1, V1 } });
            ITagContextBuilder builder = tagger.ToBuilder(tags);
            Assert.Throws<ArgumentNullException>(() => builder.Remove(null));
        }

        [Fact]
        public void TestIterator()
        {
            TagContext tags = new TagContext(new Dictionary<ITagKey, ITagValue>() { { K1, V1 }, { K2, V2 } });
            var i = tags.GetEnumerator();
            Assert.True(i.MoveNext());
            ITag tag1 = i.Current;
            Assert.True(i.MoveNext());
            ITag tag2 = i.Current;
            Assert.False(i.MoveNext());
            Assert.Equal(new List<ITag>() { Tag.Create(K1, V1), Tag.Create(K2, V2)}, new List<ITag>() { tag1, tag2 });

        }



        [Fact]
        public void TestEquals()
        {
            // new EqualsTester()
            //    .addEqualityGroup(
            var t1 = tagger.EmptyBuilder.Put(K1, V1).Put(K2, V2).Build();
            var t2 = tagger.EmptyBuilder.Put(K1, V1).Put(K2, V2).Build();
            var t3 = tagger.EmptyBuilder.Put(K2, V2).Put(K1, V1).Build();
            var t4 = new TestTagContext();


            var t5 = tagger.EmptyBuilder.Put(K1, V1).Put(K2, V1).Build();
            var t6 = tagger.EmptyBuilder.Put(K1, V2).Put(K2, V1).Build();

            Assert.True(t1.Equals(t2));
            Assert.True(t1.Equals(t3));
            Assert.True(t1.Equals(t4));

            Assert.False(t1.Equals(t5));
            Assert.False(t2.Equals(t5));
            Assert.False(t3.Equals(t5));
            Assert.False(t4.Equals(t5));
            Assert.False(t6.Equals(t5));

            Assert.False(t5.Equals(t6));

        }

        class TestTagContext : TagContextBase
        {
            public override IEnumerator<ITag> GetEnumerator()
            {
                return new List<ITag>() { Tag.Create(K1, V1), Tag.Create(K2, V2) }.GetEnumerator();
            }
        }
    }
}
