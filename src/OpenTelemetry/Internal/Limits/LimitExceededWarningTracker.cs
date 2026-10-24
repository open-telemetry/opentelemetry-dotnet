// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace OpenTelemetry.Internal;

/// <summary>
/// Accumulates the items discarded due to limits and allows a warning reporting them
/// at most once every five minutes.
/// </summary>
internal sealed class LimitExceededWarningTracker
{
    private static readonly long WarningIntervalTimestampTicks = 5 * 60 * Stopwatch.Frequency;

    private readonly Lock pendingTotalsLock = new();
    private readonly Func<long> getTimestamp;
    private readonly Func<bool> isWarningEnabled;

    private Totals pendingTotals;
    private long nextAllowedTimestamp;

    internal LimitExceededWarningTracker()
        : this(
            static () => Stopwatch.GetTimestamp(),
            static () => OpenTelemetrySdkEventSource.Log.IsEnabled(EventLevel.Warning, EventKeywords.All))
    {
    }

    internal LimitExceededWarningTracker(Func<long> getTimestamp, Func<bool> isWarningEnabled)
    {
        this.getTimestamp = getTimestamp;
        this.isWarningEnabled = isWarningEnabled;
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

            var currentTimestamp = this.getTimestamp();
            if (currentTimestamp < this.nextAllowedTimestamp)
            {
                totalsToReport = default;
                return false;
            }

            this.nextAllowedTimestamp = currentTimestamp + WarningIntervalTimestampTicks;
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
