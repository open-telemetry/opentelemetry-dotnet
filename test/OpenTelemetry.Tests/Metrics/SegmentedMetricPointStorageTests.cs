// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class SegmentedMetricPointStorageTests
{
    [Fact]
    public void AllocatesChunksLazilyAndCapsAllocatedCapacityAtMaximum()
    {
        var maxMetricPointCount = SegmentedMetricPointStorage.ChunkSize + 3;
        var storage = new SegmentedMetricPointStorage(maxMetricPointCount);

        Assert.Equal(0, storage.AllocatedCapacity);

        storage.EnsureAllocated(0);

        Assert.Equal(SegmentedMetricPointStorage.ChunkSize, storage.AllocatedCapacity);

        ref var metricPoint = ref storage.GetMetricPoint(0);
        metricPoint.ReferenceCount = 42;

        storage.EnsureAllocated(SegmentedMetricPointStorage.ChunkSize);

        Assert.Equal(maxMetricPointCount, storage.AllocatedCapacity);
        Assert.Equal(42, storage.GetMetricPoint(0).ReferenceCount);

        storage.EnsureAllocated(SegmentedMetricPointStorage.ChunkSize + 2);

        Assert.Equal(maxMetricPointCount, storage.AllocatedCapacity);
    }

    [Fact]
    public void RepeatedAllocationInSameChunkDoesNotIncreaseAllocatedCapacity()
    {
        var storage = new SegmentedMetricPointStorage(SegmentedMetricPointStorage.ChunkSize * 3);

        storage.EnsureAllocated(0);

        Assert.Equal(SegmentedMetricPointStorage.ChunkSize, storage.AllocatedCapacity);

        storage.EnsureAllocated(0);
        storage.EnsureAllocated(SegmentedMetricPointStorage.ChunkSize - 1);

        Assert.Equal(SegmentedMetricPointStorage.ChunkSize, storage.AllocatedCapacity);

        storage.EnsureAllocated(SegmentedMetricPointStorage.ChunkSize);

        Assert.Equal(SegmentedMetricPointStorage.ChunkSize * 2, storage.AllocatedCapacity);
    }

    [Fact]
    public void FirstChunkAllocationCapsAllocatedCapacityWhenMaximumIsSmallerThanChunk()
    {
        var maxMetricPointCount = 3;
        var storage = new SegmentedMetricPointStorage(maxMetricPointCount);

        storage.EnsureAllocated(maxMetricPointCount - 1);

        Assert.Equal(maxMetricPointCount, storage.AllocatedCapacity);

        ref var metricPoint = ref storage.GetMetricPoint(maxMetricPointCount - 1);
        metricPoint.ReferenceCount = 7;

        Assert.Equal(7, storage.GetMetricPoint(maxMetricPointCount - 1).ReferenceCount);
    }

    [Fact]
    public void EnsureAllocatedIsSafeForConcurrentChunkAllocation()
    {
        var maxMetricPointCount = (SegmentedMetricPointStorage.ChunkSize * 3) + 13;
        var storage = new SegmentedMetricPointStorage(maxMetricPointCount);

        Parallel.For(0, maxMetricPointCount, storage.EnsureAllocated);

        Assert.Equal(maxMetricPointCount, storage.AllocatedCapacity);
    }

    [Fact]
    public void EnsureAllocatedIsSafeForConcurrentSameChunkAllocation()
    {
        var storage = new SegmentedMetricPointStorage(SegmentedMetricPointStorage.ChunkSize * 4);

        Parallel.For(0, 1024, _ => storage.EnsureAllocated(0));

        Assert.Equal(SegmentedMetricPointStorage.ChunkSize, storage.AllocatedCapacity);
    }
}
