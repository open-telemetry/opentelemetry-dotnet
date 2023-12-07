// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Represents a bucket in the histogram metric type.
/// </summary>
public readonly struct HistogramBucket
{
    internal HistogramBucket(double explicitBound, long bucketCount)
    {
        this.ExplicitBound = explicitBound;
        this.BucketCount = bucketCount;
    }

    /// <summary>
    /// Gets the configured bounds for the bucket or <see
    /// cref="double.PositiveInfinity"/> for the catch-all bucket.
    /// </summary>
    public double ExplicitBound { get; }

    /// <summary>
    /// Gets the count of items in the bucket.
    /// </summary>
    public long BucketCount { get; }
}
