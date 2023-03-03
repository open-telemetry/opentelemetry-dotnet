// <copyright file="AggregationType.cs" company="OpenTelemetry Authors">
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
    internal enum AggregationType
    {
        /// <summary>
        /// Invalid.
        /// </summary>
        Invalid = -1,

        /// <summary>
        /// Calculate SUM from incoming delta measurements.
        /// </summary>
        LongSumIncomingDelta = 0,

        /// <summary>
        /// Calculate SUM from incoming cumulative measurements.
        /// </summary>
        LongSumIncomingCumulative = 1,

        /// <summary>
        /// Calculate SUM from incoming delta measurements.
        /// </summary>
        DoubleSumIncomingDelta = 2,

        /// <summary>
        /// Calculate SUM from incoming cumulative measurements.
        /// </summary>
        DoubleSumIncomingCumulative = 3,

        /// <summary>
        /// Keep LastValue.
        /// </summary>
        LongGauge = 4,

        /// <summary>
        /// Keep LastValue.
        /// </summary>
        DoubleGauge = 5,

        /// <summary>
        /// Histogram with sum, count, buckets.
        /// </summary>
        HistogramWithBuckets = 6,

        /// <summary>
        /// Histogram with sum, count, min, max, buckets.
        /// </summary>
        HistogramWithMinMaxBuckets = 7,

        /// <summary>
        /// Histogram with sum, count.
        /// </summary>
        Histogram = 8,

        /// <summary>
        /// Histogram with sum, count, min, max.
        /// </summary>
        HistogramWithMinMax = 9,

        /// <summary>
        /// Exponential Histogram with sum, count.
        /// </summary>
        Base2ExponentialHistogram = 10,

        /// <summary>
        /// Exponential Histogram with sum, count, min, max.
        /// </summary>
        Base2ExponentialHistogramWithMinMax = 11,
    }
}
