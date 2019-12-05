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
        /// Creates Int64 counter with given name.
        /// </summary>
        /// <param name="name">The name of the counter.</param>
        /// <param name="monotonic">indicates if only positive values are expected.</param>
        /// <returns>The counter instance.</returns>
        public Counter<long> CreateInt64Counter(string name, bool monotonic = true) => this.CreateCounter<long>(name, monotonic);

        /// <summary>
        /// Creates double counter with given name.
        /// </summary>
        /// <param name="name">indicates if only positive values are expected.</param>
        /// <param name="monotonic">The name of the counter.</param>
        /// <returns>The counter instance.</returns>
        public Counter<double> CreateDoubleCounter(string name, bool monotonic = true) => this.CreateCounter<double>(name, monotonic);

        /// <summary>
        /// Creates Int64 Gauge with given name.
        /// </summary>
        /// <param name="name">The name of the counter.</param>
        /// <param name="monotonic">indicates if only positive values are expected.</param>
        /// <returns>The Gauge instance.</returns>
        public Gauge<long> CreateInt64Gauge(string name, bool monotonic = false) => this.CreateGauge<long>(name, monotonic);

        /// <summary>
        /// Creates Int64 Measure with given name.
        /// </summary>
        /// <param name="name">The name of the measure.</param>
        /// <param name="absolute">indicates if only positive values are expected.</param>
        /// <returns>The measure instance.</returns>
        public Measure<long> CreateInt64Measure(string name, bool absolute = true) => this.CreateMeasure<long>(name, absolute);

        /// <summary>
        /// Creates double Measure with given name.
        /// </summary>
        /// <param name="name">The name of the measure.</param>
        /// <param name="absolute">indicates if only positive values are expected.</param>
        /// <returns>The measure instance.</returns>
        public Measure<double> CreateDoubleMeasure(string name, bool absolute = true) => this.CreateMeasure<double>(name, absolute);

        /// <summary>
        /// Creates double Gauge with given name.
        /// </summary>
        /// <param name="name">The name of the counter.</param>
        /// <param name="monotonic">indicates if only positive values are expected.</param>
        /// <returns>The Gauge instance.</returns>
        public Gauge<double> CreateDoubleGauge(string name, bool monotonic = false) => this.CreateGauge<double>(name, monotonic);

        /// <summary>
        /// Constructs or retrieves the <see cref="LabelSet"/> from the given label key-value pairs.
        /// </summary>
        /// <param name="labels">Label key value pairs.</param>
        /// <returns>The <see cref="LabelSet"/> with given label key value pairs.</returns>
        public abstract LabelSet GetLabelSet(IEnumerable<KeyValuePair<string, string>> labels);

        /// <summary>
        /// Creates double or Int64 counter with given name.
        /// </summary>
        /// <param name="name">indicates if only positive values are expected.</param>
        /// <param name="monotonic">The name of the counter.</param>
        /// <typeparam name="T">The element type of the counter. Should be either long or double.</typeparam>
        /// <returns>The counter instance.</returns>
        protected abstract Counter<T> CreateCounter<T>(string name, bool monotonic = true)
            where T : struct;

        /// <summary>
        /// Creates double or Int64 Gauge with given name.
        /// </summary>
        /// <param name="name">The name of the counter.</param>
        /// <param name="monotonic">indicates if only positive values are expected.</param>
        /// <typeparam name="T">The element type of the Gauge. Should be either long or double.</typeparam>
        /// <returns>The Gauge instance.</returns>
        protected abstract Gauge<T> CreateGauge<T>(string name, bool monotonic = false)
            where T : struct;

        /// <summary>
        /// Creates double or Int64 Measure with given name.
        /// </summary>
        /// <param name="name">The name of the measure.</param>
        /// <param name="absolute">indicates if only positive values are expected.</param>
        /// <typeparam name="T">The element type of the Measure. Should be either long or double.</typeparam>
        /// <returns>The measure instance.</returns>
        protected abstract Measure<T> CreateMeasure<T>(string name, bool absolute = true)
            where T : struct;
    }
}
