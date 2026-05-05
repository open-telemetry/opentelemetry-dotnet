// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Internal.Tests;

public static class StopwatchExtensionsTests
{
    [Fact]
    public static void Remaining_ClampsElapsedMillisecondsPastIntMaxValue()
    {
        var elapsed = TimeSpan.FromDays(30);
        var elapsedTimestampTicks = (long)(elapsed.Ticks * (Stopwatch.Frequency / (double)TimeSpan.TicksPerSecond));
        var begin = Stopwatch.GetTimestamp() - elapsedTimestampTicks;

        var remaining = Stopwatch.Remaining(1000, begin);

        Assert.Equal(0, remaining);
    }
}
