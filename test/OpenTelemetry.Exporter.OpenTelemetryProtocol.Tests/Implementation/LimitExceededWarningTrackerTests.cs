// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Tests;

public class LimitExceededWarningTrackerTests
{
    [Fact]
    public async Task RecordAndTryGetTotalsProducesCoherentTotalsForConcurrentCallers()
    {
        const int CallerCount = 16;
        const int WarningIntervalSeconds = 5 * 60;

        var timestamp = 0L;
        var tracker = new LimitExceededWarningTracker(new LimitExceededWarningRateLimiter(() => timestamp), static () => true);

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
    public void RecordAndTryGetTotalsIgnoresBatchesWhileWarningIsDisabled()
    {
        var isWarningEnabled = false;
        var tracker = new LimitExceededWarningTracker(new LimitExceededWarningRateLimiter(static () => 0), () => isWarningEnabled);

        Assert.False(tracker.RecordAndTryGetTotals(new(1, 2, 3, 4), out _), "A batch should not be reported while warnings are disabled.");

        // The disabled batch neither consumed the interval nor contributed to the totals.
        isWarningEnabled = true;
        Assert.True(tracker.RecordAndTryGetTotals(new(5, 6, 7, 8), out var totals), "The first enabled batch should be reported.");
        Assert.Equal(new LimitExceededWarningTracker.Totals(5, 6, 7, 8), totals);
    }

    [Fact]
    public void RecordAndWarnIfDueIgnoresBatchesWithoutAffectedItems()
    {
        var tracker = new LimitExceededWarningTracker(new LimitExceededWarningRateLimiter(static () => 0), static () => true);
        var reported = new List<LimitExceededWarningTracker.Totals>();

        tracker.RecordAndWarnIfDue(default, reported.Add);

        // The empty batch did not consume the interval.
        tracker.RecordAndWarnIfDue(new(1, 2, 3, 4), reported.Add);
        tracker.RecordAndWarnIfDue(new(5, 6, 7, 8), reported.Add);

        Assert.Equal([new LimitExceededWarningTracker.Totals(1, 2, 3, 4)], reported);
    }
}
