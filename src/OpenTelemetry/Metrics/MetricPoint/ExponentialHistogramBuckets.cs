// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Contains the buckets of an exponential histogram.
/// </summary>
// Note: Does not implement IEnumerable<> to prevent accidental boxing.
public sealed class ExponentialHistogramBuckets
{
    private long[] buckets = Array.Empty<long>();
    private int size;

    internal ExponentialHistogramBuckets()
    {
    }

    /// <summary>
    /// Gets the exponential histogram offset.
    /// </summary>
    public int Offset { get; private set; }

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="ExponentialHistogramBuckets"/>.
    /// </summary>
    /// <returns><see cref="Enumerator"/>.</returns>
    public Enumerator GetEnumerator() => new(this.buckets, this.size);

    internal void SnapshotBuckets(CircularBufferBuckets buckets)
    {
        if (this.buckets.Length != buckets.Capacity)
        {
            this.buckets = new long[buckets.Capacity];
        }

        this.size = buckets.Size;
        this.Offset = buckets.Offset;
        buckets.Copy(this.buckets);
    }

    internal ExponentialHistogramBuckets Copy()
    {
        var copy = new ExponentialHistogramBuckets
        {
            size = this.size,
            Offset = this.Offset,
            buckets = new long[this.buckets.Length],
        };
        Array.Copy(this.buckets, copy.buckets, this.buckets.Length);
        return copy;
    }

    /// <summary>
    /// Enumerates the bucket counts of an exponential histogram.
    /// </summary>
    // Note: Does not implement IEnumerator<> to prevent accidental boxing.
#pragma warning disable CA1034 // Nested types should not be visible - already part of public API
    public struct Enumerator
#pragma warning restore CA1034 // Nested types should not be visible - already part of public API
    {
        private readonly long[] buckets;
        private readonly int size;
        private int index;

        internal Enumerator(long[] buckets, int size)
        {
            this.index = 0;
            this.size = size;
            this.buckets = buckets;
            this.Current = default;
        }

        /// <summary>
        /// Gets the bucket count at the current position of the enumerator.
        /// </summary>
        public long Current { get; private set; }

        /// <summary>
        /// Advances the enumerator to the next element of the <see
        /// cref="HistogramBuckets"/>.
        /// </summary>
        /// <returns><see langword="true"/> if the enumerator was
        /// successfully advanced to the next element; <see
        /// langword="false"/> if the enumerator has passed the end of the
        /// collection.</returns>
        public bool MoveNext()
        {
            if (this.index < this.size)
            {
                this.Current = this.buckets[this.index++];
                return true;
            }

            return false;
        }
    }
}
