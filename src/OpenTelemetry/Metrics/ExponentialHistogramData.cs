// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Contains the data for an exponential histogram.
/// </summary>
public sealed class ExponentialHistogramData
{
    internal ExponentialHistogramData()
    {
        this.PositiveBuckets = new();
        this.NegativeBuckets = new();
    }

    /// <summary>
    /// Gets the exponential histogram scale.
    /// </summary>
    public int Scale { get; internal set; }

    /// <summary>
    /// Gets the exponential histogram zero count.
    /// </summary>
    public long ZeroCount { get; internal set; }

    /// <summary>
    /// Gets the exponential histogram positive buckets.
    /// </summary>
    public ExponentialHistogramBuckets PositiveBuckets { get; private set; }

    /// <summary>
    /// Gets the exponential histogram negative buckets.
    /// </summary>
    internal ExponentialHistogramBuckets NegativeBuckets { get; private set; }

    internal ExponentialHistogramData Copy()
    {
        var copy = new ExponentialHistogramData
        {
            Scale = this.Scale,
            ZeroCount = this.ZeroCount,
            PositiveBuckets = this.PositiveBuckets.Copy(),
            NegativeBuckets = this.NegativeBuckets.Copy(),
        };
        return copy;
    }
}