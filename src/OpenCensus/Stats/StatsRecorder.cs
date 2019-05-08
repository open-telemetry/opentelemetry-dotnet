// <copyright file="StatsRecorder.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Stats
{
    using System;

    public sealed class StatsRecorder : StatsRecorderBase
    {
        private readonly StatsManager statsManager;

        internal StatsRecorder(StatsManager statsManager)
        {
            this.statsManager = statsManager ?? throw new ArgumentNullException(nameof(statsManager));
        }

        public override IMeasureMap NewMeasureMap()
        {
            return MeasureMap.Create(this.statsManager);
        }
    }
}
