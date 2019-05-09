// <copyright file="StatsComponentTest.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Stats.Test
{
    using OpenCensus.Internal;
    using Xunit;

    public class StatsComponentTest
    {
        private readonly StatsComponent statsComponent = new StatsComponent(new SimpleEventQueue());

        [Fact]
        public void DefaultState()
        {
            Assert.Equal(StatsCollectionState.ENABLED, statsComponent.State);
        }

        // [Fact]
        // public void setState_Disabled()
        // {
        //    statsComponent.setState(StatsCollectionState.DISABLED);
        //    assertThat(statsComponent.getState()).isEqualTo(StatsCollectionState.DISABLED);
        // }

        // [Fact]
        // public void setState_Enabled()
        // {
        //    statsComponent.setState(StatsCollectionState.DISABLED);
        //    statsComponent.setState(StatsCollectionState.ENABLED);
        //    assertThat(statsComponent.getState()).isEqualTo(StatsCollectionState.ENABLED);
        // }

        // [Fact]
        // public void setState_DisallowsNull()
        // {
        //    thrown.expect(NullPointerException);
        //    thrown.expectMessage("newState");
        //    statsComponent.setState(null);
        // }

        // [Fact]
        // public void preventSettingStateAfterGettingState()
        // {
        //    statsComponent.setState(StatsCollectionState.DISABLED);
        //    statsComponent.getState();
        //    thrown.expect(IllegalStateException);
        //    thrown.expectMessage("State was already read, cannot set state.");
        //    statsComponent.setState(StatsCollectionState.ENABLED);
        // }
    }
}
