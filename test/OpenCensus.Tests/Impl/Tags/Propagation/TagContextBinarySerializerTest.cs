// <copyright file="TagContextBinarySerializerTest.cs" company="OpenCensus Authors">
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
    using System.Collections.Generic;

    public class TagContextBinarySerializerTest
    {
        private readonly TagsComponent tagsComponent = new TagsComponent();
        private readonly ITagContextBinarySerializer serializer;

        private readonly ITagContext tagContext = new TestTagContext();
        // new TagContext()
        //  {
        //      @Override
        //  public Iterator<Tag> getIterator()
        //      {
        //          return ImmutableSet.< Tag > of(Tag.create(TagKey.create("key"), TagValue.create("value")))
        //              .iterator();
        //      }
        //  };

        public TagContextBinarySerializerTest()
        {
            serializer = tagsComponent.TagPropagationComponent.BinarySerializer;
        }
        // [Fact]
        // public void ToByteArray_TaggingDisabled()
        // {
        //    tagsComponent.setState(TaggingState.DISABLED);
        //    assertThat(serializer.toByteArray(tagContext)).isEmpty();
        // }

        // [Fact]
        // public void ToByteArray_TaggingReenabled()
        // {
        //    byte[] serialized = serializer.ToByteArray(tagContext);
        //    tagsComponent.setState(TaggingState.DISABLED);
        //    assertThat(serializer.toByteArray(tagContext)).isEmpty();
        //    tagsComponent.setState(TaggingState.ENABLED);
        //    assertThat(serializer.toByteArray(tagContext)).isEqualTo(serialized);
        // }

        // [Fact]
        // public void FromByteArray_TaggingDisabled()
        // {
        //    byte[] serialized = serializer.toByteArray(tagContext);
        //    tagsComponent.setState(TaggingState.DISABLED);
        //    assertThat(TagsTestUtil.tagContextToList(serializer.fromByteArray(serialized))).isEmpty();
        // }

        // [Fact]
        // public void FromByteArray_TaggingReenabled()
        // {
        //    byte[] serialized = serializer.toByteArray(tagContext);
        //    tagsComponent.setState(TaggingState.DISABLED);
        //    assertThat(TagsTestUtil.tagContextToList(serializer.fromByteArray(serialized))).isEmpty();
        //    tagsComponent.setState(TaggingState.ENABLED);
        //    assertThat(serializer.fromByteArray(serialized)).isEqualTo(tagContext);
        // }
        class TestTagContext : TagContextBase
        {
            public TestTagContext()
            {

            }

            public override IEnumerator<ITag> GetEnumerator()
            {
                return new List<ITag>() { Tag.Create(TagKey.Create("key"), TagValue.Create("value")) }.GetEnumerator();
            }
        }
    }
}
