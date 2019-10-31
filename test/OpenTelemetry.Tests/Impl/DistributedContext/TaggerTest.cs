// <copyright file="TaggerTest.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using OpenTelemetry.DistributedContext.Unsafe;
using Xunit;

namespace OpenTelemetry.DistributedContext.Test
{
    public class TaggerTest
    {
        private readonly CurrentTaggingState state;
        private readonly ITagger tagger;

        private static readonly string K1 = "k1";
        private static readonly string K2 = "k2";
        private static readonly string K3 = "k3";

        private static readonly string V1 = "v1";
        private static readonly string V2 = "v2";
        private static readonly string V3 = "v3";

        private static readonly DistributedContextEntry TAG1 = new DistributedContextEntry(K1, V1);
        private static readonly DistributedContextEntry TAG2 = new DistributedContextEntry(K2, V2);
        private static readonly DistributedContextEntry TAG3 = new DistributedContextEntry(K3, V3);

        public TaggerTest()
        {
            state = new CurrentTaggingState();
            tagger = new Tagger(state);
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
            var builder = tagger.EmptyBuilder;
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
        //    TagContextBuilder factory = tagger.EmptyBuilder;
        //    Assert.Equal(factory).isInstanceOf(TagContextBuilder);
        //    Assert.Equal(TagsTestUtil.TagContextToList(factory.put(K1, V1).Build())).containsExactly(Tag.Create(K1, V1));
        // }

        [Fact]
        public void CurrentBuilder()
        {
            ITagContext tags = new SimpleTagContext(TAG1, TAG2, TAG3);
            var result = GetResultOfCurrentBuilder(tags);
            Assert.IsType<TagContextBuilder>(result);
            Assert.Equal(new List<DistributedContextEntry>() { TAG1, TAG2, TAG3 }, TagsTestUtil.TagContextToList(result.Build()));
        }

        [Fact]
        public void CurrentBuilder_DefaultIsEmpty()
        {
            var currentBuilder = tagger.CurrentBuilder;
            Assert.IsType<TagContextBuilder>(currentBuilder);
            Assert.Empty(TagsTestUtil.TagContextToList(currentBuilder.Build()));
        }

        [Fact]
        public void CurrentBuilder_RemoveDuplicateTags()
        {
            var tag1 = new DistributedContextEntry(K1, V1);
            var tag2 = new DistributedContextEntry(K1, V2);
            ITagContext tagContextWithDuplicateTags = new SimpleTagContext(tag1, tag2);
            var result = GetResultOfCurrentBuilder(tagContextWithDuplicateTags);
            Assert.Equal(new List<DistributedContextEntry>() { tag2 }, TagsTestUtil.TagContextToList(result.Build()));
        }

        [Fact]
        public void CurrentBuilder_SkipNullTag()
        {
            ITagContext tagContextWithNullTag = new SimpleTagContext(TAG1, null, TAG2);
            var result = GetResultOfCurrentBuilder(tagContextWithNullTag);
            Assert.Equal(new List<DistributedContextEntry>() { TAG1, TAG2 }, TagsTestUtil.TagContextToList(result.Build()));
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
        //    TagContextBuilder factory = getResultOfCurrentBuilder(tags);
        //    Assert.Equal(factory).isInstanceOf(TagContextBuilder);
        //    Assert.Equal(TagsTestUtil.TagContextToList(factory.Build())).containsExactly(TAG1);
        // }

        private ITagContextBuilder GetResultOfCurrentBuilder(ITagContext tagsToSet)
        {
            var orig = AsyncLocalContext.CurrentTagContext;  // Context.current().withValue(ContextUtils.TAG_CONTEXT_KEY, tagsToSet).attach();
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
            var newTagContext = tagger.ToBuilder(unknownTagContext).Build();
            Assert.Equal(new List<DistributedContextEntry>() { TAG1, TAG2, TAG3 }, TagsTestUtil.TagContextToList(newTagContext));
            Assert.IsType<TagContext>(newTagContext);
        }

        [Fact]
        public void ToBuilder_RemoveDuplicatesFromUnknownTagContext()
        {
            var tag1 = new DistributedContextEntry(K1, V1);
            var tag2 = new DistributedContextEntry(K1, V2);
            ITagContext tagContextWithDuplicateTags = new SimpleTagContext(tag1, tag2);
            var newTagContext = tagger.ToBuilder(tagContextWithDuplicateTags).Build();
            Assert.Equal(new List<DistributedContextEntry>() { tag2 }, TagsTestUtil.TagContextToList(newTagContext));
        }

        [Fact]
        public void ToBuilder_SkipNullTag()
        {
            ITagContext tagContextWithNullTag = new SimpleTagContext(TAG1, null, TAG2);
            var newTagContext = tagger.ToBuilder(tagContextWithNullTag).Build();
            Assert.Equal(new List<DistributedContextEntry>() { TAG1, TAG2 }, TagsTestUtil.TagContextToList(newTagContext));
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
        //    TagContextBuilder factory = tagger.ToBuilder(tags);
        //    Assert.Equal(factory).isInstanceOf(TagContextBuilder);
        //    Assert.Equal(TagsTestUtil.TagContextToList(factory.Build())).containsExactly(TAG1);
        // }

        [Fact]
        public void GetCurrentTagContext_DefaultIsEmptyTagContext()
        {
            var currentTagContext = tagger.CurrentTagContext;
            Assert.Empty(TagsTestUtil.TagContextToList(currentTagContext));
            Assert.IsType<TagContext>(currentTagContext);
        }

        [Fact]
        public void GetCurrentTagContext_ConvertUnknownTagContextToTagContext()
        {
            ITagContext unknownTagContext = new SimpleTagContext(TAG1, TAG2, TAG3);
            var result = GetResultOfGetCurrentTagContext(unknownTagContext);
            Assert.IsType<TagContext>(result);
            Assert.Equal(new List<DistributedContextEntry>() { TAG1, TAG2, TAG3 }, TagsTestUtil.TagContextToList(result));
        }

        [Fact]
        public void GetCurrentTagContext_RemoveDuplicatesFromUnknownTagContext()
        {
            var tag1 = new DistributedContextEntry(K1, V1);
            var tag2 = new DistributedContextEntry(K1, V2);
            ITagContext tagContextWithDuplicateTags = new SimpleTagContext(tag1, tag2);
            var result = GetResultOfGetCurrentTagContext(tagContextWithDuplicateTags);
            Assert.Equal(new List<DistributedContextEntry>() { tag2 }, TagsTestUtil.TagContextToList(result));
        }

        [Fact]
        public void GetCurrentTagContext_SkipNullTag()
        {
            ITagContext tagContextWithNullTag = new SimpleTagContext(TAG1, null, TAG2);
            var result = GetResultOfGetCurrentTagContext(tagContextWithNullTag);
            Assert.Equal(new List<DistributedContextEntry>() { TAG1, TAG2 }, TagsTestUtil.TagContextToList(result));
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
            var orig = AsyncLocalContext.CurrentTagContext;
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
            var result = GetResultOfWithTagContext(unknownTagContext);
            Assert.IsType<TagContext>(result);
            Assert.Equal(new List<DistributedContextEntry>() { TAG1, TAG2, TAG3 }, TagsTestUtil.TagContextToList(result));
        }

        [Fact]
        public void WithTagContext_RemoveDuplicatesFromUnknownTagContext()
        {
            var tag1 = new DistributedContextEntry(K1, V1);
            var tag2 = new DistributedContextEntry(K1, V2);
            ITagContext tagContextWithDuplicateTags = new SimpleTagContext(tag1, tag2);
            var result = GetResultOfWithTagContext(tagContextWithDuplicateTags);
            Assert.Equal(new List<DistributedContextEntry>() { tag2 }, TagsTestUtil.TagContextToList(result));
        }

        [Fact]
        public void WithTagContext_SkipNullTag()
        {
            ITagContext tagContextWithNullTag = new SimpleTagContext(TAG1, null, TAG2);
            var result = GetResultOfWithTagContext(tagContextWithNullTag);
            Assert.Equal(new List<DistributedContextEntry>() { TAG1, TAG2 }, TagsTestUtil.TagContextToList(result));
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
            var scope = tagger.WithTagContext(tagsToSet);
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
            private readonly List<DistributedContextEntry> tags;

            public SimpleTagContext(params DistributedContextEntry[] tags)
            {
                this.tags = new List<DistributedContextEntry>(tags);
            }

            public override IEnumerator<DistributedContextEntry> GetEnumerator()
            {
                return tags.GetEnumerator();
            }
        }
    }
}
