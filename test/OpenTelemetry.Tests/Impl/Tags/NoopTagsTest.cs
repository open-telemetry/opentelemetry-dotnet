// <copyright file="NoopTagsTest.cs" company="OpenTelemetry Authors">
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace OpenTelemetry.Tags.Test
{
    public class NoopTagsTest
    {
        private static readonly string KEY = "key";
        private static readonly string VALUE = "value";

        private static readonly ITagContext TAG_CONTEXT = new TestTagContext();

        [Fact]
        public void NoopTagger()
        {
            var noopTagger = NoopTags.NoopTagger;
            Assert.Same(NoopTags.NoopTagContext, noopTagger.Empty);
            Assert.Same(NoopTags.NoopTagContext, noopTagger.CurrentTagContext);
            Assert.Same(NoopTags.NoopTagContextBuilder, noopTagger.EmptyBuilder);
            Assert.Same(NoopTags.NoopTagContextBuilder, noopTagger.ToBuilder(TAG_CONTEXT));
            Assert.Same(NoopTags.NoopTagContextBuilder, noopTagger.CurrentBuilder);

            using (var noopScope = noopTagger.WithTagContext(TAG_CONTEXT))
            {
                Assert.NotNull(noopScope);
            }
            // does not throw
        }

        [Fact]
        public void NoopTagger_ToBuilder_DisallowsNull()
        {
            var noopTagger = NoopTags.NoopTagger;
            Assert.Throws<ArgumentNullException>(() => noopTagger.ToBuilder(null));
        }

        [Fact]
        public void NoopTagger_WithTagContext_DisallowsNull()
        {
            var noopTagger = NoopTags.NoopTagger;
            Assert.Throws<ArgumentNullException>(() => noopTagger.WithTagContext(null));
        }

        [Fact]
        public void NoopTagContextBuilder()
        {
            Assert.Same(NoopTags.NoopTagContext, NoopTags.NoopTagContextBuilder.Build());
            Assert.Same(NoopTags.NoopTagContext, NoopTags.NoopTagContextBuilder.Put(KEY, VALUE).Build());

            var noopScope = NoopTags.NoopTagContextBuilder.BuildScoped();
            Assert.NotNull(noopScope);
            // does not throw
            noopScope.Dispose();

            noopScope = NoopTags.NoopTagContextBuilder.Put(KEY, VALUE).BuildScoped();
            Assert.NotNull(noopScope);
            // does not throw
            noopScope.Dispose();
        }

        [Fact]
        public void NoopTagContextBuilder_Put_DisallowsNullKey()
        {
            var noopBuilder = NoopTags.NoopTagContextBuilder;
            Assert.Throws<ArgumentNullException>(() => noopBuilder.Put(null, VALUE));
        }

        [Fact]
        public void NoopTagContextBuilder_Put_DisallowsNullValue()
        {
            var noopBuilder = NoopTags.NoopTagContextBuilder;
            Assert.Throws<ArgumentNullException>(() => noopBuilder.Put(KEY, null));
        }

        [Fact]
        public void NoopTagContextBuilder_Remove_DisallowsNullKey()
        {
            var noopBuilder = NoopTags.NoopTagContextBuilder;
            Assert.Throws<ArgumentNullException>(() => noopBuilder.Remove(null));
        }

        [Fact]
        public void NoopTagContext()
        {
            Assert.Empty(NoopTags.NoopTagContext.ToList());
        }

        [Fact]
        public void NoopTagContextBinarySerializer()
        {
            Assert.Equal(new byte[0], NoopTags.NoopTagContextBinarySerializer.ToByteArray(TAG_CONTEXT));
            Assert.Equal(NoopTags.NoopTagContext, NoopTags.NoopTagContextBinarySerializer.FromByteArray(new byte[5]));
        }

        [Fact]
        public void NoopTagContextBinarySerializer_ToByteArray_DisallowsNull()
        {
            var noopSerializer = NoopTags.NoopTagContextBinarySerializer;
            Assert.Throws<ArgumentNullException>(() => noopSerializer.ToByteArray(null));
        }

        [Fact]
        public void NoopTagContextBinarySerializer_FromByteArray_DisallowsNull()
        {
            var noopSerializer = NoopTags.NoopTagContextBinarySerializer;
            Assert.Throws<ArgumentNullException>(() => noopSerializer.FromByteArray(null));
        }

        class TestTagContext : ITagContext
        {
            public IEnumerator<Tag> GetEnumerator()
            {
                var list = new List<Tag>() { Tag.Create(KEY, VALUE) };
                return list.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
