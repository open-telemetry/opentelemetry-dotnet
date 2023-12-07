// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Stores configuration for a histogram metric stream with base-2 exponential bucket boundaries.
/// </summary>
public sealed class Base2ExponentialBucketHistogramConfiguration : HistogramConfiguration
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