// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Attributes;
using OpenTelemetry.Metrics;

namespace Benchmarks.Metrics;

public class Base2ExponentialHistogramMapToIndexBenchmarks
{
    private const int MaxValue = 10000;
    private readonly Random random = new();
    private Base2ExponentialBucketHistogram? exponentialHistogram;

    [Params(-11, 1, 3, 7, 10, 20)]
    public int Scale { get; set; }

    [GlobalSetup]
    public void Setup()
        => this.exponentialHistogram = new Base2ExponentialBucketHistogram(scale: this.Scale);

    [Benchmark]
    public int MapToIndex()
#pragma warning disable CA5394 // Do not use insecure randomness
        => this.exponentialHistogram!.MapToIndex(this.random.Next(MaxValue));
#pragma warning restore CA5394 // Do not use insecure randomness
}
