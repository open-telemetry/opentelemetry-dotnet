// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

/// <summary>
/// A rate limiter that allows a warning to be emitted at most once every 5 minutes.
/// </summary>
/// <remarks>
/// This type is not thread-safe. Callers must synchronize access to <see cref="TryAcquire"/>.
/// </remarks>
internal sealed class LimitExceededWarningRateLimiter
{
    // The tracing and logging specifications cap limit warnings to at most once per affected span
    // or log record. This limiter enforces a stricter cap of one warning every 5 minutes per exporter
    // while the warning tracker preserves totals from suppressed batches. The goal is to prevent
    // excessive log spam from repeated limit warnings.

    private const int WarningIntervalSeconds = 5 * 60;

    private static readonly long WarningIntervalTimestampTicks = WarningIntervalSeconds * Stopwatch.Frequency;

    private readonly Func<long> getTimestamp;

    private long nextAllowedTimestamp;

    internal LimitExceededWarningRateLimiter()
        : this(static () => Stopwatch.GetTimestamp())
    {
    }

    internal LimitExceededWarningRateLimiter(Func<long> getTimestamp)
    {
        this.getTimestamp = getTimestamp;
    }

    internal bool TryAcquire()
    {
        var currentTimestamp = this.getTimestamp();

        if (currentTimestamp < this.nextAllowedTimestamp)
        {
            return false;
        }

        this.nextAllowedTimestamp = currentTimestamp + WarningIntervalTimestampTicks;
        return true;
    }
}
