// <copyright file="MeasureMapBuilder.cs" company="OpenCensus Authors">
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
    using System.Collections.Generic;
    using OpenCensus.Stats.Measurements;
    using OpenCensus.Stats.Measures;

    internal class MeasureMapBuilder
    {
        private readonly List<IMeasurement> measurements = new List<IMeasurement>();

        internal static MeasureMapBuilder Builder()
        {
            return new MeasureMapBuilder();
        }

        internal MeasureMapBuilder Put(IMeasureDouble measure, double value)
        {
            this.measurements.Add(MeasurementDouble.Create(measure, value));
            return this;
        }

        internal MeasureMapBuilder Put(IMeasureLong measure, long value)
        {
            this.measurements.Add(MeasurementLong.Create(measure, value));
            return this;
        }

        internal IEnumerable<IMeasurement> Build()
        {
            // Note: this makes adding measurements quadratic but is fastest for the sizes of
            // MeasureMapInternals that we should see. We may want to go to a strategy of sort/eliminate
            // for larger MeasureMapInternals.
            for (int i = this.measurements.Count - 1; i >= 0; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if (this.measurements[i].Measure == this.measurements[j].Measure)
                    {
                        this.measurements.RemoveAt(j);
                        j--;
                    }
                }
            }

            return new List<IMeasurement>(this.measurements);
        }
    }
}
