// <copyright file="NoopStatsTest.cs" company="OpenCensus Authors">
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
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using OpenCensus.Stats.Measures;
    using OpenCensus.Tags;
    using Xunit;

    public class NoopStatsTest
    {
        private static readonly ITag TAG = Tag.Create(TagKey.Create("key"), TagValue.Create("value"));
        private static readonly IMeasureDouble MEASURE =  MeasureDouble.Create("my measure", "description", "s");

        private readonly ITagContext tagContext = new TestTagContext();


        [Fact]
        public void NoopStatsComponent()
        {
            Assert.Same(NoopStats.NoopStatsRecorder, NoopStats.NewNoopStatsComponent().StatsRecorder);
            Assert.Equal(NoopStats.NewNoopViewManager().GetType(), NoopStats.NewNoopStatsComponent().ViewManager.GetType());
        }

        [Fact]
        public void NoopStatsComponent_GetState()
        {
            Assert.Equal(StatsCollectionState.DISABLED, NoopStats.NewNoopStatsComponent().State);
        }

        // [Fact]
        // public void NoopStatsComponent_SetState_IgnoresInput()
        // {
        //    IStatsComponent noopStatsComponent = NoopStats.NewNoopStatsComponent();
        //    noopStatsComponent.State = StatsCollectionState.ENABLED;
        //    assertThat(noopStatsComponent.getState()).isEqualTo(StatsCollectionState.DISABLED);
        // }

        // [Fact]
        // public void NoopStatsComponent_SetState_DisallowsNull()
        // {
        //    StatsComponent noopStatsComponent = NoopStats.newNoopStatsComponent();
        //    thrown.expect(NullPointerException);
        //    noopStatsComponent.setState(null);
        // }

        // [Fact]
        // public void NoopStatsComponent_DisallowsSetStateAfterGetState()
        // {
        //    StatsComponent noopStatsComponent = NoopStats.newNoopStatsComponent();
        //    noopStatsComponent.setState(StatsCollectionState.DISABLED);
        //    noopStatsComponent.getState();
        //    thrown.expect(IllegalStateException);
        //    thrown.expectMessage("State was already read, cannot set state.");
        //    noopStatsComponent.setState(StatsCollectionState.ENABLED);
        // }

        // The NoopStatsRecorder should do nothing, so this test just checks that record doesn't throw an
        // exception.
        [Fact]
        public void NoopStatsRecorder_Record()
        {
            NoopStats.NoopStatsRecorder.NewMeasureMap().Put(MEASURE, 5).Record(tagContext);
        }

        // The NoopStatsRecorder should do nothing, so this test just checks that record doesn't throw an
        // exception.
        [Fact]
        public void NoopStatsRecorder_RecordWithCurrentContext()
        {
            NoopStats.NoopStatsRecorder.NewMeasureMap().Put(MEASURE, 6).Record();
        }

        [Fact]
        public void NoopStatsRecorder_Record_DisallowNullTagContext()
        {
            IMeasureMap measureMap = NoopStats.NoopStatsRecorder.NewMeasureMap();
            Assert.Throws<ArgumentNullException>(() => measureMap.Record(null));
        }

        class TestTagContext : ITagContext
        {
            public IEnumerator<ITag> GetEnumerator()
            {
                var l =  new List<ITag>() { TAG };
                return l.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
