// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Metrics;

internal sealed class SegmentedMetricPointStorage
{
    internal const int ChunkSize = 256;

    private const int ChunkShift = 8;
    private const int ChunkMask = ChunkSize - 1;

    private readonly MetricPoint[]?[] chunks;
    private readonly int maxMetricPointCount;
    private readonly Lock allocationLock = new();

    private int allocatedChunkCount;

    internal SegmentedMetricPointStorage(int maxMetricPointCount)
    {
        this.maxMetricPointCount = maxMetricPointCount;
        this.chunks = new MetricPoint[]?[(maxMetricPointCount + ChunkMask) >> ChunkShift];
    }

    internal int AllocatedCapacity => Math.Min(Volatile.Read(ref this.allocatedChunkCount) * ChunkSize, this.maxMetricPointCount);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref MetricPoint GetMetricPoint(int index)
    {
        var chunk = Volatile.Read(ref this.chunks[index >> ChunkShift]);
        Debug.Assert(chunk != null, "Metric point chunk was not allocated.");

        return ref chunk![index & ChunkMask];
    }

    internal void EnsureAllocated(int index)
    {
        var chunkIndex = index >> ChunkShift;
        if (Volatile.Read(ref this.chunks[chunkIndex]) != null)
        {
            return;
        }

        lock (this.allocationLock)
        {
            if (this.chunks[chunkIndex] == null)
            {
                Volatile.Write(ref this.chunks[chunkIndex], new MetricPoint[ChunkSize]);
                Volatile.Write(ref this.allocatedChunkCount, this.allocatedChunkCount + 1);
            }
        }
    }
}
