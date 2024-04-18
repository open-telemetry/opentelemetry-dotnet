// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

internal sealed class LookupData
{
    public bool DeferredReclaim;
    public int Index;
    public Tags SortedTags;
    public Tags GivenTags;

    public LookupData(int index, in Tags sortedTags, in Tags givenTags)
    {
        this.DeferredReclaim = false;
        this.Index = index;
        this.SortedTags = sortedTags;
        this.GivenTags = givenTags;
    }
}
