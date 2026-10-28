// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Reads spans with limits applied while one batch is exported.
/// </summary>
/// <remarks>
/// Read each reader to the end, or read its <c>DroppedCount</c>, to count unread items as dropped.
/// Explicitly marked drops are recorded immediately. Disposing the batch reports the drops
/// through a rate limited warning, and invalidates its readers.
/// <para/>
/// Read all of a span's collections before moving to the next span. A span is counted once in the
/// warning's affected span count only while its drops are recorded one after another; if another
/// span's drops come in between, it is counted again.
/// </remarks>
internal sealed class SpanLimitBatch : IDisposable
{
    private readonly SpanLimiter limiter;
    private Activity? lastAffectedSpan;
    private int disposed;

    internal SpanLimitBatch(SpanLimiter limiter)
    {
        this.limiter = limiter;
    }

    /// <summary>
    /// Gets the maximum number of instrumentation scope attributes. Scope attributes use the
    /// general attribute limits.
    /// </summary>
    /// <remarks>
    /// Scope attributes have no reader yet. Exporters apply this limit while writing them.
    /// </remarks>
    public int ScopeAttributeCountLimit => this.limiter.ScopeAttributeCountLimit;

    /// <summary>
    /// Gets the maximum length the writer should truncate instrumentation scope attribute values
    /// to, or <see langword="null"/> for no limit.
    /// </summary>
    public int? ScopeAttributeValueLengthLimit => this.limiter.ScopeAttributeValueLengthLimit;

    internal long AffectedSpanCount { get; private set; }

    internal long DroppedAttributeCount { get; private set; }

    internal long DroppedEventCount { get; private set; }

    internal long DroppedLinkCount { get; private set; }

    /// <summary>
    /// Creates a reader for the attributes of a span with the span attribute limits applied.
    /// </summary>
    /// <param name="activity">The span.</param>
    /// <returns><see cref="SpanAttributeReader"/>.</returns>
    public SpanAttributeReader CreateAttributeReader(Activity activity)
    {
        Guard.ThrowIfNull(activity);
        this.ThrowIfDisposed();

        return new(
            activity,
            new AttributeReaderCore(
                this,
                activity,
                activity.EnumerateTagObjects(),
                this.limiter.AttributeCountLimit,
                captureStatusTags: true),
            this.limiter.AttributeValueLengthLimit);
    }

    /// <summary>
    /// Creates a reader for the events of a span with the event count limit applied.
    /// </summary>
    /// <param name="activity">The span.</param>
    /// <returns><see cref="SpanEventReader"/>.</returns>
    public SpanEventReader CreateEventReader(Activity activity)
    {
        Guard.ThrowIfNull(activity);
        this.ThrowIfDisposed();

        return new(
            activity,
            this,
            new SpanItemReaderCore<ActivityEvent>(
                this,
                activity,
                activity.EnumerateEvents(),
                this.limiter.EventCountLimit,
                isLinks: false),
            this.limiter.AttributePerEventCountLimit,
            this.limiter.AttributeValueLengthLimit);
    }

    /// <summary>
    /// Creates a reader for the links of a span with the link count limit applied.
    /// </summary>
    /// <param name="activity">The span.</param>
    /// <returns><see cref="SpanLinkReader"/>.</returns>
    public SpanLinkReader CreateLinkReader(Activity activity)
    {
        Guard.ThrowIfNull(activity);
        this.ThrowIfDisposed();

        return new(
            activity,
            this,
            new SpanItemReaderCore<ActivityLink>(
                this,
                activity,
                activity.EnumerateLinks(),
                this.limiter.LinkCountLimit,
                isLinks: true),
            this.limiter.AttributePerLinkCountLimit,
            this.limiter.AttributeValueLengthLimit);
    }

    /// <summary>
    /// Discards the drops recorded so far. Call it before reading the same spans again, for
    /// example when serialization is retried with a larger buffer.
    /// </summary>
    public void Reset()
    {
        this.ThrowIfDisposed();

        this.lastAffectedSpan = null;
        this.AffectedSpanCount = 0;
        this.DroppedAttributeCount = 0;
        this.DroppedEventCount = 0;
        this.DroppedLinkCount = 0;
    }

    /// <summary>
    /// Reports the drops recorded in this batch.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this.disposed, 1) == 0)
        {
            this.limiter.ReportBatchDrops(this);
        }
    }

    internal void ThrowIfDisposed()
    {
#if NET
        ObjectDisposedException.ThrowIf(Volatile.Read(ref this.disposed) != 0, this);
#else
        if (Volatile.Read(ref this.disposed) != 0)
        {
            throw new ObjectDisposedException(typeof(SpanLimitBatch).FullName);
        }
#endif
    }

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

    private void MarkAffected(Activity span)
    {
        if (!ReferenceEquals(span, this.lastAffectedSpan))
        {
            this.lastAffectedSpan = span;
            this.AffectedSpanCount++;
        }
    }
}
