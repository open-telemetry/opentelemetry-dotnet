// <copyright file="StatsDefaultTest.cs" company="OpenCensus Authors">
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
    using Xunit;

    public class StatsDefaultTest
    {
        // [Fact]
        // public void loadStatsManager_UsesProvidedClassLoader()
        //      {
        //          final RuntimeException toThrow = new RuntimeException("UseClassLoader");
        //          thrown.expect(RuntimeException.class);
        //  thrown.expectMessage("UseClassLoader");
        //  Stats.loadStatsComponent(
        //      new ClassLoader()
        //      {
        //          @Override
        //        public Class<?> loadClass(String name)
        //          {
        //              throw toThrow;
        //          }
        //      });
        // }

        // [Fact]
        //  public void loadStatsManager_IgnoresMissingClasses()
        //    {
        //        ClassLoader classLoader =
        //            new ClassLoader() {
        //          @Override
        //              public Class<?> loadClass(String name) throws ClassNotFoundException {
        //            throw new ClassNotFoundException();
        //        }
        //    };

        // assertThat(Stats.loadStatsComponent(classLoader).getClass().getName())
        //        .isEqualTo("io.opencensus.stats.NoopStats$NoopStatsComponent");
        // }

        [Fact(Skip = "Fix later when default will be changed back")]
        public void DefaultValues()
        {
            Assert.Equal(NoopStats.NoopStatsRecorder, Stats.StatsRecorder);
            Assert.Equal(NoopStats.NewNoopViewManager().GetType(), Stats.ViewManager.GetType());
        }

        [Fact(Skip = "Fix later when default will be changed back")]
        public void GetState()
        {
            Assert.Equal(StatsCollectionState.DISABLED, Stats.State);
        }

    }
}
