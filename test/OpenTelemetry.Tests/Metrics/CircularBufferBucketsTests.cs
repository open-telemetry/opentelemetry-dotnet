// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics.Tests;

public class CircularBufferBucketsTests
{
    [Fact]
    public void ConstructorThrowsOnInvalidCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBufferBuckets(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBufferBuckets(1));
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
    public void ResetClearsStateBeforePostResetInsertion()
    {
        var buckets = new CircularBufferBuckets(5);

        buckets.TryIncrement(10);
        buckets.TryIncrement(11);

        buckets.Reset();

        Assert.Equal(0, buckets.Offset);
        Assert.Equal(0, buckets.Size);

        var result = buckets.TryIncrement(20);

        Assert.Equal(
            (Result: 0, Offset: 20, Size: 1, Count: 1L),
            (Result: result, buckets.Offset, buckets.Size, Count: buckets[20]));
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

    [Theory]
    [InlineData(5, 0, 4)] // Contiguous storage (first == size).
    [InlineData(5, 3, 4)] // Wrapped storage (first < size).
    [InlineData(5, 3, 5)] // Full-capacity wrapped storage.
    public void CopyHandlesContiguousAndWrappedStorage(int capacity, int start, int count)
    {
        var buckets = new CircularBufferBuckets(capacity);

        for (var i = 0; i < count; i++)
        {
            buckets.TryIncrement(start + i, i + 1);
        }

        var copy = new long[capacity];
        buckets.Copy(copy);

        var expected = new long[capacity];
        for (var i = 0; i < count; i++)
        {
            expected[i] = i + 1;
        }

        Assert.Equal(expected, copy);
    }

    [Theory]
    [InlineData(5, 0, 5, 1)] // Even first index, full capacity.
    [InlineData(5, 1, 5, 1)] // Odd first index, full capacity.
    [InlineData(5, 2, 4, 1)] // Even first index, partial capacity.
    [InlineData(6, 3, 4, 1)] // Odd first index, partial capacity.
    [InlineData(8, 0, 8, 2)] // Two levels at once.
    [InlineData(8, 5, 8, 3)] // Three levels at once, odd first index.
    [InlineData(5, -3, 5, 1)] // Negative indexes.
    public void ScaleDownHandlesWrappedStorage(int capacity, int start, int count, int level)
    {
        var buckets = new CircularBufferBuckets(capacity);
        var expected = new Dictionary<int, long>();

        for (var i = count - 1; i >= 0; i--)
        {
            var index = start + i;
            var value = 1L << i;

            Assert.Equal(0, buckets.TryIncrement(index, value));

            var scaledIndex = index >> level;
            expected[scaledIndex] = expected.TryGetValue(scaledIndex, out var sum) ? sum + value : value;
        }

        buckets.ScaleDown(level);

        Assert.Equal(start >> level, buckets.Offset);
        Assert.Equal(expected.Count, buckets.Size);

        var copy = new long[capacity];
        buckets.Copy(copy);

        for (var i = 0; i < buckets.Size; i++)
        {
            var index = buckets.Offset + i;

            Assert.Equal(expected[index], buckets[index]);
            Assert.Equal(expected[index], copy[i]);
        }

        for (var i = buckets.Size; i < capacity; i++)
        {
            Assert.Equal(0, copy[i]);
        }

        var below = buckets.Offset - 1;
        var above = buckets.Offset + buckets.Size;

        Assert.Equal(0, buckets.TryIncrement(below, 100));
        Assert.Equal(0, buckets.TryIncrement(above, 200));

        Assert.Equal(below, buckets.Offset);
        Assert.Equal(expected.Count + 2, buckets.Size);
        Assert.Equal(100, buckets[below]);
        Assert.Equal(200, buckets[above]);

        foreach (var pair in expected)
        {
            Assert.Equal(pair.Value, buckets[pair.Key]);
        }
    }
}
