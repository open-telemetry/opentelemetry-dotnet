// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

/// <summary>
/// Accumulates the items discarded due to limits and decides when a rate limited
/// warning reporting them should be emitted.
/// </summary>
internal sealed class LimitExceededWarningTracker
{
    private readonly Lock pendingTotalsLock = new();
    private readonly LimitExceededWarningRateLimiter rateLimiter;
    private readonly Func<bool> isWarningEnabled;

    private Totals pendingTotals;

    internal LimitExceededWarningTracker(LimitExceededWarningRateLimiter rateLimiter)
        : this(rateLimiter, static () => OpenTelemetryProtocolExporterEventSource.Log.IsEnabled(EventLevel.Warning, EventKeywords.All))
    {
    }

    internal LimitExceededWarningTracker(LimitExceededWarningRateLimiter rateLimiter, Func<bool> isWarningEnabled)
    {
        this.rateLimiter = rateLimiter;
        this.isWarningEnabled = isWarningEnabled;
    }

    /// <summary>
    /// Records the totals for a batch, if any items were affected, and invokes
    /// <paramref name="writeWarning"/> with the accumulated totals when a warning is due.
    /// </summary>
    /// <param name="batchTotals">The totals discarded from the batch.</param>
    /// <param name="writeWarning">Emits the signal-specific warning event.</param>
    internal void RecordAndWarnIfDue(in Totals batchTotals, Action<Totals> writeWarning)
    {
        if (batchTotals.AffectedItemCount > 0 && this.RecordAndTryGetTotals(batchTotals, out var totalsToReport))
        {
            writeWarning(totalsToReport);
        }
    }

    /// <summary>
    /// Records the totals for a batch and, when a warning is due, returns the totals
    /// accumulated since the previous warning.
    /// </summary>
    /// <param name="batchTotals">The totals discarded from the batch.</param>
    /// <param name="totalsToReport">The totals to report when a warning is due.</param>
    /// <returns><see langword="true"/> if a warning should be emitted; otherwise, <see langword="false"/>.</returns>
    internal bool RecordAndTryGetTotals(in Totals batchTotals, out Totals totalsToReport)
    {
        // Without a listener, nothing is tracked so that the rate limit window is not consumed.
        if (!this.isWarningEnabled())
        {
            totalsToReport = default;
            return false;
        }

        lock (this.pendingTotalsLock)
        {
            this.pendingTotals = this.pendingTotals.Add(batchTotals);

            if (!this.rateLimiter.TryAcquire())
            {
                totalsToReport = default;
                return false;
            }

            totalsToReport = this.pendingTotals;
            this.pendingTotals = default;
            return true;
        }
    }

    /// <summary>
    /// The number of affected items and the attributes, events, and links discarded from them.
    /// </summary>
    internal readonly struct Totals
    {
        internal Totals(
            long affectedItemCount,
            long droppedAttributeCount,
            long droppedEventCount,
            long droppedLinkCount)
        {
            this.AffectedItemCount = affectedItemCount;
            this.DroppedAttributeCount = droppedAttributeCount;
            this.DroppedEventCount = droppedEventCount;
            this.DroppedLinkCount = droppedLinkCount;
        }

        internal long AffectedItemCount { get; }

        internal long DroppedAttributeCount { get; }

        internal long DroppedEventCount { get; }

        internal long DroppedLinkCount { get; }

        internal Totals Add(in Totals other)
            => new(
                this.AffectedItemCount + other.AffectedItemCount,
                this.DroppedAttributeCount + other.DroppedAttributeCount,
                this.DroppedEventCount + other.DroppedEventCount,
                this.DroppedLinkCount + other.DroppedLinkCount);
    }
}
