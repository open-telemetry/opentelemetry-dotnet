// <copyright file="Meter.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Main interface to obtain metric instruments.
    /// </summary>
    public abstract class Meter
    {
        /// <summary>
        /// Creates a counter for long with given name.
        /// </summary>
        /// <param name="name">The name of the counter.</param>
        /// <param name="monotonic">indicates if only positive values are expected.</param>
        /// <returns>The counter instance.</returns>
        public abstract Counter<long> CreateLongCounter(string name, bool monotonic = true);

        /// <summary>
        /// Creates a counter for double with given name.
        /// </summary>
        /// <param name="name">indicates if only positive values are expected.</param>
        /// <param name="monotonic">The name of the counter.</param>
        /// <returns>The counter instance.</returns>
        public abstract Counter<double> CreateDoubleCounter(string name, bool monotonic = true);

        /// <summary>
        /// Creates a guage for long with given name.
        /// </summary>
        /// <param name="name">The name of the counter.</param>
        /// <param name="monotonic">indicates if only positive values are expected.</param>
        /// <returns>The guage instance.</returns>
        public abstract Guage<long> CreateLongGauge(string name, bool monotonic = false);

        /// <summary>
        /// Creates a guage for long with given name.
        /// </summary>
        /// <param name="name">The name of the counter.</param>
        /// <param name="monotonic">indicates if only positive values are expected.</param>
        /// <returns>The guage instance.</returns>
        public abstract Guage<double> CreateDoubleGauge(string name, bool monotonic = false);

        /// <summary>
        /// Creates a measure for long with given name.
        /// </summary>
        /// <param name="name">The name of the measure.</param>
        /// <param name="absolute">indicates if only positive values are expected.</param>
        /// <returns>The measure instance.</returns>
        public abstract Measure<long> CreateLongMeasure(string name, bool absolute = true);

        /// <summary>
        /// Creates a measure for long with given name.
        /// </summary>
        /// <param name="name">The name of the measure.</param>
        /// <param name="absolute">indicates if only positive values are expected.</param>
        /// <returns>The measure instance.</returns>
        public abstract Measure<double> CreateDoubleMeasure(string name, bool absolute = true);

        /// <summary>
        /// Constructs or retrieves the <see cref="LabelSet"/> from the given label key-value pairs.
        /// </summary>
        /// <param name="labels">Label key value pairs.</param>
        /// <returns>The <see cref="LabelSet"/> with given label key value pairs.</returns>
        public abstract LabelSet GetLabelSet(IEnumerable<KeyValuePair<string, string>> labels);
    }
}
