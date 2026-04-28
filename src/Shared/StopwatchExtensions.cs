// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace System.Diagnostics;

internal static class StopwatchExtensions
{
    extension(Stopwatch)
    {
#if !NET
        public static TimeSpan GetElapsedTime(long begin)
        {
            var end = Stopwatch.GetTimestamp();

            var timestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;
            var delta = end - begin;
            var ticks = (long)(timestampToTicks * delta);

            return new TimeSpan(ticks);
        }
#endif

        public static int Remaining(int durationMilliseconds, long begin)
        {
            var elapsed = (int)Stopwatch.GetElapsedTime(begin).TotalMilliseconds;
            return elapsed >= durationMilliseconds ? 0 : durationMilliseconds - elapsed;
        }
    }
}
