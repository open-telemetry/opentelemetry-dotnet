// <copyright file="Stats.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Stats
{
    using OpenTelemetry.Internal;

    public class Stats
    {
        private static readonly Stats StatsValue = new Stats();

        private readonly CurrentStatsState state = new CurrentStatsState();
        private readonly StatsManager statsManager;
        private readonly IViewManager viewManager;
        private readonly IStatsRecorder statsRecorder;

        internal Stats()
        {
            this.statsManager = new StatsManager(new SimpleEventQueue(), this.state);
            this.viewManager = new ViewManager(this.statsManager);
            this.statsRecorder = new StatsRecorder(this.statsManager);
        }

        public static IStatsRecorder StatsRecorder
        {
            get
            {
                return StatsValue.statsRecorder;
            }
        }

        public static IViewManager ViewManager
        {
            get
            {
                return StatsValue.viewManager;
            }
        }

        public static StatsCollectionState State
        {
            get
            {
                return StatsValue.state.Value;
            }

            set
            {
                if (!(StatsValue.viewManager is ViewManager manager))
                {
                    return;
                }

                var result = StatsValue.state.Set(value);
                if (result)
                {
                    if (value == StatsCollectionState.DISABLED)
                    {
                        manager.ClearStats();
                    }
                    else
                    {
                        manager.ResumeStatsCollection();
                    }
                }
            }
        }
    }
}
