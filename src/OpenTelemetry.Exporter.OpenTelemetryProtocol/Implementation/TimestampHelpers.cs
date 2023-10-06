// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0der the License.
// </copyright>

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

/// <summary>
/// Helpers to convert .NET time related types to the timestamp used in OTLP.
/// </summary>
internal static class TimestampHelpers
{
    private const long NanosecondsPerTicks = 100;
    private const long UnixEpochTicks = 621355968000000000; // = DateTimeOffset.FromUnixTimeMilliseconds(0).Ticks

    internal static long ToUnixTimeNanoseconds(this DateTime dt)
    {
        return (dt.Ticks - UnixEpochTicks) * NanosecondsPerTicks;
    }

    internal static long ToUnixTimeNanoseconds(this DateTimeOffset dto)
    {
        return (dto.Ticks - UnixEpochTicks) * NanosecondsPerTicks;
    }

    internal static long ToNanoseconds(this TimeSpan duration)
    {
        return duration.Ticks * NanosecondsPerTicks;
    }
}
