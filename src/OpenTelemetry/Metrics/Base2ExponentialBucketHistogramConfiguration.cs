// <copyright file="Base2ExponentialBucketHistogramConfiguration.cs" company="OpenTelemetry Authors">
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
/// Stores configuration for a histogram metric stream with base-2 exponential bucket boundaries.
/// </summary>
internal sealed class Base2ExponentialBucketHistogramConfiguration : HistogramConfiguration
{
    private int maxSize = Metric.DefaultExponentialHistogramMaxBuckets;
    private int maxScale = Metric.DefaultExponentialHistogramMaxScale;

    /// <summary>
    /// Gets or sets the maximum number of buckets in each of the positive and negative ranges, not counting the special zero bucket.
    /// </summary>
    /// <remarks>
    /// The default value is 160. The minimum size is 2.
    /// </remarks>
    public int MaxSize
    {
        get
        {
            return this.maxSize;
        }

        set
        {
            if (value < 2)
            {
                throw new ArgumentException($"Histogram max size is invalid. Minimum size is 2.", nameof(value));
            }

            this.maxSize = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum scale factor used to determine the resolution of bucket boundaries.
    /// The higher the scale the higher the resolution.
    /// </summary>
    /// <remarks>
    /// The default value is 20. The minimum size is -11. The maximum size is 20.
    /// </remarks>
    public int MaxScale
    {
        get
        {
            return this.maxScale;
        }

        set
        {
            if (value < -11 || value > 20)
            {
                throw new ArgumentException($"Histogram max scale is invalid. Max scale must be in the range [-11, 20].", nameof(value));
            }

            this.maxScale = value;
        }
    }
}
