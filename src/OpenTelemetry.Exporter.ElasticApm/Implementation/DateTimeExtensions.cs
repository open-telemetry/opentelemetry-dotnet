using System;

namespace OpenTelemetry.Exporter.ElasticApm.Implementation
{
    internal static class DateTimeExtensions
    {
        private static readonly long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        private static readonly long UnixEpochTicks = DateTimeOffset.FromUnixTimeMilliseconds(0).Ticks;
        private static readonly long UnixEpochMicroseconds = UnixEpochTicks / TicksPerMicrosecond;

        internal static long ToEpochMicroseconds(this DateTime utcDateTime)
        {
            long microseconds = utcDateTime.Ticks / TicksPerMicrosecond;
            return microseconds - UnixEpochMicroseconds;
        }

        internal static long ToEpochMicroseconds(this TimeSpan timeSpan)
        {
            return timeSpan.Ticks / TicksPerMicrosecond;
        }
    }
}
