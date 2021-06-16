// <copyright file="MetricAggregatorType.cs" company="OpenTelemetry Authors">
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
    public enum MetricAggregatorType
    {
        /// <summary>
        /// None.
        /// </summary>
        NONE,

        /// <summary>
        /// Gauge.
        /// </summary>
        GAUGE,

        /// <summary>
        /// Sum (cummulative, non-monotonic).
        /// </summary>
        SUM,

        /// <summary>
        /// Sum (cummulative, monotonic).
        /// </summary>
        SUM_MONOTONIC,

        /// <summary>
        /// Sum (delta, non-monotonic).
        /// </summary>
        SUM_DELTA,

        /// <summary>
        /// Sum (delta, monotonic).
        /// </summary>
        SUM_DELTA_MONOTONIC,

        /// <summary>
        /// Summary (non-monotonic).
        /// </summary>
        SUMMARY,

        /// <summary>
        /// Summary (monotonic).
        /// </summary>
        SUMMARY_MONOTONIC,

        /// <summary>
        /// Histogram (cummulative).
        /// </summary>
        HISTOGRAM,

        /// <summary>
        /// Histogram (delta).
        /// </summary>
        HISTOGRAM_DELTA,
    }
}
