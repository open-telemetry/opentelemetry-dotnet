// <copyright file="IMeasureMap.cs" company="OpenCensus Authors">
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
    using OpenCensus.Stats.Measures;
    using OpenCensus.Tags;

    /// <summary>
    /// Measure map. Holds the mapping of measures and values.
    /// </summary>
    public interface IMeasureMap
    {
        /// <summary>
        /// Associates the measure with the given value. Subsequent updates to the same measure will overwrite the previous value.
        /// </summary>
        /// <param name="measure">Measure to associate the value with.</param>
        /// <param name="value">Value to associate with the measure.</param>
        /// <returns>Measure map for calls chaining.</returns>
        IMeasureMap Put(IMeasureDouble measure, double value);

        /// <summary>
        /// Associates the measure with the given value. Subsequent updates to the same measure will overwrite the previous value.
        /// </summary>
        /// <param name="measure">Measure to associate the value with.</param>
        /// <param name="value">Value to associate with the measure.</param>
        /// <returns>Measure map for calls chaining.</returns>
        IMeasureMap Put(IMeasureLong measure, long value);

        /// <summary>
        /// Records all of the measures at the same time with the current tag context.
        /// </summary>
        void Record();

        /// <summary>
        /// Records all of the measures at the same time with the explicit tag context.
        /// </summary>
        /// <param name="tags">Tags to associate with the measure.</param>
        void Record(ITagContext tags);
    }
}
