// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Represents a bucket in the histogram metric type.
/// </summary>
public readonly struct HistogramBucket : IEquatable<HistogramBucket>
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

    /// <summary>
    /// Compare two <see cref="HistogramBucket"/> for equality.
    /// </summary>
    /// <param name="bucket1">First bucket to compare.</param>
    /// <param name="bucket2">Second bucket to compare.</param>
    public static bool operator ==(HistogramBucket bucket1, HistogramBucket bucket2) => bucket1.Equals(bucket2);

    /// <summary>
    /// Compare two <see cref="HistogramBucket"/> for not equality.
    /// </summary>
    /// <param name="bucket1">First bucket to compare.</param>
    /// <param name="bucket2">Second bucket to compare.</param>
    public static bool operator !=(HistogramBucket bucket1, HistogramBucket bucket2) => !bucket1.Equals(bucket2);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is HistogramBucket other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
#if NET || NETSTANDARD2_1_OR_GREATER
        return HashCode.Combine(this.ExplicitBound, this.BucketCount);
#else
        var hash = 17;
        unchecked
        {
            hash = (31 * hash) + this.ExplicitBound.GetHashCode();
            hash = (31 * hash) + this.BucketCount.GetHashCode();
        }

        return hash;
#endif
    }

    /// <inheritdoc/>
    public bool Equals(HistogramBucket other) =>
        this.ExplicitBound.Equals(other.ExplicitBound) &&
        this.BucketCount == other.BucketCount;
}
