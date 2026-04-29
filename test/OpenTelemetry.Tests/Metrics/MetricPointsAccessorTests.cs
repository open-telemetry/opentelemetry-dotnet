// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MetricPointsAccessorTests
{
    [Fact]
    public void Verify_Equals_SameReferences()
    {
        var points = new MetricPoint[0];
        var indices = new int[0];

        var accessor1 = new MetricPointsAccessor(points, indices, 0);
        var accessor2 = new MetricPointsAccessor(points, indices, 0);

        Assert.True(accessor1.Equals(accessor2));
        Assert.True(accessor1.Equals((object)accessor2));
    }

    [Fact]
    public void Verify_Equals_DifferentReferences()
    {
        var accessor1 = new MetricPointsAccessor(new MetricPoint[0], new int[0], 0);
        var accessor2 = new MetricPointsAccessor(new MetricPoint[0], new int[0], 0);

        Assert.False(accessor1.Equals(accessor2));
        Assert.False(accessor1.Equals((object)accessor2));
    }

    [Fact]
    public void Verify_Equals_DifferentTargetCount()
    {
        var points = new MetricPoint[2];
        var indices = new int[2];

        var accessor1 = new MetricPointsAccessor(points, indices, 1);
        var accessor2 = new MetricPointsAccessor(points, indices, 2);

        Assert.False(accessor1.Equals(accessor2));
    }

    [Fact]
    public void VerifyOperator_Equals()
    {
        var points = new MetricPoint[0];
        var indices = new int[0];

        var accessor1 = new MetricPointsAccessor(points, indices, 0);
        var accessor2 = new MetricPointsAccessor(points, indices, 0);
        var accessor3 = new MetricPointsAccessor(new MetricPoint[0], new int[0], 0);

        Assert.True(accessor1 == accessor2);
        Assert.False(accessor1 == accessor3);
    }

    [Fact]
    public void VerifyOperator_NotEquals()
    {
        var points = new MetricPoint[0];
        var indices = new int[0];

        var accessor1 = new MetricPointsAccessor(points, indices, 0);
        var accessor2 = new MetricPointsAccessor(points, indices, 0);
        var accessor3 = new MetricPointsAccessor(new MetricPoint[0], new int[0], 0);

        Assert.False(accessor1 != accessor2);
        Assert.True(accessor1 != accessor3);
    }

    [Fact]
    public void Verify_GetHashCode_SameReferences()
    {
        var points = new MetricPoint[0];
        var indices = new int[0];

        var accessor1 = new MetricPointsAccessor(points, indices, 0);
        var accessor2 = new MetricPointsAccessor(points, indices, 0);

        Assert.Equal(accessor1.GetHashCode(), accessor2.GetHashCode());
    }
}
