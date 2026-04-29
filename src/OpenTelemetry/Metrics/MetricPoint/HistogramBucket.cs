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
    public static bool operator ==(HistogramBucket left, HistogramBucket right) => left.Equals(right);

    /// <summary>
    /// Compare two <see cref="HistogramBucket"/> for inequality.
    /// </summary>
    public static bool operator !=(HistogramBucket left, HistogramBucket right) => !left.Equals(right);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is HistogramBucket other && this.Equals(other);

    /// <inheritdoc/>
    public bool Equals(HistogramBucket other)
        => this.ExplicitBound.Equals(other.ExplicitBound) && this.BucketCount == other.BucketCount;

    /// <inheritdoc/>
    public override int GetHashCode()
    {
#if NET || NETSTANDARD2_1_OR_GREATER
        return HashCode.Combine(this.ExplicitBound, this.BucketCount);
#else
        unchecked
        {
            var hash = 17;
            hash = (31 * hash) + this.ExplicitBound.GetHashCode();
            hash = (31 * hash) + this.BucketCount.GetHashCode();
            return hash;
        }
#endif
    }
}
