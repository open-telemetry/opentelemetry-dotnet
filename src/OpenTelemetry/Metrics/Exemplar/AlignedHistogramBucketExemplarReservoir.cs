// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

/// <summary>
/// The AlignedHistogramBucketExemplarReservoir implementation.
/// </summary>
internal sealed class AlignedHistogramBucketExemplarReservoir : ExemplarReservoir
{
    private readonly AggregatorStore parentAggregatorStore;
    private readonly Exemplar[] bufferA;
    private readonly Exemplar[] bufferB;
    private Exemplar[] activeBuffer;

    public AlignedHistogramBucketExemplarReservoir(AggregatorStore parentAggregatorStore, int length)
    {
        Debug.Assert(parentAggregatorStore != null, "parentAggregatorStore was null");

        this.parentAggregatorStore = parentAggregatorStore;
        this.bufferA = new Exemplar[length + 1];
        this.bufferB = new Exemplar[length + 1];
        this.activeBuffer = this.bufferA;
    }

    public override void Offer(long value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        this.OfferAtBoundary(value, tags, this.FindBucketIndexForValue(value));
    }

    public override void Offer(double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        this.OfferAtBoundary(value, tags, this.FindBucketIndexForValue(value));
    }

    public override ReadOnlyExemplarCollection Collect()
    {
        var currentBuffer = this.activeBuffer;

        this.activeBuffer = currentBuffer == this.bufferA
            ? this.bufferB
            : this.bufferA;

        if (this.parentAggregatorStore.OutputDelta)
        {
            for (int i = 0; i < this.activeBuffer.Length; i++)
            {
                this.activeBuffer[i].Reset();
            }
        }

        return new(currentBuffer);
    }

    protected int FindBucketIndexForValue(double value)
    {

    }

    private void OfferAtBoundary<T>(T value, ReadOnlySpan<KeyValuePair<string, object?>> tags, int bucketIndex)
        where T : notnull
    {
        ref var exemplar = ref this.activeBuffer[bucketIndex];

        exemplar.Timestamp = DateTimeOffset.UtcNow;

        if (typeof(T) == typeof(long))
        {
            exemplar.LongValue = (long)(object)value;
        }
        else if (typeof(T) == typeof(double))
        {
            exemplar.DoubleValue = (double)(object)value;
        }
        else
        {
            Debug.Fail("Invalid value type");
            exemplar.DoubleValue = Convert.ToDouble((object)value);
        }

        var currentActivity = Activity.Current;
        if (currentActivity != null)
        {
            exemplar.TraceId = currentActivity.TraceId;
            exemplar.SpanId = currentActivity.SpanId;
        }
        else
        {
            exemplar.TraceId = default;
            exemplar.SpanId = default;
        }

        exemplar.StoreFilteredTags(tags);
    }
}
