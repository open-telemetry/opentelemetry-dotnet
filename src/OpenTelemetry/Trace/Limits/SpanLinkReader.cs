// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Reads the links of a span with the link count limit applied.
/// </summary>
internal ref struct SpanLinkReader
{
    private readonly Activity activity;
    private readonly SpanLimitTotals? totals;
    private readonly int attributeCountLimit;
    private readonly int? valueLengthLimit;
    private SpanItemReaderCore<ActivityLink> core;

    internal SpanLinkReader(
        Activity activity,
        SpanLimitTotals? totals,
        SpanItemReaderCore<ActivityLink> core,
        int attributeCountLimit,
        int? valueLengthLimit)
    {
        this.activity = activity;
        this.totals = totals;
        this.core = core;
        this.attributeCountLimit = attributeCountLimit;
        this.valueLengthLimit = valueLengthLimit;
    }

    /// <summary>
    /// Gets the number of links dropped. If reading stopped early, the remaining links are
    /// counted first.
    /// </summary>
    public int DroppedCount => this.core.DroppedCount;

    /// <summary>
    /// Reads the next link within the limit.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <returns><see langword="true"/> if a link was read; otherwise, <see langword="false"/>.</returns>
    public bool TryRead(out ActivityLink link)
        => this.core.TryRead(out link);

    /// <summary>
    /// Creates a reader for the attributes of the link just read, with the per-link attribute limit applied.
    /// </summary>
    /// <returns><see cref="AttributeReader"/>.</returns>
    public readonly AttributeReader CreateAttributeReader()
        => new(
            new AttributeReaderCore(
                this.totals,
                this.activity,
                this.core.Current.EnumerateTagObjects(),
                this.attributeCountLimit,
                captureStatusTags: false),
            this.valueLengthLimit);
}
