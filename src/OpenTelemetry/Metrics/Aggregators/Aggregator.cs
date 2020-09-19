// <copyright file="Aggregator.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
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

using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics.Aggregators
{
    /// <summary>
    /// Aggregator base class.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    public abstract class Aggregator<T>
        where T : struct
    {
        /// <summary>
        /// Adds value to the running total in a thread safe manner.
        /// </summary>
        /// <param name="value">Value to be aggregated.</param>
        public abstract void Update(T value);

        /// <summary>
        /// Checkpoints the current aggregate data, and resets the state.
        /// </summary>
        public abstract void Checkpoint();

        /// <summary>
        /// Convert Aggregator data to MetricData.
        /// </summary>
        /// <returns>An instance of <see cref="MetricData"/> representing the currently aggregated value.</returns>
        public abstract MetricData ToMetricData();

        /// <summary>
        /// Get Aggregation Type.
        /// </summary>
        /// <returns><see cref="AggregationType"/>.</returns>
        public abstract AggregationType GetAggregationType();
    }
}
