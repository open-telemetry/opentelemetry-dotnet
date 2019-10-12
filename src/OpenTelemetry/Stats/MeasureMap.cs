﻿// <copyright file="MeasureMap.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Stats.Measures;
using OpenTelemetry.Tags;
using OpenTelemetry.Tags.Unsafe;

namespace OpenTelemetry.Stats
{
    internal sealed class MeasureMap : MeasureMapBase
    {
        private readonly StatsManager statsManager;
        private readonly MeasureMapBuilder builder = MeasureMapBuilder.Builder();

        internal MeasureMap(StatsManager statsManager)
        {
            this.statsManager = statsManager;
        }

        public override IMeasureMap Put(IMeasureDouble measure, double value)
        {
            this.builder.Put(measure, value);
            return this;
        }

        public override IMeasureMap Put(IMeasureLong measure, long value)
        {
            this.builder.Put(measure, value);
            return this;
        }

        public override void Record()
        {
            // Use the context key directly, to avoid depending on the tags implementation.
            this.Record(AsyncLocalContext.CurrentTagContext);
        }

        public override void Record(ITagContext tags)
        {
            this.statsManager.Record(tags, this.builder.Build());
        }

        internal static IMeasureMap Create(StatsManager statsManager)
        {
            return new MeasureMap(statsManager);
        }
    }
}
