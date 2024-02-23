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
        if (this.ResetOnCollect)
        {
            for (int i = 0; i < this.runningExemplars.Length; i++)
            {
                ref var running = ref this.runningExemplars[i];
                if (running.IsUpdated())
                {
                    running.Copy(ref this.snapshotExemplars[i]);
                    running.Reset();
                }
                else
                {
                    this.snapshotExemplars[i].Reset();
                }
            }

            this.OnReset();
        }
        else
        {
            for (int i = 0; i < this.runningExemplars.Length; i++)
            {
                ref var running = ref this.runningExemplars[i];
                if (running.IsUpdated())
                {
                    running.Copy(ref this.snapshotExemplars[i]);
                }
                else
                {
                    this.snapshotExemplars[i].Reset();
                }
            }
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

    protected virtual void OnReset()
    {
    }

    protected void UpdateExemplar<T>(int exemplarIndex, in ExemplarMeasurement<T> measurement)
        where T : struct
    {
        this.runningExemplars[exemplarIndex].Update(in measurement);
    }
}
