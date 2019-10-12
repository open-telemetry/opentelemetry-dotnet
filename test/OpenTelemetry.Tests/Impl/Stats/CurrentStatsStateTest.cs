// <copyright file="CurrentStatsStateTest.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace OpenTelemetry.Stats.Test
{
    public class CurrentStatsStateTest
    {
        [Fact]
        public void DefaultState()
        {
            Assert.Equal(StatsCollectionState.ENABLED, new CurrentStatsState().Value);
        }

        [Fact]
        public void SetState()
        {
            var state = new CurrentStatsState();
            Assert.True(state.Set(StatsCollectionState.DISABLED));
            Assert.Equal(StatsCollectionState.DISABLED, state.Internal);
            Assert.True(state.Set(StatsCollectionState.ENABLED));
            Assert.Equal(StatsCollectionState.ENABLED, state.Internal);
            Assert.False(state.Set(StatsCollectionState.ENABLED));
        }



        [Fact]
        public void PreventSettingStateAfterReadingState()
        {
            var state = new CurrentStatsState();
            var st = state.Value;
            Assert.Throws<ArgumentException>(() => state.Set(StatsCollectionState.DISABLED));
        }

        [Fact]

        public async Task PreventSettingStateAfterReadingState_IsThreadSafe()
        {
            // This test relies on timing, and as such may not FAIL reliably under some conditions
            // (e.g. more/less machine load, faster/slower processors).
            // It will not incorrectly fail transiently though.

            for (var i = 0; i < 10; i ++)
            {
                var state = new CurrentStatsState();

                using (var cts = new CancellationTokenSource())
                {
                    var _ = Task.Run(
                        () =>
                        {
                            while (!cts.IsCancellationRequested)
                            {
                                try
                                {
                                    state.Set(StatsCollectionState.DISABLED);
                                    state.Set(StatsCollectionState.ENABLED);
                                }
                                catch
                                {
                                    // Throw is expected after the read is performed
                                }
                            }
                        },
                        cts.Token);

                    await Task.Delay(10);

                    // Read the value a bunch of times
                    var values = Enumerable.Range(0, 20)
                        .Select(__ => state.Value)
                        .ToList();

                    // They should all be the same
                    foreach (var item in values)
                    {
                        Assert.Equal(item, values[0]);
                    }

                    cts.Cancel();
                }
            }
        }
    }
}
