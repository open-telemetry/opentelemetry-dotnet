// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

internal struct PeriodicExportingMetricReaderSchedule(int exportIntervalMilliseconds)
{
    private long nextExportAtMilliseconds = exportIntervalMilliseconds;

    public int GetTimeout(long elapsedMilliseconds)
    {
        if (exportIntervalMilliseconds == Timeout.Infinite)
        {
            return Timeout.Infinite;
        }

        if (this.nextExportAtMilliseconds <= elapsedMilliseconds)
        {
            // Skip intervals missed while a collection was in progress.
            this.nextExportAtMilliseconds = elapsedMilliseconds - (elapsedMilliseconds % exportIntervalMilliseconds) + exportIntervalMilliseconds;
        }

        // A wait can finish early, so the next deadline can be slightly more
        // than one interval away. Keep the timeout in the range accepted by
        // WaitHandle.WaitAny and Task.Delay, even for int.MaxValue intervals.
        return (int)Math.Min(this.nextExportAtMilliseconds - elapsedMilliseconds, int.MaxValue);
    }

    public void OnExportIntervalElapsed()
    {
        // Consume the scheduled boundary even if the wait returned early.
        this.nextExportAtMilliseconds += exportIntervalMilliseconds;
    }
}
