// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Tests;

public class LimitExceededWarningRateLimiterTests
{
    [Fact]
    public void TryAcquireAllowsOneCallerPerInterval()
    {
        var interval = 5 * 60 * Stopwatch.Frequency;
        var timestamp = 0L;
        var rateLimiter = new LimitExceededWarningRateLimiter(() => timestamp);

        Assert.True(rateLimiter.TryAcquire(), "The first caller should acquire the limiter.");
        Assert.False(rateLimiter.TryAcquire(), "A second caller should not acquire the limiter in the same interval.");

        timestamp = interval - 1;
        Assert.False(rateLimiter.TryAcquire(), "A caller should not acquire the limiter before the interval elapses.");

        timestamp = interval;
        Assert.True(rateLimiter.TryAcquire(), "A caller should acquire the limiter when the interval elapses.");

        timestamp = (2 * interval) - 1;
        Assert.False(rateLimiter.TryAcquire(), "A caller should not acquire the limiter before the next interval elapses.");

        timestamp = 2 * interval;
        Assert.True(rateLimiter.TryAcquire(), "A caller should acquire the limiter when the next interval elapses.");
    }
}
