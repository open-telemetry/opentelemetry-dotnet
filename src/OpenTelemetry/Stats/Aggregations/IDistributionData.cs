// <copyright file="IDistributionData.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Stats.Aggregations
{
    /// <summary>
    /// Data accumulated by distributed aggregation.
    /// </summary>
    public interface IDistributionData : IAggregationData
    {
        /// <summary>
        /// Gets the mean of values.
        /// </summary>
        double Mean { get; }

        /// <summary>
        /// Gets the number of samples.
        /// </summary>
        long Count { get; }

        /// <summary>
        /// Gets the minimum of values.
        /// </summary>
        double Min { get; }

        /// <summary>
        /// Gets the maximum of values.
        /// </summary>
        double Max { get; }

        /// <summary>
        /// Gets the sum of squares of values.
        /// </summary>
        double SumOfSquaredDeviations { get; }

        /// <summary>
        /// Gets the counts in buckets.
        /// </summary>
        IReadOnlyList<long> BucketCounts { get; }
    }
}
