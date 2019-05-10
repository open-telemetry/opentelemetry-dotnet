// <copyright file="StatsComponent.cs" company="OpenTelemetry Authors">
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

    public class StatsComponent : StatsComponentBase
    {
        // The StatsCollectionState shared between the StatsComponent, StatsRecorder and ViewManager.
        private readonly CurrentStatsState state = new CurrentStatsState();

        private readonly IViewManager viewManager;
        private readonly IStatsRecorder statsRecorder;

        public StatsComponent()
            : this(new SimpleEventQueue())
        {
        }

        public StatsComponent(IEventQueue queue)
        {
            StatsManager statsManager = new StatsManager(queue, this.state);
            this.viewManager = new ViewManager(statsManager);
            this.statsRecorder = new StatsRecorder(statsManager);
        }

        public override IViewManager ViewManager
        {
            get { return this.viewManager; }
        }

        public override IStatsRecorder StatsRecorder
        {
            get { return this.statsRecorder; }
        }

        public override StatsCollectionState State
        {
            get
            {
                return this.state.Value;
            }

            set
            {
                if (!(this.viewManager is ViewManager manager))
                {
                    return;
                }

                var result = this.state.Set(value);
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
