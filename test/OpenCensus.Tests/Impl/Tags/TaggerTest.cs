// <copyright file="TaggerTest.cs" company="OpenCensus Authors">
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
    using System.Collections.Generic;
    using OpenCensus.Common;
    using OpenCensus.Tags.Unsafe;
    using Xunit;

    public class TaggerTest
    {
        private readonly TagsComponent tagsComponent = new TagsComponent();
        private readonly ITagger tagger;

        private static readonly ITagKey K1 = TagKey.Create("k1");
        private static readonly ITagKey K2 = TagKey.Create("k2");
        private static readonly ITagKey K3 = TagKey.Create("k3");

        private static readonly ITagValue V1 = TagValue.Create("v1");
        private static readonly ITagValue V2 = TagValue.Create("v2");
        private static readonly ITagValue V3 = TagValue.Create("v3");

        private static readonly ITag TAG1 = Tag.Create(K1, V1);
        private static readonly ITag TAG2 = Tag.Create(K2, V2);
        private static readonly ITag TAG3 = Tag.Create(K3, V3);

        public TaggerTest()
        {
            tagger = tagsComponent.Tagger;
        }

        [Fact]
        public void Empty()
        {
            Assert.Empty(TagsTestUtil.TagContextToList(tagger.Empty));
            Assert.IsType<TagContext>(tagger.Empty);
        }

        // [Fact]
        // public void Empty_TaggingDisabled()
        // {
        //    tagsComponent.State =TaggingState.DISABLED);
        //    Assert.Empty(TagsTestUtil.TagContextToList(tagger.Empty)).isEmpty();
        //    Assert.IsType<TagContext>(tagger.Empty);
        // }

        [Fact]
        public void EmptyBuilder()
        {
            ITagContextBuilder builder = tagger.EmptyBuilder;
            Assert.IsType<TagContextBuilder>(builder);
            Assert.Empty(TagsTestUtil.TagContextToList(builder.Build()));
        }

        // [Fact]
        // public void EmptyBuilder_TaggingDisabled()
        // {
        //    tagsComponent.setState(TaggingState.DISABLED);
        //    Assert.Equal(tagger.EmptyBuilder).isSameAs(NoopTagContextBuilder.Instance);
        // }

        // [Fact]
        // public void EmptyBuilder_TaggingReenabled()
        // {
        //    tagsComponent.setState(TaggingState.DISABLED);
        //    Assert.Equal(tagger.EmptyBuilder).isSameAs(NoopTagContextBuilder.Instance);
        //    tagsComponent.setState(TaggingState.ENABLED);
        //    TagContextBuilder builder = tagger.EmptyBuilder;
        //    Assert.Equal(builder).isInstanceOf(TagContextBuilder);
        //    Assert.Equal(TagsTestUtil.TagContextToList(builder.put(K1, V1).Build())).containsExactly(Tag.Create(K1, V1));
        // }

        [Fact]
        public void CurrentBuilder()
        {
            ITagContext tags = new SimpleTagContext(TAG1, TAG2, TAG3);
            ITagContextBuilder result = GetResultOfCurrentBuilder(tags);
            Assert.IsType<TagContextBuilder>(result);
            Assert.Equal(new List<ITag>() { TAG1, TAG2, TAG3 }, TagsTestUtil.TagContextToList(result.Build()));
        }

        [Fact]
        public void CurrentBuilder_DefaultIsEmpty()
        {
            ITagContextBuilder currentBuilder = tagger.CurrentBuilder;
            Assert.IsType<TagContextBuilder>(currentBuilder);
            Assert.Empty(TagsTestUtil.TagContextToList(currentBuilder.Build()));
        }

        [Fact]
        public void CurrentBuilder_RemoveDuplicateTags()
        {
            ITag tag1 = Tag.Create(K1, V1);
            ITag tag2 = Tag.Create(K1, V2);
            ITagContext tagContextWithDuplicateTags = new SimpleTagContext(tag1, tag2);
            ITagContextBuilder result = GetResultOfCurrentBuilder(tagContextWithDuplicateTags);
            Assert.Equal(new List<ITag>() { tag2 }, TagsTestUtil.TagContextToList(result.Build()));
        }

        [Fact]
        public void CurrentBuilder_SkipNullTag()
        {
            ITagContext tagContextWithNullTag = new SimpleTagContext(TAG1, null, TAG2);
            ITagContextBuilder result = GetResultOfCurrentBuilder(tagContextWithNullTag);
            Assert.Equal(new List<ITag>() { TAG1, TAG2 }, TagsTestUtil.TagContextToList(result.Build()));
        }

        // [Fact]
        // public void CurrentBuilder_TaggingDisabled()
        // {
        //    tagsComponent.setState(TaggingState.DISABLED);
        //    Assert.Equal(getResultOfCurrentBuilder(new SimpleTagContext(TAG1)))
        //        .isSameAs(NoopTagContextBuilder.Instance);
        // }

        // [Fact]
        // public void currentBuilder_TaggingReenabled()
        // {
        //    TagContext tags = new SimpleTagContext(TAG1);
        //    tagsComponent.setState(TaggingState.DISABLED);
        //    Assert.Equal(getResultOfCurrentBuilder(tags)).isSameAs(NoopTagContextBuilder.Instance);
        //    tagsComponent.setState(TaggingState.ENABLED);
        //    TagContextBuilder builder = getResultOfCurrentBuilder(tags);
        //    Assert.Equal(builder).isInstanceOf(TagContextBuilder);
        //    Assert.Equal(TagsTestUtil.TagContextToList(builder.Build())).containsExactly(TAG1);
        // }

        private ITagContextBuilder GetResultOfCurrentBuilder(ITagContext tagsToSet)
        {
            ITagContext orig = AsyncLocalContext.CurrentTagContext;  // Context.current().withValue(ContextUtils.TAG_CONTEXT_KEY, tagsToSet).attach();
            AsyncLocalContext.CurrentTagContext = tagsToSet;
            try
            {
                return tagger.CurrentBuilder;
            }
            finally
            {
                AsyncLocalContext.CurrentTagContext = orig;
            }
        }

        [Fact]
        public void ToBuilder_ConvertUnknownTagContextToTagContext()
        {
            ITagContext unknownTagContext = new SimpleTagContext(TAG1, TAG2, TAG3);
            ITagContext newTagContext = tagger.ToBuilder(unknownTagContext).Build();
            Assert.Equal(new List<ITag>() { TAG1, TAG2, TAG3 }, TagsTestUtil.TagContextToList(newTagContext));
            Assert.IsType<TagContext>(newTagContext);
        }

        [Fact]
        public void ToBuilder_RemoveDuplicatesFromUnknownTagContext()
        {
            ITag tag1 = Tag.Create(K1, V1);
            ITag tag2 = Tag.Create(K1, V2);
            ITagContext tagContextWithDuplicateTags = new SimpleTagContext(tag1, tag2);
            ITagContext newTagContext = tagger.ToBuilder(tagContextWithDuplicateTags).Build();
            Assert.Equal(new List<ITag>() { tag2 }, TagsTestUtil.TagContextToList(newTagContext));
        }

        [Fact]
        public void ToBuilder_SkipNullTag()
        {
            ITagContext tagContextWithNullTag = new SimpleTagContext(TAG1, null, TAG2);
            ITagContext newTagContext = tagger.ToBuilder(tagContextWithNullTag).Build();
            Assert.Equal(new List<ITag>() { TAG1, TAG2 }, TagsTestUtil.TagContextToList(newTagContext));
        }

        // [Fact]
        // public void ToBuilder_TaggingDisabled()
        // {
        //    tagsComponent.setState(TaggingState.DISABLED);
        //    Assert.Equal(tagger.ToBuilder(new SimpleTagContext(TAG1)))
        //        .isSameAs(NoopTagContextBuilder.Instance);
        // }

        // [Fact]
        // public void ToBuilder_TaggingReenabled()
        // {
        //    TagContext tags = new SimpleTagContext(TAG1);
        //    tagsComponent.setState(TaggingState.DISABLED);
        //    Assert.Equal(tagger.ToBuilder(tags)).isSameAs(NoopTagContextBuilder.Instance);
        //    tagsComponent.setState(TaggingState.ENABLED);
        //    TagContextBuilder builder = tagger.ToBuilder(tags);
        //    Assert.Equal(builder).isInstanceOf(TagContextBuilder);
        //    Assert.Equal(TagsTestUtil.TagContextToList(builder.Build())).containsExactly(TAG1);
        // }

        [Fact]
        public void GetCurrentTagContext_DefaultIsEmptyTagContext()
        {
            ITagContext currentTagContext = tagger.CurrentTagContext;
            Assert.Empty(TagsTestUtil.TagContextToList(currentTagContext));
            Assert.IsType<TagContext>(currentTagContext);
        }

        [Fact]
        public void GetCurrentTagContext_ConvertUnknownTagContextToTagContext()
        {
            ITagContext unknownTagContext = new SimpleTagContext(TAG1, TAG2, TAG3);
            ITagContext result = GetResultOfGetCurrentTagContext(unknownTagContext);
            Assert.IsType<TagContext>(result);
            Assert.Equal(new List<ITag>() { TAG1, TAG2, TAG3 }, TagsTestUtil.TagContextToList(result));
        }

        [Fact]
        public void GetCurrentTagContext_RemoveDuplicatesFromUnknownTagContext()
        {
            ITag tag1 = Tag.Create(K1, V1);
            ITag tag2 = Tag.Create(K1, V2);
            ITagContext tagContextWithDuplicateTags = new SimpleTagContext(tag1, tag2);
            ITagContext result = GetResultOfGetCurrentTagContext(tagContextWithDuplicateTags);
            Assert.Equal(new List<ITag>() { tag2 }, TagsTestUtil.TagContextToList(result));
        }

        [Fact]
        public void GetCurrentTagContext_SkipNullTag()
        {
            ITagContext tagContextWithNullTag = new SimpleTagContext(TAG1, null, TAG2);
            ITagContext result = GetResultOfGetCurrentTagContext(tagContextWithNullTag);
            Assert.Equal(new List<ITag>() { TAG1, TAG2 }, TagsTestUtil.TagContextToList(result));
        }

        // [Fact]
        // public void GetCurrentTagContext_TaggingDisabled()
        // {
        //    tagsComponent.setState(TaggingState.DISABLED);
        //    Assert.Equal(TagsTestUtil.TagContextToList(getResultOfGetCurrentTagContext(new SimpleTagContext(TAG1))))
        //        .isEmpty();
        // }

        // [Fact]
        // public void getCurrentTagContext_TaggingReenabled()
        // {
        //    TagContext tags = new SimpleTagContext(TAG1);
        //    tagsComponent.setState(TaggingState.DISABLED);
        //    Assert.Equal(TagsTestUtil.TagContextToList(getResultOfGetCurrentTagContext(tags))).isEmpty();
        //    tagsComponent.setState(TaggingState.ENABLED);
        //    Assert.Equal(TagsTestUtil.TagContextToList(getResultOfGetCurrentTagContext(tags))).containsExactly(TAG1);
        // }

        private ITagContext GetResultOfGetCurrentTagContext(ITagContext tagsToSet)
        {
            ITagContext orig = AsyncLocalContext.CurrentTagContext;
            AsyncLocalContext.CurrentTagContext = tagsToSet;
            // Context orig = Context.current().withValue(ContextUtils.TAG_CONTEXT_KEY, tagsToSet).attach();
            try
            {
                return tagger.CurrentTagContext;
            }
            finally
            {
                AsyncLocalContext.CurrentTagContext = orig;
            }
        }

        [Fact]
        public void WithTagContext_ConvertUnknownTagContextToTagContext()
        {
            ITagContext unknownTagContext = new SimpleTagContext(TAG1, TAG2, TAG3);
            ITagContext result = GetResultOfWithTagContext(unknownTagContext);
            Assert.IsType<TagContext>(result);
            Assert.Equal(new List<ITag>() { TAG1, TAG2, TAG3 }, TagsTestUtil.TagContextToList(result));
        }

        [Fact]
        public void WithTagContext_RemoveDuplicatesFromUnknownTagContext()
        {
            ITag tag1 = Tag.Create(K1, V1);
            ITag tag2 = Tag.Create(K1, V2);
            ITagContext tagContextWithDuplicateTags = new SimpleTagContext(tag1, tag2);
            ITagContext result = GetResultOfWithTagContext(tagContextWithDuplicateTags);
            Assert.Equal(new List<ITag>() { tag2 }, TagsTestUtil.TagContextToList(result));
        }

        [Fact]
        public void WithTagContext_SkipNullTag()
        {
            ITagContext tagContextWithNullTag = new SimpleTagContext(TAG1, null, TAG2);
            ITagContext result = GetResultOfWithTagContext(tagContextWithNullTag);
            Assert.Equal(new List<ITag>() { TAG1, TAG2 }, TagsTestUtil.TagContextToList(result));
        }

        // [Fact]
        // public void WithTagContext_ReturnsNoopScopeWhenTaggingIsDisabled()
        // {
        //    tagsComponent.setState(TaggingState.DISABLED);
        //    Assert.Equal(tagger.withTagContext(new SimpleTagContext(TAG1))).isSameAs(NoopScope.getInstance());
        // }

        // [Fact]
        // public void withTagContext_TaggingDisabled()
        // {
        //    tagsComponent.setState(TaggingState.DISABLED);
        //    Assert.Equal(TagsTestUtil.TagContextToList(getResultOfWithTagContext(new SimpleTagContext(TAG1)))).isEmpty();
        // }

        // [Fact]
        // public void WithTagContext_TaggingReenabled()
        // {
        //    ITagContext tags = new SimpleTagContext(TAG1);
        //    tagsComponent.setState(TaggingState.DISABLED);
        //    Assert.Equal(TagsTestUtil.TagContextToList(getResultOfWithTagContext(tags))).isEmpty();
        //    tagsComponent.setState(TaggingState.ENABLED);
        //    Assert.Equal(TagsTestUtil.TagContextToList(getResultOfWithTagContext(tags))).containsExactly(TAG1);
        // }

        private ITagContext GetResultOfWithTagContext(ITagContext tagsToSet)
        {
            IScope scope = tagger.WithTagContext(tagsToSet);
            try
            {
                return AsyncLocalContext.CurrentTagContext;
            }
            finally
            {
                scope.Dispose();
            }
        }

        class SimpleTagContext : TagContextBase
        {
            private readonly List<ITag> tags;

            public SimpleTagContext(params ITag[] tags)
            {
                this.tags = new List<ITag>(tags);
            }

            public override IEnumerator<ITag> GetEnumerator()
            {
                return tags.GetEnumerator();
            }
        }
    }
}
