// <copyright file="ExplicitBucketHistogramConfiguration.cs" company="OpenTelemetry Authors">
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
    public class ExplicitBucketHistogramConfiguration : MetricStreamConfiguration
    {
        /// <summary>
        /// Gets or sets the values representing explicit histogram bucket
        /// boundary values.
        /// </summary>
        /// <remarks>
        /// The array must be in ascending order with distinct values.
        /// An empty array would result in no histogram buckets being calculated.
        /// A null value would result in default bucket boundaries being used.
        /// </remarks>
        public double[] Boundaries { get; set; }
    }
}
