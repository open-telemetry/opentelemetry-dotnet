// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class HistogramBucketTests
{
    [Fact]
    public void Verify_Equals()
    {
        var bucket1 = new HistogramBucket(100.0, 5L);
        var bucket2 = new HistogramBucket(100.0, 5L);
        var bucket3 = new HistogramBucket(200.0, 5L);

        Assert.True(bucket1.Equals(bucket2));
        Assert.True(bucket1.Equals((object)bucket2));
        Assert.False(bucket1.Equals(bucket3));
        Assert.False(bucket1.Equals((object)bucket3));
        Assert.False(bucket1.Equals(null));
        Assert.False(bucket1.Equals(42));
    }

    [Fact]
    public void VerifyOperator_Equals()
    {
        var bucket1 = new HistogramBucket(100.0, 5L);
        var bucket2 = new HistogramBucket(100.0, 5L);
        var bucket3 = new HistogramBucket(200.0, 5L);

        Assert.True(bucket1 == bucket2);
        Assert.False(bucket1 == bucket3);
    }

    [Fact]
    public void VerifyOperator_NotEquals()
    {
        var bucket1 = new HistogramBucket(100.0, 5L);
        var bucket2 = new HistogramBucket(100.0, 5L);
        var bucket3 = new HistogramBucket(200.0, 5L);

        Assert.False(bucket1 != bucket2);
        Assert.True(bucket1 != bucket3);
    }

    [Fact]
    public void Verify_GetHashCode()
    {
        var bucket1 = new HistogramBucket(100.0, 5L);
        var bucket2 = new HistogramBucket(100.0, 5L);
        var bucket3 = new HistogramBucket(200.0, 5L);

        Assert.Equal(bucket1.GetHashCode(), bucket2.GetHashCode());
        Assert.NotEqual(bucket1.GetHashCode(), bucket3.GetHashCode());
    }

    [Fact]
    public void Verify_PositiveInfinity()
    {
        var bucket1 = new HistogramBucket(double.PositiveInfinity, 10L);
        var bucket2 = new HistogramBucket(double.PositiveInfinity, 10L);

        Assert.True(bucket1 == bucket2);
        Assert.Equal(bucket1.GetHashCode(), bucket2.GetHashCode());
    }
}
