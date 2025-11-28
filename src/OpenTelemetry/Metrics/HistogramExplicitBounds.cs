// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Metrics;

internal sealed class HistogramExplicitBounds
{
    internal const int DefaultBoundaryCountForBinarySearch = 50;

    private readonly BucketLookupNode? bucketLookupTreeRoot;
    private readonly Func<double, int> findHistogramBucketIndex;

    public HistogramExplicitBounds(double[] bounds)
    {
        this.Bounds = CleanUpInfinitiesFromExplicitBounds(bounds);
        this.findHistogramBucketIndex = this.FindBucketIndexLinear;

        if (this.Bounds.Length >= DefaultBoundaryCountForBinarySearch)
        {
            this.bucketLookupTreeRoot = ConstructBalancedBST(this.Bounds, 0, this.Bounds.Length);
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
    }

    public double[] Bounds { get; private set; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FindBucketIndex(double value)
    {
        return this.findHistogramBucketIndex(value);
    }

    private static double[] CleanUpInfinitiesFromExplicitBounds(double[] bounds)
    {
        for (var i = 0; i < bounds.Length; i++)
        {
            if (double.IsNegativeInfinity(bounds[i]) || double.IsPositiveInfinity(bounds[i]))
            {
                return bounds
                    .Where(b => !double.IsNegativeInfinity(b) && !double.IsPositiveInfinity(b))
                    .ToArray();
            }
        }

        return bounds;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindBucketIndexBinary(double value)
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

        Debug.Assert(this.Bounds != null, "ExplicitBounds was null.");

        return this.Bounds!.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindBucketIndexLinear(double value)
    {
        Debug.Assert(this.Bounds != null, "ExplicitBounds was null.");

        int i;
        for (i = 0; i < this.Bounds!.Length; i++)
        {
            // Upper bound is inclusive
            if (value <= this.Bounds[i])
            {
                break;
            }
        }

        return i;
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
