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

namespace OpenTelemetry.Metrics
{
    public enum Aggregator
    {
        /// <summary>
        /// None.
        /// </summary>
        NONE,

        /// <summary>
        /// Sum Aggregator (Cummulative, Non-Monotonic).
        /// </summary>
        SUM,

        /// <summary>
        /// Sum Aggregator (Cummulative, Monotonic).
        /// </summary>
        SUM_MONOTONIC,

        /// <summary>
        /// Sum Aggregator (Delta, Non-Monotonic).
        /// </summary>
        SUM_DELTA,

        /// <summary>
        /// Sum Aggregator (Delta, Monotonic).
        /// </summary>
        SUM_DELTA_MONOTONIC,

        /// <summary>
        /// Gauge Aggregator.
        /// </summary>
        GAUGE,

        /// <summary>
        /// Summary Aggregator.
        /// </summary>
        SUMMARY,

        /// <summary>
        /// Histogram Aggregator (Cummulative).
        /// AggregatorParam: double[] // optional explicit bounds.
        /// </summary>
        HISTOGRAM,

        /// <summary>
        /// Histogram Aggregator (Delta).
        /// AggregatorParam: double[] // optional explicit bounds.
        /// </summary>
        HISTOGRAM_DELTA,
    }
}
