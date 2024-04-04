// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

internal abstract class FixedSizeExemplarReservoir : ExemplarReservoir
{
    private readonly Exemplar[] runningExemplars;
    private readonly Exemplar[] snapshotExemplars;

    protected FixedSizeExemplarReservoir(int capacity)
    {
        Guard.ThrowIfOutOfRange(capacity, min: 1);

        this.runningExemplars = new Exemplar[capacity];
        this.snapshotExemplars = new Exemplar[capacity];
        this.Capacity = capacity;
    }

    internal int Capacity { get; }

    /// <summary>
    /// Collects all the exemplars accumulated by the Reservoir.
    /// </summary>
    /// <returns><see cref="ReadOnlyExemplarCollection"/>.</returns>
    public sealed override ReadOnlyExemplarCollection Collect()
    {
        var runningExemplars = this.runningExemplars;

        for (int i = 0; i < runningExemplars.Length; i++)
        {
            ref var running = ref runningExemplars[i];

            running.Collect(
                ref this.snapshotExemplars[i],
                reset: this.ResetOnCollect);
        }

        this.OnCollected();

        return new(this.snapshotExemplars);
    }

    internal sealed override void Initialize(AggregatorStore aggregatorStore)
    {
        var viewDefinedTagKeys = aggregatorStore.TagKeysInteresting;

        for (int i = 0; i < this.runningExemplars.Length; i++)
        {
            this.runningExemplars[i].ViewDefinedTagKeys = viewDefinedTagKeys;
            this.snapshotExemplars[i].ViewDefinedTagKeys = viewDefinedTagKeys;
        }

        base.Initialize(aggregatorStore);
    }

    protected virtual void OnCollected()
    {
    }

    protected void UpdateExemplar<T>(int exemplarIndex, in ExemplarMeasurement<T> measurement)
        where T : struct
    {
        this.runningExemplars[exemplarIndex].Update(in measurement);
    }
}
