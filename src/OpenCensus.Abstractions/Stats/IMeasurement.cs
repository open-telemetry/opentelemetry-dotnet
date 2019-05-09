// <copyright file="IMeasurement.cs" company="OpenCensus Authors">
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
    using OpenCensus.Stats.Measurements;

    /// <summary>
    /// Represents a single measurement for a measure.
    /// </summary>
    public interface IMeasurement
    {
        /// <summary>
        /// Gets the measure this measurement recorded for.
        /// </summary>
        IMeasure Measure { get; }

        /// <summary>
        /// Calls the measure-type specific callback without type casting of a measurement.
        /// </summary>
        /// <typeparam name="T">Result type of the callback.</typeparam>
        /// <param name="p0">Callback to be called for double measure.</param>
        /// <param name="p1">Callback to be called for long measure.</param>
        /// <param name="defaultFunction">Callback to be called for all other measures.</param>
        /// <returns>The result of measure type specific callback execution.</returns>
        T Match<T>(Func<IMeasurementDouble, T> p0, Func<IMeasurementLong, T> p1, Func<IMeasurement, T> defaultFunction);
    }
}
