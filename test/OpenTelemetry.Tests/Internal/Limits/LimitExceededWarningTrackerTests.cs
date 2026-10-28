// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Internal.Tests;

public class LimitExceededWarningTrackerTests
{
    [Fact]
    public void RecordAndTryGetTotals_AtIntervalBoundaries_ReportsAccumulatedTotals()
    {
        var interval = 5 * 60 * Stopwatch.Frequency;
        var timestamp = 0L;
        var tracker = new LimitExceededWarningTracker(() => timestamp, static () => true);
        var batchTotals = new LimitExceededWarningTracker.Totals(1, 2, 3, 4);

        Assert.True(tracker.RecordAndTryGetTotals(batchTotals, out var initialTotals), "The first batch should be reported.");
        Assert.Equal(batchTotals, initialTotals);
        Assert.False(tracker.RecordAndTryGetTotals(batchTotals, out var suppressedTotals), "A second batch should be suppressed in the same interval.");
        Assert.Equal(default, suppressedTotals);

        timestamp = interval - 1;
        Assert.False(tracker.RecordAndTryGetTotals(batchTotals, out _), "A batch should be suppressed before the interval elapses.");

        timestamp = interval;
        Assert.True(tracker.RecordAndTryGetTotals(batchTotals, out var intervalTotals), "A batch should be reported at the interval boundary.");
        Assert.Equal(new LimitExceededWarningTracker.Totals(3, 6, 9, 12), intervalTotals);

        timestamp = (2 * interval) - 1;
        Assert.False(tracker.RecordAndTryGetTotals(batchTotals, out _), "A batch should be suppressed before the next interval elapses.");

        timestamp = 2 * interval;
        Assert.True(tracker.RecordAndTryGetTotals(batchTotals, out var nextIntervalTotals), "A batch should be reported at the next interval boundary.");
        Assert.Equal(new LimitExceededWarningTracker.Totals(2, 4, 6, 8), nextIntervalTotals);
    }

    [Fact]
    public async Task RecordAndTryGetTotals_ConcurrentSuppressedBatches_AccumulatesAllCounters()
    {
        const int CallerCount = 16;
        const int WarningIntervalSeconds = 5 * 60;

        var timestamp = 0L;
        var tracker = new LimitExceededWarningTracker(() => timestamp, static () => true);

        Assert.True(tracker.RecordAndTryGetTotals(new(1, 2, 3, 4), out var initialTotals), "The initial batch should be reported.");
        Assert.Equal(new LimitExceededWarningTracker.Totals(1, 2, 3, 4), initialTotals);

        using var startGate = new ManualResetEventSlim();
        var tasks = Enumerable.Range(0, CallerCount)
            .Select(callerIndex => Task.Run(() =>
            {
                Assert.True(startGate.Wait(TimeSpan.FromSeconds(10)), "The concurrent callers should be released.");
                return tracker.RecordAndTryGetTotals(new(1, 2, 3, 4), out _);
            }))
            .ToArray();

        startGate.Set();
        var results = await Task.WhenAll(tasks);
        Assert.DoesNotContain(results, emitted => emitted);

        timestamp = WarningIntervalSeconds * Stopwatch.Frequency;
        Assert.True(tracker.RecordAndTryGetTotals(new(1, 2, 3, 4), out var intervalTotals), "The batch after the warning interval should be reported.");
        Assert.Equal(
            new LimitExceededWarningTracker.Totals(
                CallerCount + 1,
                (CallerCount + 1) * 2,
                (CallerCount + 1) * 3,
                (CallerCount + 1) * 4),
            intervalTotals);
    }

    [Fact]
    public async Task RecordAndTryGetTotals_ConcurrentCallersAtWarningBoundary_EmitsOnceWithoutLosingTotals()
    {
        const int CallerCount = 8;
        var timestamp = 0L;
        var tracker = new LimitExceededWarningTracker(
            () => timestamp,
            static () => true);

        Assert.True(tracker.RecordAndTryGetTotals(new(1, 1, 10, 100), out _), "The initial batch should emit a warning and begin the suppression interval.");
        Assert.False(tracker.RecordAndTryGetTotals(new(1, 100, 1000, 10000), out _), "A second batch in the same interval should accumulate its totals without emitting a warning.");
        timestamp = 5 * 60 * Stopwatch.Frequency;

        using var startGate = new Barrier(CallerCount);
        var tasks = Enumerable.Range(1, CallerCount)
            .Select(value => Task.Factory.StartNew(
                () =>
                {
                    Assert.True(startGate.SignalAndWait(TimeSpan.FromSeconds(10)), "All callers should reach the warning boundary.");
                    var emitted = tracker.RecordAndTryGetTotals(new(1, value, value * 10, value * 100), out var totals);
                    return (Emitted: emitted, Totals: totals);
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        var results = await Task.WhenAll(tasks);
        var emittedTotals = Assert.Single(results, result => result.Emitted).Totals;
        Assert.Equal(emittedTotals.DroppedAttributeCount * 10, emittedTotals.DroppedEventCount);
        Assert.Equal(emittedTotals.DroppedAttributeCount * 100, emittedTotals.DroppedLinkCount);
        Assert.All(results.Where(result => !result.Emitted), result => Assert.Equal(default, result.Totals));

        timestamp *= 2;
        Assert.True(tracker.RecordAndTryGetTotals(new(1, 7, 70, 700), out var remainingTotals), "The next warning interval should report any totals not emitted by the concurrent callers.");

        var expectedAttributeCount = 100 + Enumerable.Range(1, CallerCount).Sum() + 7;
        Assert.Equal(
            new LimitExceededWarningTracker.Totals(
                CallerCount + 2,
                expectedAttributeCount,
                expectedAttributeCount * 10,
                expectedAttributeCount * 100),
            emittedTotals.Add(remainingTotals));
    }

    [Fact]
    public void RecordAndTryGetTotals_WarningsDisabled_DoesNotAccumulateOrConsumeInterval()
    {
        var isWarningEnabled = false;
        var tracker = new LimitExceededWarningTracker(static () => 0, () => isWarningEnabled);

        Assert.False(tracker.RecordAndTryGetTotals(new(1, 2, 3, 4), out _), "A batch should not be reported while warnings are disabled.");

        // The disabled batch neither consumed the interval nor contributed to the totals.
        isWarningEnabled = true;
        Assert.True(tracker.RecordAndTryGetTotals(new(5, 6, 7, 8), out var totals), "The first enabled batch should be reported.");
        Assert.Equal(new LimitExceededWarningTracker.Totals(5, 6, 7, 8), totals);
    }
}
