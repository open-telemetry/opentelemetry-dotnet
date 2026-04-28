// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace System.Diagnostics;

internal static class StopwatchExtensions
{
#if !NET
    private static readonly double ToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;
#endif

#pragma warning disable SA1201 // A <unknown> should not follow a field
    extension(Stopwatch)
#pragma warning disable SA1201 // A <unknown> should not follow a field
    {
#if !NET
        public static TimeSpan GetElapsedTime(long begin)
        {
            var end = Stopwatch.GetTimestamp();
            var delta = end - begin;
            var ticks = (long)(ToTicks * delta);

            return new TimeSpan(ticks);
        }
#endif

        public static int Remaining(int durationMilliseconds, long begin)
        {
            var elapsedMilliseconds = Stopwatch.GetElapsedTime(begin).Ticks / TimeSpan.TicksPerMillisecond;
            elapsedMilliseconds = elapsedMilliseconds < 0 ? 0 : elapsedMilliseconds > int.MaxValue ? int.MaxValue : elapsedMilliseconds;

            return elapsedMilliseconds >= durationMilliseconds ? 0 : durationMilliseconds - (int)elapsedMilliseconds;
        }
    }
}
