// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics.Tests;

public class HistogramBucketTests
{
    [Fact]
    public void Verify_Equals()
    {
        var bucket1 = new HistogramBucket(10.5, 42);
        Assert.True(bucket1.Equals(bucket1));
        Assert.True(bucket1.Equals((object)bucket1));

        var bucket2 = new HistogramBucket(10.5, 42);
        Assert.True(bucket1.Equals(bucket2));
        Assert.True(bucket1.Equals((object)bucket2));

        var differentBound = new HistogramBucket(20.5, 42);
        Assert.False(bucket1.Equals(differentBound));
        Assert.False(bucket1.Equals((object)differentBound));

        var differentCount = new HistogramBucket(10.5, 43);
        Assert.False(bucket1.Equals(differentCount));
        Assert.False(bucket1.Equals((object)differentCount));

        Assert.False(bucket1.Equals(Guid.Empty));
    }

    [Fact]
    public void Verify_Equals_NaNBound()
    {
        // ExplicitBound is compared using double.Equals so that NaN bounds
        // compare equal (x.Equals(x) stays reflexive).
        var bucket1 = new HistogramBucket(double.NaN, 1);
        var bucket2 = new HistogramBucket(double.NaN, 1);
        Assert.True(bucket1.Equals(bucket2));
        Assert.True(bucket1 == bucket2);
    }

    [Fact]
    public void VerifyOperator_Equals()
    {
        var bucket1 = new HistogramBucket(10.5, 42);
        var bucket2 = new HistogramBucket(10.5, 42);
        var bucket3 = new HistogramBucket(10.5, 43);

        Assert.True(bucket1 == bucket2);
        Assert.False(bucket1 == bucket3);
    }

    [Fact]
    public void VerifyOperator_NotEquals()
    {
        var bucket1 = new HistogramBucket(10.5, 42);
        var bucket2 = new HistogramBucket(10.5, 42);
        var bucket3 = new HistogramBucket(10.5, 43);

        Assert.False(bucket1 != bucket2);
        Assert.True(bucket1 != bucket3);
    }

    [Fact]
    public void Verify_GetHashCode()
    {
        var bucket1 = new HistogramBucket(10.5, 42);
        var bucket2 = new HistogramBucket(10.5, 42);
        var bucket3 = new HistogramBucket(20.5, 42);

        Assert.Equal(bucket1.GetHashCode(), bucket2.GetHashCode());
        Assert.NotEqual(bucket1.GetHashCode(), bucket3.GetHashCode());
    }
}
