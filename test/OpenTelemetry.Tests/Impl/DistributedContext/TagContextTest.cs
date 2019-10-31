// <copyright file="TagContextTest.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.DistributedContext.Test
{
    public class TagContextTest
    {
        private readonly ITagger tagger = new Tagger(new CurrentTaggingState());

        private static readonly string K1 = "k1";
        private static readonly string K2 = "k2";

        private static readonly string V1 = "v1";
        private static readonly string V2 = "v2";


        [Fact]
        public void getTags_empty()
        {
            var tags = new TagContext(new Dictionary<string, string>());
            Assert.Empty(tags.Tags);
        }

        [Fact]
        public void getTags_nonEmpty()
        {
            var tags = new TagContext(new Dictionary<string, string>() { { K1, V1 }, { K2, V2 } });
            Assert.Equal(new Dictionary<string, string>() { { K1, V1 }, { K2, V2 } }, tags.Tags);
        }

        [Fact]
        public void Put_NewKey()
        {
            var tags = new TagContext(new Dictionary<string, string>() { { K1, V1 } });
            Assert.Equal(new Dictionary<string, string>() { { K1, V1 }, { K2, V2 } },
                ((TagContext)tagger.ToBuilder(tags).Put(K2, V2).Build()).Tags);
        }

        [Fact]
        public void Put_ExistingKey()
        {
            var tags = new TagContext(new Dictionary<string, string>() { { K1, V1 } });
            Assert.Equal(new Dictionary<string, string>() { { K1, V2 } },
                ((TagContext)tagger.ToBuilder(tags).Put(K1, V2).Build()).Tags);
        }

        [Fact]
        public void Put_NullKey()
        {
            var tags = new TagContext(new Dictionary<string, string>() { { K1, V1 } });
            var builder = tagger.ToBuilder(tags);
            Assert.Throws<ArgumentNullException>(() => builder.Put(null, V2));
        }

        [Fact]
        public void Put_NullValue()
        {
            var tags = new TagContext(new Dictionary<string, string>() { { K1, V1 } });
            var builder = tagger.ToBuilder(tags);
            Assert.Throws<ArgumentNullException>(() => builder.Put(K2, null));
        }

        [Fact]
        public void Remove_ExistingKey()
        {
            var tags = new TagContext(new Dictionary<string, string>() { { K1, V1 }, { K2, V2 } });
            Assert.Equal(new Dictionary<string, string>() { { K2, V2 } }, ((TagContext)tagger.ToBuilder(tags).Remove(K1).Build()).Tags);
        }

        [Fact]
        public void Remove_DifferentKey()
        {
            var tags = new TagContext(new Dictionary<string, string>() { { K1, V1 } });
            Assert.Equal(new Dictionary<string, string>() { { K1, V1 } }, ((TagContext)tagger.ToBuilder(tags).Remove(K2).Build()).Tags);
        }

        [Fact]
        public void Remove_NullKey()
        {
            var tags = new TagContext(new Dictionary<string, string>() { { K1, V1 } });
            var builder = tagger.ToBuilder(tags);
            Assert.Throws<ArgumentNullException>(() => builder.Remove(null));
        }

        [Fact]
        public void TestIterator()
        {
            var tags = new TagContext(new Dictionary<string, string>() { { K1, V1 }, { K2, V2 } });
            var i = tags.GetEnumerator();
            Assert.True(i.MoveNext());
            var tag1 = i.Current;
            Assert.True(i.MoveNext());
            var tag2 = i.Current;
            Assert.False(i.MoveNext());
            Assert.Equal(new List<DistributedContextEntry>() { new DistributedContextEntry(K1, V1), new DistributedContextEntry(K2, V2)}, new List<DistributedContextEntry>() { tag1, tag2 });

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
            public override IEnumerator<DistributedContextEntry> GetEnumerator()
            {
                return new List<DistributedContextEntry>() { new DistributedContextEntry(K1, V1), new DistributedContextEntry(K2, V2) }.GetEnumerator();
            }
        }
    }
}
