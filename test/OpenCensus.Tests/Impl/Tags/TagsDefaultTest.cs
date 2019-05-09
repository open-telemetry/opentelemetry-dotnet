// <copyright file="TagsDefaultTest.cs" company="OpenCensus Authors">
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
    using Xunit;

    public class TagsDefaultTest
    {
        [Fact(Skip = "Fix statics usage")]
        public void TestState()
        {
            // Test that setState ignores its input.
            // Tags.setState(TaggingState.ENABLED);
            Assert.Equal(TaggingState.DISABLED, Tags.State);

            // Test that setState cannot be called after getState.
            // thrown.expect(IllegalStateException);
            // thrown.expectMessage("State was already read, cannot set state.");
            // Tags.setState(TaggingState.ENABLED);
        }

        [Fact(Skip = "Fix statics usage")]
        public void DefaultTagger()
        {
            Assert.Equal(NoopTags.NoopTagger, Tags.Tagger);
        }

        [Fact(Skip = "Fix statics usage")]
        public void DefaultTagContextSerializer()
        {
            Assert.Equal(NoopTags.NoopTagPropagationComponent, Tags.TagPropagationComponent);
        }

    }
}
