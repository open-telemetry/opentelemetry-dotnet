// <copyright file="CurrentTaggingStateTest.cs" company="OpenTelemetry Authors">
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
    using System;
    using Xunit;

    public class CurrentTaggingStateTest
    {
        [Fact]
        public void DefaultState()
        {
            Assert.Equal(TaggingState.ENABLED, new CurrentTaggingState().Value);
        }

        [Fact]
        public void SetState()
        {
            var state = new CurrentTaggingState();
            state.Set(TaggingState.DISABLED);
            Assert.Equal(TaggingState.DISABLED, state.Internal);
            state.Set(TaggingState.ENABLED);
            Assert.Equal(TaggingState.ENABLED, state.Internal);
        }


        [Fact]
        public void PreventSettingStateAfterReadingState()
        {
            var state = new CurrentTaggingState();
            var current = state.Value;
            Assert.Throws<InvalidOperationException>(() => state.Set(TaggingState.DISABLED));
        }
    }
}
