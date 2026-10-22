// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Applies span limits for one exporter.
/// </summary>
internal sealed class SpanLimiter
{
    // Internal for now. Once approved this may be made public so that third party exporters can use it as well.

    private readonly LimitExceededWarningTracker warningTracker;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpanLimiter"/> class.
    /// </summary>
    /// <param name="spanLimitOptions">The span limits.</param>
    /// <param name="attributeLimitOptions">The general attribute limits.</param>
    /// <param name="exporterName">The exporter name, reported in drop warnings.</param>
    public SpanLimiter(SpanLimitOptions spanLimitOptions, AttributeLimitOptions attributeLimitOptions, string exporterName)
        : this(spanLimitOptions, attributeLimitOptions, exporterName, new LimitExceededWarningTracker())
    {
    }

    internal SpanLimiter(
        SpanLimitOptions spanLimitOptions,
        AttributeLimitOptions attributeLimitOptions,
        string exporterName,
        LimitExceededWarningTracker warningTracker)
    {
        Guard.ThrowIfNull(spanLimitOptions);
        Guard.ThrowIfNull(attributeLimitOptions);
        Guard.ThrowIfNullOrEmpty(exporterName);

        // The values are copied so every batch sees one consistent set. The general limits apply
        // to instrumentation scope attributes.
        this.AttributeCountLimit = spanLimitOptions.AttributeCountLimit;
        this.AttributeValueLengthLimit = spanLimitOptions.AttributeValueLengthLimit;
        this.EventCountLimit = spanLimitOptions.EventCountLimit;
        this.LinkCountLimit = spanLimitOptions.LinkCountLimit;
        this.AttributePerEventCountLimit = spanLimitOptions.AttributePerEventCountLimit;
        this.AttributePerLinkCountLimit = spanLimitOptions.AttributePerLinkCountLimit;
        this.ScopeAttributeCountLimit = attributeLimitOptions.AttributeCountLimit;
        this.ScopeAttributeValueLengthLimit = attributeLimitOptions.AttributeValueLengthLimit;
        this.ExporterName = exporterName;
        this.warningTracker = warningTracker;
    }

    /// <summary>
    /// Gets the exporter name reported in drop warnings.
    /// </summary>
    internal string ExporterName { get; }

    /// <summary>
    /// Gets the maximum number of attributes a span can have.
    /// </summary>
    internal int AttributeCountLimit { get; }

    /// <summary>
    /// Gets the maximum length the writer should truncate attribute values to, or <see langword="null"/> for no limit.
    /// </summary>
    internal int? AttributeValueLengthLimit { get; }

    /// <summary>
    /// Gets the maximum number of events a span can have.
    /// </summary>
    internal int EventCountLimit { get; }

    /// <summary>
    /// Gets the maximum number of links a span can have.
    /// </summary>
    internal int LinkCountLimit { get; }

    /// <summary>
    /// Gets the maximum number of attributes an event can have.
    /// </summary>
    internal int AttributePerEventCountLimit { get; }

    /// <summary>
    /// Gets the maximum number of attributes a link can have.
    /// </summary>
    internal int AttributePerLinkCountLimit { get; }

    /// <summary>
    /// Gets the maximum number of instrumentation scope attributes. Scope attributes use the general attribute limits.
    /// </summary>
    internal int ScopeAttributeCountLimit { get; }

    /// <summary>
    /// Gets the maximum length the writer should truncate instrumentation scope attribute values to, or <see langword="null"/> for no limit.
    /// </summary>
    internal int? ScopeAttributeValueLengthLimit { get; }

    /// <summary>
    /// Opens a batch. Dispose it when the batch has been read to report what was dropped.
    /// </summary>
    /// <returns><see cref="SpanLimitBatch"/>.</returns>
    public SpanLimitBatch BeginBatch()
        => new(this);

    internal void ReportBatchDrops(SpanLimitBatch batch)
    {
        if (batch.AffectedSpanCount > 0)
        {
            var batchTotals = new LimitExceededWarningTracker.Totals(
                batch.AffectedSpanCount,
                batch.DroppedAttributeCount,
                batch.DroppedEventCount,
                batch.DroppedLinkCount);

            if (this.warningTracker.RecordAndTryGetTotals(in batchTotals, out var totalsToReport))
            {
                OpenTelemetrySdkEventSource.Log.SpanLimitsExceeded(
                    this.ExporterName,
                    totalsToReport.AffectedItemCount,
                    totalsToReport.DroppedAttributeCount,
                    totalsToReport.DroppedEventCount,
                    totalsToReport.DroppedLinkCount);
            }
        }
    }
}
