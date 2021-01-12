// <copyright file="DoubleExponentialDistributionOptions.cs" company="OpenTelemetry Authors">
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
    /// <summary>
    /// Exponential distribution options for AggregationType.DoubleDistribution
    ///
    /// Boundaries are determined by the following formula where n = NumberOfFiniteBuckets and i = bucket number:
    /// b = Scale * GrowthFactor ^ i.
    /// </summary>
    public class DoubleExponentialDistributionOptions : AggregationOptions
    {
        /// <summary>
        /// Gets or sets the growth factor.
        /// </summary>
        public double GrowthFactor { get; set; }

        /// <summary>
        /// Gets or sets the scale.
        /// </summary>
        public double Scale { get; set; }

        /// <summary>
        /// Gets or sets the number of finite buckets. The true number of buckets will be this value + 2 accounting for
        /// the underflow and overflow buckets.
        /// </summary>
        public int NumberOfFiniteBuckets { get; set; }
    }
}
