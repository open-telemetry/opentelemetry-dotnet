﻿// <copyright file="TagsTest.cs" company="OpenTelemetry Authors">
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
    using OpenTelemetry.Tags.Propagation;
    using Xunit;

    public class TagsTest
    {
        public TagsTest()
        {
            Tags.Initialize(true);
        }

        [Fact]
        public void GetTagger()
        {
            Assert.Equal(typeof(Tagger), Tags.Tagger.GetType());
        }

        [Fact]
        public void GetTagContextSerializer()
        {
            Assert.Equal(typeof(TagPropagationComponent), Tags.TagPropagationComponent.GetType());
        }
    }
}
