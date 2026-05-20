// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NET
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
#endif
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

internal sealed class HistogramExplicitBounds
{
    internal const int DefaultBoundaryCountForBinarySearch = 50;

    private const int RadixLookupBitCount = 12;
    private const int RadixLinearSearchThreshold = 32;

    private readonly RadixBucketLookup? radixBucketLookup;

    public HistogramExplicitBounds(double[] bounds, double[]? displayBounds = null)
    {
        this.Bounds = CleanUpInfinitiesFromExplicitBounds(bounds);
        this.DisplayBounds = displayBounds != null ? CleanUpInfinitiesFromExplicitBounds(displayBounds) : null;

        if (this.Bounds.Length >= DefaultBoundaryCountForBinarySearch)
        {
            this.radixBucketLookup = new(this.Bounds);
        }
    }

    public double[] Bounds { get; }

    /// <summary>
    /// Gets the cleaned display bounds for export/serialization.
    /// When non-null, exporters should use these values instead of Bounds
    /// for displaying bucket boundaries. This is used to fix float-to-double
    /// precision artifacts (e.g., 0.025 instead of 0.02500000037252903).
    /// </summary>
    public double[]? DisplayBounds { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FindBucketIndex(double value)
    {
        if (double.IsNaN(value))
        {
            return this.Bounds.Length;
        }

        if (this.Bounds.Length == 0)
        {
            return 0;
        }

        if (value <= this.Bounds[0])
        {
            return 0;
        }

        if (value > this.Bounds[this.Bounds.Length - 1])
        {
            return this.Bounds.Length;
        }

        if (this.radixBucketLookup != null)
        {
            (var start, var end) = this.radixBucketLookup.GetBucketSearchRange(value);

            return start == end
                ? start
                : end - start > RadixLinearSearchThreshold
                ? this.FindBucketIndexBinary(value, start, end)
                : this.FindBucketIndexLinear(value, start, end);
        }

        return this.FindBucketIndexLinear(value, 0, this.Bounds.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double[] CleanUpInfinitiesFromExplicitBounds(double[] bounds)
    {
        for (var i = 0; i < bounds.Length; i++)
        {
            if (double.IsNegativeInfinity(bounds[i]) || double.IsPositiveInfinity(bounds[i]))
            {
                return [.. bounds.Where(b => !double.IsNegativeInfinity(b) && !double.IsPositiveInfinity(b))];
            }
        }

        return bounds;
    }

#if NET
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindBucketIndexLinearSimd(ReadOnlySpan<double> bounds, double value)
    {
        var index = 0;

        if (Avx.IsSupported && bounds.Length >= Vector256<double>.Count)
        {
            ref var searchSpace = ref MemoryMarshal.GetReference(bounds);
            var valueVector = Vector256.Create(value);
            var lastStart = bounds.Length - Vector256<double>.Count;

            while (index <= lastStart)
            {
                var boundsVector = Vector256.LoadUnsafe(ref searchSpace, (nuint)index);
                var compare = Avx.CompareLessThanOrEqual(valueVector, boundsVector);
                var mask = Avx.MoveMask(compare);

                if (mask != 0)
                {
                    return index + BitOperations.TrailingZeroCount((uint)mask);
                }

                index += Vector256<double>.Count;
            }
        }
        else if (Sse2.IsSupported && bounds.Length >= Vector128<double>.Count)
        {
            ref var searchSpace = ref MemoryMarshal.GetReference(bounds);
            var valueVector = Vector128.Create(value);
            var lastStart = bounds.Length - Vector128<double>.Count;

            while (index <= lastStart)
            {
                var boundsVector = Vector128.LoadUnsafe(ref searchSpace, (nuint)index);
                var compare = Sse2.CompareLessThanOrEqual(valueVector, boundsVector);
                var mask = Sse2.MoveMask(compare);

                if (mask != 0)
                {
                    return index + BitOperations.TrailingZeroCount((uint)mask);
                }

                index += Vector128<double>.Count;
            }
        }
        else if (AdvSimd.Arm64.IsSupported && bounds.Length >= Vector128<double>.Count)
        {
            ref var searchSpace = ref MemoryMarshal.GetReference(bounds);
            var valueVector = Vector128.Create(value);
            var lastStart = bounds.Length - Vector128<double>.Count;

            while (index <= lastStart)
            {
                var boundsVector = Vector128.LoadUnsafe(ref searchSpace, (nuint)index);
                var compare = AdvSimd.Arm64.CompareLessThanOrEqual(valueVector, boundsVector).AsUInt64();

                if (compare.GetElement(0) != 0)
                {
                    return index;
                }

                if (compare.GetElement(1) != 0)
                {
                    return index + 1;
                }

                index += Vector128<double>.Count;
            }
        }

        for (; index < bounds.Length; index++)
        {
            if (value <= bounds[index])
            {
                return index;
            }
        }

        return -1;
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ToSortableBits(double value)
    {
        var bits = (ulong)BitConverter.DoubleToInt64Bits(value);
        return (bits & 0x8000_0000_0000_0000UL) == 0
            ? bits ^ 0x8000_0000_0000_0000UL
            : ~bits;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindBucketIndexBinary(double value, int start, int end)
    {
        var bounds = this.Bounds;
        var left = start;
        var right = end - 1;

        while (left <= right)
        {
            var middle = left + ((right - left) / 2);

            if (value <= bounds[middle])
            {
                right = middle - 1;
            }
            else
            {
                left = middle + 1;
            }
        }

        return left;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindBucketIndexLinear(double value, int start, int end)
    {
#if NET
        if (!double.IsNaN(value))
        {
            var index = FindBucketIndexLinearSimd(this.Bounds.AsSpan(start, end - start), value);
            if (index >= 0)
            {
                return start + index;
            }
        }
#endif

        var bounds = this.Bounds;

        for (var i = start; i < end; i++)
        {
            // Upper bound is inclusive
            if (value <= bounds[i])
            {
                return i;
            }
        }

        return end;
    }

    private sealed class RadixBucketLookup
    {
        private readonly int[] bucketSearchStarts;
        private readonly int keyMask;
        private readonly int shift;

        public RadixBucketLookup(double[] bounds)
        {
            Debug.Assert(bounds.Length > 0, "bounds was empty");

            var firstKey = ToSortableBits(bounds[0]);
            var lastKey = ToSortableBits(bounds[bounds.Length - 1]);
            var commonPrefixLength = MathHelper.LeadingZero64((long)(firstKey ^ lastKey));
            var radixBits = Math.Min(RadixLookupBitCount, 64 - commonPrefixLength);

            if (radixBits == 0)
            {
                this.bucketSearchStarts = [0, bounds.Length];
                this.keyMask = 0;
                this.shift = 0;
            }
            else
            {
                var bucketCount = 1 << radixBits;
                this.bucketSearchStarts = new int[bucketCount + 1];
                this.keyMask = bucketCount - 1;
                this.shift = 64 - commonPrefixLength - radixBits;

                var boundaryIndex = 0;

                for (var key = 0; key < bucketCount; key++)
                {
                    this.bucketSearchStarts[key] = boundaryIndex;

                    while (boundaryIndex < bounds.Length && this.GetKey(ToSortableBits(bounds[boundaryIndex])) == key)
                    {
                        boundaryIndex++;
                    }
                }

                this.bucketSearchStarts[bucketCount] = bounds.Length;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (int Start, int End) GetBucketSearchRange(double value)
        {
            if (double.IsNaN(value))
            {
                var end = this.bucketSearchStarts[this.bucketSearchStarts.Length - 1];
                return (end, end);
            }

            var key = this.GetKey(ToSortableBits(value));
            return (this.bucketSearchStarts[key], this.bucketSearchStarts[key + 1]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetKey(ulong sortableBits)
            => this.keyMask == 0 ? 0 : (int)((sortableBits >> this.shift) & (uint)this.keyMask);
    }
}
