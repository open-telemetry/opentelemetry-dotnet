// <copyright file="CurrentTagContextUtilsTest.cs" company="OpenCensus Authors">
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

    public class CurrentTagContextUtilsTest
    {
        private static readonly ITag TAG = Tag.Create(TagKey.Create("key"), TagValue.Create("value"));

        private readonly ITagContext tagContext = new TestTagContext();

        [Fact]
        public void TestGetCurrentTagContext_DefaultContext()
        {
            ITagContext tags = CurrentTagContextUtils.CurrentTagContext;
            Assert.NotNull(tags);
            Assert.Empty(TagsTestUtil.TagContextToList(tags));
        }

        [Fact]
        public void TestGetCurrentTagContext_ContextSetToNull()
        {
            var orig = AsyncLocalContext.CurrentTagContext;
            AsyncLocalContext.CurrentTagContext = null;
            try
            {
                ITagContext tags = CurrentTagContextUtils.CurrentTagContext;
                Assert.NotNull(tags);
                Assert.Empty(TagsTestUtil.TagContextToList(tags));
            }
            finally
            {
                AsyncLocalContext.CurrentTagContext = orig;
            }
        }

        [Fact]
        public void TestWithTagContext()
        {
            Assert.Empty(TagsTestUtil.TagContextToList(CurrentTagContextUtils.CurrentTagContext));

            IScope scopedTags = CurrentTagContextUtils.WithTagContext(tagContext);
            try
            {
                Assert.Same(tagContext, CurrentTagContextUtils.CurrentTagContext);
            }
            finally
            {
                scopedTags.Dispose();
            }
            Assert.Empty(TagsTestUtil.TagContextToList(CurrentTagContextUtils.CurrentTagContext));
        }

        [Fact]
        public void TestWithTagContextUsingWrap()
        {
            // Runnable runnable;
            //        Scope scopedTags = CurrentTagContextUtils.withTagContext(tagContext);
            //        try
            //        {
            //            assertThat(CurrentTagContextUtils.getCurrentTagContext()).isSameAs(tagContext);
            //            runnable =
            //                Context.current()
            //                    .wrap(
            //                        new Runnable() {
            //                @Override
            //                          public void run()
            //            {
            //                assertThat(CurrentTagContextUtils.getCurrentTagContext())
            //                    .isSameAs(tagContext);
            //            }
            //        });
            //    } finally {
            //  scopedTags.close();
            // }
            // assertThat(tagContextToList(CurrentTagContextUtils.getCurrentTagContext())).isEmpty();
            //// When we run the runnable we will have the TagContext in the current Context.
            // runnable.run();
        }

        class TestTagContext : TagContextBase
        {
            public TestTagContext()
            {

            }

            public override IEnumerator<ITag> GetEnumerator()
            {
                return new List<ITag>() { TAG }.GetEnumerator();
            }
        }
    }
}
