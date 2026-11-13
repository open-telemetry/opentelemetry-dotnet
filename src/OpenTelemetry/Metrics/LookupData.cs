// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

internal sealed class LookupData
{
    public bool DeferredReclaim;
    public int Index;
    public Tags SortedTags;
    public Tags GivenTags;

    // The delta collect cycle during which the MetricPoint was created.
    public int CollectCycle;

    public LookupData(int index, in Tags sortedTags, in Tags givenTags, int collectCycle)
    {
        this.DeferredReclaim = false;
        this.Index = index;
        this.SortedTags = sortedTags;
        this.GivenTags = givenTags;
        this.CollectCycle = collectCycle;
    }
}
