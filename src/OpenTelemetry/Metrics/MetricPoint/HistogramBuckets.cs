// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Metrics;

/// <summary>
/// A collection of <see cref="HistogramBucket"/>s associated with a histogram metric type.
/// </summary>
// Note: Does not implement IEnumerable<> to prevent accidental boxing.
public class HistogramBuckets
{
    internal readonly double[]? ExplicitBounds;

    internal readonly HistogramBucketValues[] BucketCounts;

    internal double RunningSum;
    internal double SnapshotSum;

    internal double RunningMin = double.PositiveInfinity;
    internal double SnapshotMin;

    internal double RunningMax = double.NegativeInfinity;
    internal double SnapshotMax;

    private readonly HistogramExplicitBounds? histogramExplicitBounds;

    internal HistogramBuckets(HistogramExplicitBounds? histogramExplicitBounds)
    {
        this.histogramExplicitBounds = histogramExplicitBounds;
        this.ExplicitBounds = histogramExplicitBounds?.Bounds;
        this.BucketCounts = this.ExplicitBounds != null ? new HistogramBucketValues[this.ExplicitBounds.Length + 1] : [];
    }

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="HistogramBuckets"/>.
    /// </summary>
    /// <returns><see cref="Enumerator"/>.</returns>
    public Enumerator GetEnumerator() => new(this);

    internal HistogramBuckets Copy()
    {
        HistogramBuckets copy = new HistogramBuckets(this.histogramExplicitBounds);

        Array.Copy(this.BucketCounts, copy.BucketCounts, this.BucketCounts.Length);
        copy.SnapshotSum = this.SnapshotSum;
        copy.SnapshotMin = this.SnapshotMin;
        copy.SnapshotMax = this.SnapshotMax;

        return copy;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int FindBucketIndex(double value)
    {
        Debug.Assert(this.histogramExplicitBounds != null, "histogramExplicitBounds was null.");
        return this.histogramExplicitBounds!.FindBucketIndex(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Snapshot(bool outputDelta)
    {
        var bucketCounts = this.BucketCounts;

        if (outputDelta)
        {
            for (int i = 0; i < bucketCounts.Length; i++)
            {
                ref var values = ref bucketCounts[i];
                ref var running = ref values.RunningValue;
                values.SnapshotValue = running;
                running = 0L;
            }
        }
        else
        {
            for (int i = 0; i < bucketCounts.Length; i++)
            {
                ref var values = ref bucketCounts[i];
                values.SnapshotValue = values.RunningValue;
            }
        }
    }

    /// <summary>
    /// Enumerates the elements of a <see cref="HistogramBuckets"/>.
    /// </summary>
    // Note: Does not implement IEnumerator<> to prevent accidental boxing.
#pragma warning disable CA1034 // Nested types should not be visible - already part of public API
    public struct Enumerator
#pragma warning restore CA1034 // Nested types should not be visible - already part of public API
    {
        private readonly int numberOfBuckets;
        private readonly HistogramBuckets histogramMeasurements;
        private int index;

        internal Enumerator(HistogramBuckets histogramMeasurements)
        {
            this.histogramMeasurements = histogramMeasurements;
            this.index = 0;
            this.Current = default;
            this.numberOfBuckets = histogramMeasurements.BucketCounts.Length;
        }

        /// <summary>
        /// Gets the <see cref="HistogramBucket"/> at the current position of the enumerator.
        /// </summary>
        public HistogramBucket Current { get; private set; }

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
            if (this.index < this.numberOfBuckets)
            {
                double explicitBound = this.index < this.numberOfBuckets - 1
                    ? this.histogramMeasurements.ExplicitBounds![this.index]
                    : double.PositiveInfinity;
                long bucketCount = this.histogramMeasurements.BucketCounts[this.index].SnapshotValue;
                this.Current = new HistogramBucket(explicitBound, bucketCount);
                this.index++;
                return true;
            }

            return false;
        }
    }

    internal struct HistogramBucketValues
    {
        public long RunningValue;
        public long SnapshotValue;
    }
}
