// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Internal;

/// <summary>
/// The limit rules shared by the span event and span link readers.
/// </summary>
/// <typeparam name="T"><see cref="ActivityEvent"/> or <see cref="ActivityLink"/>.</typeparam>
internal struct SpanItemReaderCore<T>
    where T : struct
{
    private readonly SpanLimitTotals? totals;
    private readonly Activity span;
    private readonly int countLimit;
    private readonly bool isLinks;
    private Activity.Enumerator<T> enumerator;
    private T current;
    private int readCount;
    private int droppedCount;
    private bool hasCurrent;
    private bool completed;

    internal SpanItemReaderCore(
        SpanLimitTotals? totals,
        Activity span,
        Activity.Enumerator<T> enumerator,
        int countLimit,
        bool isLinks)
    {
        this.totals = totals;
        this.span = span;
        this.enumerator = enumerator;
        this.countLimit = countLimit;
        this.isLinks = isLinks;
    }

    /// <summary>
    /// Gets the item returned by the last successful <see cref="TryRead"/>.
    /// </summary>
    internal readonly T Current
        => this.hasCurrent
            ? this.current
            : throw new InvalidOperationException("Attributes can only be read for the item returned by the last successful TryRead.");

    internal int DroppedCount
    {
        get
        {
            this.EnsureCompleted();
            return this.droppedCount;
        }
    }

    internal bool TryRead(out T item)
    {
        if (!this.completed && this.enumerator.MoveNext())
        {
            if (this.readCount < this.countLimit)
            {
                this.readCount++;
                this.current = this.enumerator.Current;
                this.hasCurrent = true;
                item = this.current;
                return true;
            }

            this.droppedCount++;
        }

        this.EnsureCompleted();
        item = default;
        return false;
    }

    internal void EnsureCompleted()
    {
        if (this.completed)
        {
            return;
        }

        // Items not read, including any beyond the count limit, are not exported.
        while (this.enumerator.MoveNext())
        {
            this.droppedCount++;
        }

        this.completed = true;
        this.hasCurrent = false;
        this.current = default;

        if (this.isLinks)
        {
            this.totals?.RecordDroppedLinks(this.span, this.droppedCount);
        }
        else
        {
            this.totals?.RecordDroppedEvents(this.span, this.droppedCount);
        }
    }
}
