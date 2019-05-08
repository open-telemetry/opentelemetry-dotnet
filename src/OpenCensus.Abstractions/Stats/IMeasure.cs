// <copyright file="IMeasure.cs" company="OpenCensus Authors">
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

    /// <summary>
    /// A single measure to track.
    /// </summary>
    public interface IMeasure
    {
        /// <summary>
        /// Gets the name of the measure.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the description of the measure.
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Gets the unit of the measure.
        /// </summary>
        string Unit { get; }

        /// <summary>
        /// Execute callback with the specific measure type without type casting.
        /// </summary>
        /// <typeparam name="T">The result type of a callback.</typeparam>
        /// <param name="p0">Callback to be called for the double measure.</param>
        /// <param name="p1">Callback to be called for the long measure.</param>
        /// <param name="defaultFunction">Callback to be called for any other measure.</param>
        /// <returns>The result of measure type specific callback execution.</returns>
        T Match<T>(
            Func<IMeasureDouble, T> p0,
            Func<IMeasureLong, T> p1,
            Func<IMeasure, T> defaultFunction);
    }
}
