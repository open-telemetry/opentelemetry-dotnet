// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Internal;

/// <summary>
/// The items discarded due to span limits while a batch is read.
/// </summary>
internal sealed class SpanLimitTotals
{
    private Activity? lastAffectedSpan;

    internal long AffectedSpanCount { get; private set; }

    internal long DroppedAttributeCount { get; private set; }

    internal long DroppedEventCount { get; private set; }

    internal long DroppedLinkCount { get; private set; }

    internal void RecordDroppedAttributes(Activity span, int count)
    {
        if (count > 0)
        {
            this.MarkAffected(span);
            this.DroppedAttributeCount += count;
        }
    }

    internal void RecordDroppedEvents(Activity span, int count)
    {
        if (count > 0)
        {
            this.MarkAffected(span);
            this.DroppedEventCount += count;
        }
    }

    internal void RecordDroppedLinks(Activity span, int count)
    {
        if (count > 0)
        {
            this.MarkAffected(span);
            this.DroppedLinkCount += count;
        }
    }

    internal void Reset()
    {
        this.lastAffectedSpan = null;
        this.AffectedSpanCount = 0;
        this.DroppedAttributeCount = 0;
        this.DroppedEventCount = 0;
        this.DroppedLinkCount = 0;
    }

    private void MarkAffected(Activity span)
    {
        // Exporters read one span at a time, so a span is counted once however many of its
        // collections dropped items.
        if (!ReferenceEquals(span, this.lastAffectedSpan))
        {
            this.lastAffectedSpan = span;
            this.AffectedSpanCount++;
        }
    }
}
