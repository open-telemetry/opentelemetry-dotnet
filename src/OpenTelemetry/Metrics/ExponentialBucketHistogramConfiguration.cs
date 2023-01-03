// <copyright file="ExponentialBucketHistogramConfiguration.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics;

/// <summary>
/// Stores configuration for a histogram metric stream with exponential bucket boundaries.
/// </summary>
public sealed class ExponentialBucketHistogramConfiguration : MetricStreamConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of buckets in each of the positive and negative ranges, not counting the special zero bucket.
    /// </summary>
    /// <remarks>
    /// The default value is 160.
    /// </remarks>
    public int MaxSize { get; set; } = 160;
}
