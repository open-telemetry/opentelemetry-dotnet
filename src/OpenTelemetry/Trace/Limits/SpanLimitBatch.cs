// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Reads spans with limits applied while one batch is exported.
/// </summary>
/// <remarks>
/// Readers are only available from a batch, so what they drop is always recorded. Disposing the
/// batch reports the drops, through a rate limited warning.
/// <para/>
/// Read all of a span's collections before moving to the next span. A span is counted once in the
/// warning's affected span count only while its drops are recorded one after another; if another
/// span's drops come in between, it is counted again.
/// </remarks>
internal ref struct SpanLimitBatch
{
    private readonly SpanLimiter limiter;
    private SpanLimitTotals? totals;

    internal SpanLimitBatch(SpanLimiter limiter, SpanLimitTotals totals)
    {
        this.limiter = limiter;
        this.totals = totals;
    }

    /// <summary>
    /// Gets the maximum number of instrumentation scope attributes. Scope attributes use the
    /// general attribute limits.
    /// </summary>
    /// <remarks>
    /// Scope attributes have no reader yet. Exporters apply this limit while writing them.
    /// </remarks>
    public readonly int ScopeAttributeCountLimit => this.limiter.ScopeAttributeCountLimit;

    /// <summary>
    /// Gets the maximum length the writer should truncate instrumentation scope attribute values
    /// to, or <see langword="null"/> for no limit.
    /// </summary>
    public readonly int? ScopeAttributeValueLengthLimit => this.limiter.ScopeAttributeValueLengthLimit;

    /// <summary>
    /// Creates a reader for the attributes of a span with the span attribute limits applied.
    /// </summary>
    /// <param name="activity">The span.</param>
    /// <returns><see cref="SpanAttributeReader"/>.</returns>
    public readonly SpanAttributeReader CreateAttributeReader(Activity activity)
    {
        Guard.ThrowIfNull(activity);

        return new(
            activity,
            new AttributeReaderCore(
                this.totals,
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
    public readonly SpanEventReader CreateEventReader(Activity activity)
    {
        Guard.ThrowIfNull(activity);

        return new(
            activity,
            this.totals,
            new SpanItemReaderCore<ActivityEvent>(
                this.totals,
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
    public readonly SpanLinkReader CreateLinkReader(Activity activity)
    {
        Guard.ThrowIfNull(activity);

        return new(
            activity,
            this.totals,
            new SpanItemReaderCore<ActivityLink>(
                this.totals,
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
    public readonly void Reset()
        => this.totals?.Reset();

    /// <summary>
    /// Reports the drops recorded in this batch.
    /// </summary>
    public void Dispose()
    {
        var totals = this.totals;
        if (totals != null)
        {
            this.totals = null;
            this.limiter.CompleteBatch(totals);
        }
    }
}
