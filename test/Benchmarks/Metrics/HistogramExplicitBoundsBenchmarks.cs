// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using OpenTelemetry.Metrics;

namespace Benchmarks.Metrics;

public class HistogramExplicitBoundsBenchmarks
{
    private HistogramExplicitBounds? emptyBounds;
    private HistogramExplicitBounds? finiteBounds;
    private HistogramExplicitBounds? infiniteBounds;
    private double exactBoundaryValue;
    private double inRangeValue;

    [Params(10, 49, 50, 1000)]
    public int BoundCount { get; set; }

    [Params("PositiveOnly", "MixedSigned")]
    public string Layout { get; set; } = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        var finite = CreateBounds(this.BoundCount, this.Layout);
        var infinite = CreateInfiniteBounds(finite);

        this.emptyBounds = new(Array.Empty<double>());
        this.finiteBounds = new(finite);
        this.infiniteBounds = new(infinite);

        var midpointIndex = finite.Length / 2;
        this.exactBoundaryValue = finite[midpointIndex];
        this.inRangeValue = midpointIndex == 0
            ? finite[0]
            : finite[midpointIndex - 1] + ((finite[midpointIndex] - finite[midpointIndex - 1]) / 2);
    }

    [Benchmark]
    public int LookupExactBoundary() => this.finiteBounds!.FindBucketIndex(this.exactBoundaryValue);

    [Benchmark]
    public int LookupInRangeValue() => this.finiteBounds!.FindBucketIndex(this.inRangeValue);

    [Benchmark]
    public int LookupNegativeInfinity() => this.finiteBounds!.FindBucketIndex(double.NegativeInfinity);

    [Benchmark]
    public int LookupPositiveInfinity() => this.finiteBounds!.FindBucketIndex(double.PositiveInfinity);

    [Benchmark]
    public int LookupNaN() => this.finiteBounds!.FindBucketIndex(double.NaN);

    [Benchmark]
    public int LookupWithInfiniteBounds() => this.infiniteBounds!.FindBucketIndex(this.inRangeValue);

    [Benchmark]
    public int LookupEmptyBounds() => this.emptyBounds!.FindBucketIndex(42);

    private static double[] CreateBounds(int count, string layout)
        => layout == "MixedSigned"
            ? CreateMixedSignedBounds(count)
            : CreatePositiveOnlyBounds(count);

    private static double[] CreateInfiniteBounds(double[] finiteBounds)
    {
        var infiniteBounds = new double[finiteBounds.Length + 2];
        infiniteBounds[0] = double.NegativeInfinity;
        Array.Copy(finiteBounds, 0, infiniteBounds, 1, finiteBounds.Length);
        infiniteBounds[infiniteBounds.Length - 1] = double.PositiveInfinity;
        return infiniteBounds;
    }

    private static double[] CreateMixedSignedBounds(int count)
    {
        var bounds = new double[count];
        var start = -5000.0;
        var step = 10000.0 / count;

        for (var i = 0; i < count; i++)
        {
            bounds[i] = start + (i * step);
        }

        return bounds;
    }

    private static double[] CreatePositiveOnlyBounds(int count)
    {
        var bounds = new double[count];
        var step = 10000.0 / count;

        for (var i = 0; i < count; i++)
        {
            bounds[i] = i * step;
        }

        return bounds;
    }
}
