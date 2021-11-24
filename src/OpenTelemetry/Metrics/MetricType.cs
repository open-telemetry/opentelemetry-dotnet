// <copyright file="MetricType.cs" company="OpenTelemetry Authors">
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

using System;

namespace OpenTelemetry.Metrics
{
    public enum MetricType : short
    {
        /// <summary>
        /// Sum of Long type.
        /// </summary>
        LongSum = 1,

        /// <summary>
        /// Sum of Double type.
        /// </summary>
        DoubleSum = 2,

        /// <summary>
        /// Gauge of Long type.
        /// </summary>
        LongGauge = 3,

        /// <summary>
        /// Gauge of Double type.
        /// </summary>
        DoubleGauge = 4,

        /// <summary>
        /// Histogram. (Sum and Count).
        /// </summary>
        Histogram = 5,

        /// <summary>
        /// Histogram with Min and Max.
        /// </summary>
        HistogramWithMinMax = 6,

        /// <summary>
        /// Histogram with Buckets.
        /// </summary>
        HistogramWithBuckets = 7,

        /// <summary>
        /// Histogram with Min, Max and Buckets.
        /// </summary>
        HistogramWithMinMaxAndBuckets = 8,
    }
}
