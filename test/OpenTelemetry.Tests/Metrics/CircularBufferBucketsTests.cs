// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class CircularBufferBucketsTests
{
    [Fact]
    public void ConstructorThrowsOnInvalidCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBufferBuckets(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBufferBuckets(-1));
    }

    [Fact]
    public void BasicInsertions()
    {
        var buckets = new CircularBufferBuckets(5);

        Assert.Equal(5, buckets.Capacity);
        Assert.Equal(0, buckets.Size);

        Assert.Equal(0, buckets.TryIncrement(0));
        Assert.Equal(1, buckets.Size);

        Assert.Equal(0, buckets.TryIncrement(1));
        Assert.Equal(2, buckets.Size);

        Assert.Equal(0, buckets.TryIncrement(3));
        Assert.Equal(4, buckets.Size);

        Assert.Equal(0, buckets.TryIncrement(4));
        Assert.Equal(5, buckets.Size);

        Assert.Equal(0, buckets.TryIncrement(2));
        Assert.Equal(5, buckets.Size);

        Assert.Equal(1, buckets.TryIncrement(9));
        Assert.Equal(1, buckets.TryIncrement(5));
        Assert.Equal(1, buckets.TryIncrement(-1));
        Assert.Equal(2, buckets.TryIncrement(10));
        Assert.Equal(2, buckets.TryIncrement(19));
        Assert.Equal(3, buckets.TryIncrement(20));
        Assert.Equal(3, buckets.TryIncrement(39));
        Assert.Equal(4, buckets.TryIncrement(40));
        Assert.Equal(5, buckets.Size);
    }

    [Fact]
    public void PositiveInsertions()
    {
        var buckets = new CircularBufferBuckets(5);

        Assert.Equal(0, buckets.TryIncrement(102));
        Assert.Equal(0, buckets.TryIncrement(103));
        Assert.Equal(0, buckets.TryIncrement(101));
        Assert.Equal(0, buckets.TryIncrement(100));
        Assert.Equal(0, buckets.TryIncrement(104));

        Assert.Equal(100, buckets.Offset);
        Assert.Equal(5, buckets.Size);

        Assert.Equal(1, buckets.TryIncrement(99));
        Assert.Equal(1, buckets.TryIncrement(105));
    }

    [Fact]
    public void NegativeInsertions()
    {
        var buckets = new CircularBufferBuckets(5);

        Assert.Equal(0, buckets.TryIncrement(2));
        Assert.Equal(0, buckets.TryIncrement(0));
        Assert.Equal(0, buckets.TryIncrement(-2));
        Assert.Equal(0, buckets.TryIncrement(1));
        Assert.Equal(0, buckets.TryIncrement(-1));

        Assert.Equal(-2, buckets.Offset);
        Assert.Equal(5, buckets.Size);

        Assert.Equal(1, buckets.TryIncrement(3));
        Assert.Equal(1, buckets.TryIncrement(-3));
    }

    [Fact]
    public void IntegerOverflow()
    {
        var buckets = new CircularBufferBuckets(2);

        Assert.Equal(0, buckets.TryIncrement(int.MaxValue));

        Assert.Equal(int.MaxValue, buckets.Offset);
        Assert.Equal(1, buckets.Size);

        Assert.Equal(30, buckets.TryIncrement(1));
        Assert.Equal(30, buckets.TryIncrement(0));
        Assert.Equal(31, buckets.TryIncrement(-1));
        Assert.Equal(31, buckets.TryIncrement(int.MinValue + 1));
        Assert.Equal(31, buckets.TryIncrement(int.MinValue));
    }

    [Fact]
    public void IndexOperations()
    {
        var buckets = new CircularBufferBuckets(5);

        buckets.TryIncrement(2);
        buckets.TryIncrement(2);
        buckets.TryIncrement(2);
        buckets.TryIncrement(2);
        buckets.TryIncrement(2);
        buckets.TryIncrement(0);
        buckets.TryIncrement(0);
        buckets.TryIncrement(0);
        buckets.TryIncrement(-2);
        buckets.TryIncrement(1);
        buckets.TryIncrement(1);
        buckets.TryIncrement(1);
        buckets.TryIncrement(1);
        buckets.TryIncrement(-1);
        buckets.TryIncrement(-1);

        Assert.Equal(-2, buckets.Offset);

        Assert.Equal(1, buckets[-2]);
        Assert.Equal(2, buckets[-1]);
        Assert.Equal(3, buckets[0]);
        Assert.Equal(4, buckets[1]);
        Assert.Equal(5, buckets[2]);
    }

    [Fact]
    public void ScaleDownCapacity1()
    {
        var buckets = new CircularBufferBuckets(1);

        buckets.ScaleDown(1);
        buckets.ScaleDown(2);
        buckets.ScaleDown(3);
        buckets.ScaleDown(4);

        buckets.TryIncrement(0);

        Assert.Equal(0, buckets.Offset);
        Assert.Equal(1, buckets.Size);
        Assert.Equal(1, buckets[0]);
    }

    [Fact]
    public void ScaleDownIntMaxValue()
    {
        var buckets = new CircularBufferBuckets(1);

        buckets.TryIncrement(int.MaxValue);

        Assert.Equal(int.MaxValue, buckets.Offset);

        buckets.ScaleDown(1);

        Assert.Equal(0x3FFFFFFF, buckets.Offset);
        Assert.Equal(1, buckets[0x3FFFFFFF]);
    }

    [Fact]
    public void ScaleDownIntMinValue()
    {
        var buckets = new CircularBufferBuckets(1);

        buckets.TryIncrement(int.MinValue);

        Assert.Equal(int.MinValue, buckets.Offset);

        buckets.ScaleDown(1);

        Assert.Equal(-0x40000000, buckets.Offset);
        Assert.Equal(1, buckets[-0x40000000]);
    }

    [Fact]
    public void ScaleDownCapacity2()
    {
        var buckets = new CircularBufferBuckets(2);

        buckets.TryIncrement(int.MinValue, 2);
        buckets.TryIncrement(int.MinValue + 1);
        buckets.ScaleDown(1);

        Assert.Equal(1, buckets.Size);
        Assert.Equal(3, buckets[buckets.Offset]);

        buckets = new CircularBufferBuckets(2);

        buckets.TryIncrement(int.MaxValue - 1, 2);
        buckets.TryIncrement(int.MaxValue);
        buckets.ScaleDown(1);

        Assert.Equal(1, buckets.Size);
        Assert.Equal(3, buckets[buckets.Offset]);
        Assert.Equal(0, buckets[buckets.Offset + 1]);

        buckets = new CircularBufferBuckets(2);

        buckets.TryIncrement(int.MaxValue - 2, 2);
        buckets.TryIncrement(int.MaxValue - 1);
        buckets.ScaleDown(1);

        Assert.Equal(2, buckets.Size);
        Assert.Equal(2, buckets[buckets.Offset]);
        Assert.Equal(1, buckets[buckets.Offset + 1]);
    }

    [Fact]
    public void ScaleDownCapacity3()
    {
        var buckets = new CircularBufferBuckets(3);

        buckets.TryIncrement(0, 2);
        buckets.TryIncrement(1, 4);
        buckets.TryIncrement(2, 8);
        buckets.ScaleDown(1);

        Assert.Equal(0, buckets.Offset);
        Assert.Equal(2, buckets.Size);
        Assert.Equal(6, buckets[buckets.Offset]);
        Assert.Equal(8, buckets[buckets.Offset + 1]);

        buckets = new CircularBufferBuckets(3);

        buckets.TryIncrement(1, 2);
        buckets.TryIncrement(2, 4);
        buckets.TryIncrement(3, 8);
        buckets.ScaleDown(1);

        Assert.Equal(0, buckets.Offset);
        Assert.Equal(2, buckets.Size);
        Assert.Equal(2, buckets[buckets.Offset]);
        Assert.Equal(12, buckets[buckets.Offset + 1]);

        buckets = new CircularBufferBuckets(3);

        buckets.TryIncrement(2, 2);
        buckets.TryIncrement(3, 4);
        buckets.TryIncrement(4, 8);
        buckets.ScaleDown(1);

        Assert.Equal(1, buckets.Offset);
        Assert.Equal(2, buckets.Size);
        Assert.Equal(6, buckets[buckets.Offset]);
        Assert.Equal(8, buckets[buckets.Offset + 1]);

        buckets = new CircularBufferBuckets(3);

        buckets.TryIncrement(3, 2);
        buckets.TryIncrement(4, 4);
        buckets.TryIncrement(5, 8);
        buckets.ScaleDown(1);

        Assert.Equal(1, buckets.Offset);
        Assert.Equal(2, buckets.Size);
        Assert.Equal(2, buckets[buckets.Offset]);
        Assert.Equal(12, buckets[buckets.Offset + 1]);

        buckets = new CircularBufferBuckets(3);

        buckets.TryIncrement(4, 2);
        buckets.TryIncrement(5, 4);
        buckets.TryIncrement(6, 8);
        buckets.ScaleDown(1);

        Assert.Equal(2, buckets.Offset);
        Assert.Equal(2, buckets.Size);
        Assert.Equal(6, buckets[buckets.Offset]);
        Assert.Equal(8, buckets[buckets.Offset + 1]);

        buckets = new CircularBufferBuckets(3);

        buckets.TryIncrement(5, 2);
        buckets.TryIncrement(6, 4);
        buckets.TryIncrement(7, 8);
        buckets.ScaleDown(1);

        Assert.Equal(2, buckets.Offset);
        Assert.Equal(2, buckets.Size);
        Assert.Equal(2, buckets[buckets.Offset]);
        Assert.Equal(12, buckets[buckets.Offset + 1]);
    }

    [Fact]
    public void ScaleDownCapacity4()
    {
        var buckets = new CircularBufferBuckets(4);

        buckets.TryIncrement(0, 2);
        buckets.TryIncrement(1, 4);
        buckets.TryIncrement(2, 8);
        buckets.TryIncrement(2, 16);
        buckets.ScaleDown(1);

        Assert.Equal(0, buckets.Offset);
        Assert.Equal(2, buckets.Size);
        Assert.Equal(6, buckets[buckets.Offset]);
        Assert.Equal(24, buckets[buckets.Offset + 1]);

        buckets = new CircularBufferBuckets(4);

        buckets.TryIncrement(1, 2);
        buckets.TryIncrement(2, 4);
        buckets.TryIncrement(3, 8);
        buckets.TryIncrement(4, 16);
        buckets.ScaleDown(1);

        Assert.Equal(0, buckets.Offset);
        Assert.Equal(3, buckets.Size);
        Assert.Equal(2, buckets[buckets.Offset]);
        Assert.Equal(12, buckets[buckets.Offset + 1]);
        Assert.Equal(16, buckets[buckets.Offset + 2]);

        buckets = new CircularBufferBuckets(4);

        buckets.TryIncrement(2, 2);
        buckets.TryIncrement(3, 4);
        buckets.TryIncrement(4, 8);
        buckets.TryIncrement(5, 16);
        buckets.ScaleDown(1);

        Assert.Equal(1, buckets.Offset);
        Assert.Equal(2, buckets.Size);
        Assert.Equal(6, buckets[buckets.Offset]);
        Assert.Equal(24, buckets[buckets.Offset + 1]);

        buckets = new CircularBufferBuckets(4);

        buckets.TryIncrement(3, 2);
        buckets.TryIncrement(4, 4);
        buckets.TryIncrement(5, 8);
        buckets.TryIncrement(6, 16);
        buckets.ScaleDown(1);

        Assert.Equal(1, buckets.Offset);
        Assert.Equal(3, buckets.Size);
        Assert.Equal(2, buckets[buckets.Offset]);
        Assert.Equal(12, buckets[buckets.Offset + 1]);
        Assert.Equal(16, buckets[buckets.Offset + 2]);

        buckets = new CircularBufferBuckets(4);

        buckets.TryIncrement(4, 2);
        buckets.TryIncrement(5, 4);
        buckets.TryIncrement(6, 8);
        buckets.TryIncrement(7, 16);
        buckets.ScaleDown(1);

        Assert.Equal(2, buckets.Offset);
        Assert.Equal(2, buckets.Size);
        Assert.Equal(6, buckets[buckets.Offset]);
        Assert.Equal(24, buckets[buckets.Offset + 1]);

        buckets = new CircularBufferBuckets(4);

        buckets.TryIncrement(5, 2);
        buckets.TryIncrement(6, 4);
        buckets.TryIncrement(7, 8);
        buckets.TryIncrement(8, 16);
        buckets.ScaleDown(1);

        Assert.Equal(2, buckets.Offset);
        Assert.Equal(3, buckets.Size);
        Assert.Equal(2, buckets[buckets.Offset]);
        Assert.Equal(12, buckets[buckets.Offset + 1]);
        Assert.Equal(16, buckets[buckets.Offset + 2]);

        buckets = new CircularBufferBuckets(4);

        buckets.TryIncrement(6, 2);
        buckets.TryIncrement(7, 4);
        buckets.TryIncrement(8, 8);
        buckets.TryIncrement(9, 16);
        buckets.ScaleDown(1);

        Assert.Equal(3, buckets.Offset);
        Assert.Equal(2, buckets.Size);
        Assert.Equal(6, buckets[buckets.Offset]);
        Assert.Equal(24, buckets[buckets.Offset + 1]);

        buckets = new CircularBufferBuckets(4);

        buckets.TryIncrement(7, 2);
        buckets.TryIncrement(8, 4);
        buckets.TryIncrement(9, 8);
        buckets.TryIncrement(10, 16);
        buckets.ScaleDown(1);

        Assert.Equal(3, buckets.Offset);
        Assert.Equal(3, buckets.Size);
        Assert.Equal(2, buckets[buckets.Offset]);
        Assert.Equal(12, buckets[buckets.Offset + 1]);
        Assert.Equal(16, buckets[buckets.Offset + 2]);
    }
}
