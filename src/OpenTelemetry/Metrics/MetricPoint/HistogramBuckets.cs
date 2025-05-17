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
    internal const int DefaultBoundaryCountForBinarySearch = 50;

    internal readonly double[]? ExplicitBounds;

    internal readonly HistogramBucketValues[] BucketCounts;

    internal double RunningSum;
    internal double SnapshotSum;

    internal double RunningMin = double.PositiveInfinity;
    internal double SnapshotMin;

    internal double RunningMax = double.NegativeInfinity;
    internal double SnapshotMax;

    private readonly BucketLookupNode? bucketLookupTreeRoot;

    private readonly Func<double, int> findHistogramBucketIndex;

    internal HistogramBuckets(double[]? explicitBounds)
    {
        explicitBounds = CleanUpInfinitiesFromExplicitBounds(explicitBounds);
        this.ExplicitBounds = explicitBounds;
        this.findHistogramBucketIndex = this.FindBucketIndexLinear;
        if (explicitBounds != null && explicitBounds.Length >= DefaultBoundaryCountForBinarySearch)
        {
            this.bucketLookupTreeRoot = ConstructBalancedBST(explicitBounds, 0, explicitBounds.Length)!;
            this.findHistogramBucketIndex = this.FindBucketIndexBinary;

            static BucketLookupNode? ConstructBalancedBST(double[] values, int min, int max)
            {
                if (min == max)
                {
                    return null;
                }

                int median = min + ((max - min) / 2);
                return new BucketLookupNode
                {
                    Index = median,
                    UpperBoundInclusive = values[median],
                    LowerBoundExclusive = median > 0 ? values[median - 1] : double.NegativeInfinity,
                    Left = ConstructBalancedBST(values, min, median),
                    Right = ConstructBalancedBST(values, median + 1, max),
                };
            }
        }

        this.BucketCounts = explicitBounds != null ? new HistogramBucketValues[explicitBounds.Length + 1] : Array.Empty<HistogramBucketValues>();
    }

    private static double[]? CleanUpInfinitiesFromExplicitBounds(double[]? explicitBounds) => explicitBounds
        ?.Where(b => !double.IsNegativeInfinity(b) && !double.IsPositiveInfinity(b)).ToArray();

    /// <summary>
    /// Returns an enumerator that iterates through the <see cref="HistogramBuckets"/>.
    /// </summary>
    /// <returns><see cref="Enumerator"/>.</returns>
    public Enumerator GetEnumerator() => new(this);

    internal HistogramBuckets Copy()
    {
        HistogramBuckets copy = new HistogramBuckets(this.ExplicitBounds);

        Array.Copy(this.BucketCounts, copy.BucketCounts, this.BucketCounts.Length);
        copy.SnapshotSum = this.SnapshotSum;
        copy.SnapshotMin = this.SnapshotMin;
        copy.SnapshotMax = this.SnapshotMax;

        return copy;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int FindBucketIndex(double value)
    {
        return this.findHistogramBucketIndex(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int FindBucketIndexBinary(double value)
    {
        BucketLookupNode? current = this.bucketLookupTreeRoot;

        Debug.Assert(current != null, "Bucket root was null.");

        do
        {
            if (value <= current!.LowerBoundExclusive)
            {
                current = current.Left;
            }
            else if (value > current.UpperBoundInclusive)
            {
                current = current.Right;
            }
            else
            {
                return current.Index;
            }
        }
        while (current != null);

        Debug.Assert(this.ExplicitBounds != null, "ExplicitBounds was null.");

        return this.ExplicitBounds!.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int FindBucketIndexLinear(double value)
    {
        Debug.Assert(this.ExplicitBounds != null, "ExplicitBounds was null.");

        int i;
        for (i = 0; i < this.ExplicitBounds!.Length; i++)
        {
            // Upper bound is inclusive
            if (value <= this.ExplicitBounds[i])
            {
                break;
            }
        }

        return i;
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
    public struct Enumerator
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

    private sealed class BucketLookupNode
    {
        public double UpperBoundInclusive { get; set; }

        public double LowerBoundExclusive { get; set; }

        public int Index { get; set; }

        public BucketLookupNode? Left { get; set; }

        public BucketLookupNode? Right { get; set; }
    }
}
