// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Reads the events of a span with the event count limit applied.
/// </summary>
internal ref struct SpanEventReader
{
    private readonly Activity activity;
    private readonly SpanLimitBatch batch;
    private readonly int attributeCountLimit;
    private readonly int? valueLengthLimit;
    private SpanItemReaderCore<ActivityEvent> core;

    internal SpanEventReader(
        Activity activity,
        SpanLimitBatch batch,
        SpanItemReaderCore<ActivityEvent> core,
        int attributeCountLimit,
        int? valueLengthLimit)
    {
        this.activity = activity;
        this.batch = batch;
        this.core = core;
        this.attributeCountLimit = attributeCountLimit;
        this.valueLengthLimit = valueLengthLimit;
    }

    /// <summary>
    /// Gets the number of events dropped. If reading stopped early, the remaining events are
    /// counted first.
    /// </summary>
    public int DroppedCount => this.core.DroppedCount;

    /// <summary>
    /// Reads the next event within the limit.
    /// </summary>
    /// <param name="activityEvent">The event.</param>
    /// <returns><see langword="true"/> if an event was read; otherwise, <see langword="false"/>.</returns>
    public bool TryRead(out ActivityEvent activityEvent)
        => this.core.TryRead(out activityEvent);

    /// <summary>
    /// Creates a reader for the attributes of the event just read, with the per-event attribute limit applied.
    /// </summary>
    /// <returns><see cref="AttributeReader"/>.</returns>
    public readonly AttributeReader CreateAttributeReader()
        => new(
            new AttributeReaderCore(
                this.batch,
                this.activity,
                this.core.Current.EnumerateTagObjects(),
                this.attributeCountLimit,
                captureStatusTags: false),
            this.valueLengthLimit);
}
