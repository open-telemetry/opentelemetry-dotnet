// <copyright file="NoopMeasureMap.cs" company="OpenCensus Authors">
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
    using OpenCensus.Stats.Measures;
    using OpenCensus.Tags;

    internal sealed class NoopMeasureMap : MeasureMapBase
    {
        internal static readonly NoopMeasureMap Instance = new NoopMeasureMap();

        public override IMeasureMap Put(IMeasureDouble measure, double value)
        {
            return this;
        }

        public override IMeasureMap Put(IMeasureLong measure, long value)
        {
            return this;
        }

        public override void Record()
        {
        }

        public override void Record(ITagContext tags)
        {
           if (tags == null)
            {
                throw new ArgumentNullException(nameof(tags));
            }
        }
    }
}
