// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal.Tests;

public class PeriodicExportingMetricReaderScheduleTests
{
    [Theory]
    [InlineData(99, 101)]
    [InlineData(100, 100)]
    [InlineData(101, 99)]
    public void TimedCollectionConsumesItsBoundary(int elapsedMilliseconds, int expectedTimeout)
    {
        var schedule = new PeriodicExportingMetricReaderSchedule(100);

        Assert.Equal(100, schedule.GetTimeout(0));
        schedule.OnExportIntervalElapsed();

        // A timeout at 99 ms must not cause another collection at 100 ms.
        Assert.Equal(expectedTimeout, schedule.GetTimeout(elapsedMilliseconds));
    }

    [Fact]
    public void TriggeredCollectionKeepsTheNextBoundaryAndSkipsMissedIntervals()
    {
        var schedule = new PeriodicExportingMetricReaderSchedule(100);

        Assert.Equal(100, schedule.GetTimeout(0));

        // Triggered collections do not consume the next scheduled boundary.
        Assert.Equal(60, schedule.GetTimeout(40));
        Assert.Equal(10, schedule.GetTimeout(90));

        // If a triggered collection runs past a boundary, resume at the next one.
        Assert.Equal(70, schedule.GetTimeout(130));

        // An early timeout must consume that later boundary too.
        schedule.OnExportIntervalElapsed();
        Assert.Equal(101, schedule.GetTimeout(199));
    }

    [Fact]
    public void SlowCollectionSkipsMissedIntervals()
    {
        var schedule = new PeriodicExportingMetricReaderSchedule(100);

        Assert.Equal(100, schedule.GetTimeout(0));
        schedule.OnExportIntervalElapsed();

        Assert.Equal(50, schedule.GetTimeout(450));

        schedule.OnExportIntervalElapsed();
        Assert.Equal(101, schedule.GetTimeout(499));
    }

    [Fact]
    public void LargeIntervalDoesNotOverflowTimeout()
    {
        var schedule = new PeriodicExportingMetricReaderSchedule(int.MaxValue);

        Assert.Equal(int.MaxValue, schedule.GetTimeout(0));
        schedule.OnExportIntervalElapsed();

        // The next boundary is int.MaxValue + 1 ms away after an early timeout.
        Assert.Equal(int.MaxValue, schedule.GetTimeout((long)int.MaxValue - 1));
        Assert.Equal(1, schedule.GetTimeout(((long)int.MaxValue * 2) - 1));
    }

    [Fact]
    public void InfiniteIntervalNeverSchedulesATimeout()
    {
        var schedule = new PeriodicExportingMetricReaderSchedule(Timeout.Infinite);

        Assert.Equal(Timeout.Infinite, schedule.GetTimeout(0));
        Assert.Equal(Timeout.Infinite, schedule.GetTimeout((long)int.MaxValue * 3));
    }
}
