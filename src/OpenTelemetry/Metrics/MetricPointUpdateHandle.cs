// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace OpenTelemetry.Metrics;

internal sealed class MetricPointUpdateHandle
{
    private const int UninitializedMetricPointIndex = int.MinValue;

    private readonly AggregatorStore aggregatorStore;
    private readonly Lock initializationLock = new();
    private readonly KeyValuePair<string, object?>[] tags;
    private int metricPointIndex = UninitializedMetricPointIndex;

    internal MetricPointUpdateHandle(
        AggregatorStore aggregatorStore,
        KeyValuePair<string, object?>[] tags)
    {
        this.aggregatorStore = aggregatorStore;
        this.tags = tags;
    }

    internal ReadOnlySpan<KeyValuePair<string, object?>> Tags => this.tags;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Update(long value)
        => this.aggregatorStore.UpdateBound(this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Update(double value)
        => this.aggregatorStore.UpdateBound(this, value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetMetricPointIndex()
    {
        var index = Volatile.Read(ref this.metricPointIndex);
        return index == UninitializedMetricPointIndex
            ? this.InitializeMetricPointIndex()
            : index;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private int InitializeMetricPointIndex()
    {
        lock (this.initializationLock)
        {
            var index = this.metricPointIndex;
            if (index == UninitializedMetricPointIndex)
            {
                index = this.aggregatorStore.ResolveBoundMetricPoint(this.tags);
                if (index != AggregatorStore.TransientMetricPointLookupFailure)
                {
                    Volatile.Write(ref this.metricPointIndex, index);
                }
            }

            return index;
        }
    }
}
