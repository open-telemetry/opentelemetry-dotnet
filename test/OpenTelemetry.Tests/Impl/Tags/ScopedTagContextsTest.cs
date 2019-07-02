// <copyright file="ScopedTagContextsTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Tags.Test
{
    using System.Collections.Generic;
    using OpenTelemetry.Context;
    using Xunit;

    public class ScopedTagContextsTest
    {
        private static readonly TagKey KEY_1 = TagKey.Create("key 1");
        private static readonly TagKey KEY_2 = TagKey.Create("key 2");

        private static readonly TagValue VALUE_1 = TagValue.Create("value 1");
        private static readonly TagValue VALUE_2 = TagValue.Create("value 2");

        private readonly ITagger tagger = new Tagger(new CurrentTaggingState());

        [Fact]
        public void DefaultTagContext()
        {
            var defaultTagContext = tagger.CurrentTagContext;
            Assert.Empty(TagsTestUtil.TagContextToList(defaultTagContext));
            Assert.IsType<TagContext>(defaultTagContext);
        }

        [Fact]
        public void WithTagContext()
        {
            Assert.Empty(TagsTestUtil.TagContextToList(tagger.CurrentTagContext));
            var scopedTags = tagger.EmptyBuilder.Put(KEY_1, VALUE_1).Build();
            var scope = tagger.WithTagContext(scopedTags);
            try
            {
                Assert.Same(scopedTags, tagger.CurrentTagContext);
            }
            finally
            {
                scope.Dispose();
            }
            Assert.Empty(TagsTestUtil.TagContextToList(tagger.CurrentTagContext));
        }

        [Fact]
        public void CreateBuilderFromCurrentTags()
        {
            var scopedTags = tagger.EmptyBuilder.Put(KEY_1, VALUE_1).Build();
            var scope = tagger.WithTagContext(scopedTags);
            try
            {
                var newTags = tagger.CurrentBuilder.Put(KEY_2, VALUE_2).Build();
                Assert.Equal(new List<Tag>() { Tag.Create(KEY_1, VALUE_1), Tag.Create(KEY_2, VALUE_2) },
                    TagsTestUtil.TagContextToList(newTags));
                Assert.Same(scopedTags, tagger.CurrentTagContext);
            }
            finally
            {
                scope.Dispose();
            }
        }

        [Fact]
        public void SetCurrentTagsWithBuilder()
        {
            Assert.Empty(TagsTestUtil.TagContextToList(tagger.CurrentTagContext));
            var scope = tagger.EmptyBuilder.Put(KEY_1, VALUE_1).BuildScoped();
            try
            {
                Assert.Equal(new List<Tag>() { Tag.Create(KEY_1, VALUE_1) }, TagsTestUtil.TagContextToList(tagger.CurrentTagContext));
            }
            finally
            {
                scope.Dispose();
            }
            Assert.Empty(TagsTestUtil.TagContextToList(tagger.CurrentTagContext));
        }

        [Fact]
        public void AddToCurrentTagsWithBuilder()
        {
            var scopedTags = tagger.EmptyBuilder.Put(KEY_1, VALUE_1).Build();
            var scope1 = tagger.WithTagContext(scopedTags);
            try
            {
                var scope2 = tagger.CurrentBuilder.Put(KEY_2, VALUE_2).BuildScoped();
                try
                {
                    Assert.Equal(new List<Tag>() { Tag.Create(KEY_1, VALUE_1), Tag.Create(KEY_2, VALUE_2) },
                        TagsTestUtil.TagContextToList(tagger.CurrentTagContext));
                }
                finally
                {
                    scope2.Dispose();
                }
                Assert.Same(scopedTags, tagger.CurrentTagContext);
            }
            finally
            {
                scope1.Dispose();
            }
        }
    }
}

