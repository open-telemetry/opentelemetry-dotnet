// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Trace.Tests;

internal static class SpanLimitTestHelpers
{
    public const int SpanLimitsExceededEventId = 60;

    public static readonly long WarningInterval = 5 * 60 * Stopwatch.Frequency;

    public static SpanLimiter CreateLimiter(
        Action<SpanLimitOptions>? configure = null,
        string exporterName = "test",
        Func<long>? getTimestamp = null,
        Func<bool>? isWarningEnabled = null)
    {
        var options = new SpanLimitOptions();
        configure?.Invoke(options);

        var tracker = new LimitExceededWarningTracker(
            new LimitExceededWarningRateLimiter(getTimestamp ?? (static () => 0)),
            isWarningEnabled ?? (static () => true));

        return new SpanLimiter(options, new AttributeLimitOptions(), exporterName, tracker);
    }

    /// <summary>
    /// Returns a clock that moves forward one warning interval on every call, so every batch
    /// with drops emits a warning.
    /// </summary>
    public static Func<long> AdvancingClock()
    {
        var timestamp = 0L;
        return () => Interlocked.Add(ref timestamp, WarningInterval);
    }

    public static List<string> ReadKeys(ref SpanAttributeReader reader)
    {
        var keys = new List<string>();
        while (reader.TryRead(out var attribute))
        {
            keys.Add(attribute.Key);
        }

        return keys;
    }

    public static List<string> ReadKeys(ref AttributeReader reader)
    {
        var keys = new List<string>();
        while (reader.TryRead(out var attribute))
        {
            keys.Add(attribute.Key);
        }

        return keys;
    }

    public static Activity CreateActivity(params string[] tagKeys)
    {
        var activity = new Activity("test");
        foreach (var key in tagKeys)
        {
            activity.SetTag(key, "value");
        }

        return activity;
    }

    public static List<SpanLimitWarning> GetWarnings(TestEventListener listener)
        => [.. listener.CurrentMessages
            .Where(e => e.EventSource.Name == "OpenTelemetry-Sdk" && e.EventId == SpanLimitsExceededEventId)
            .Select(e => new SpanLimitWarning(
                (string)e.Payload![0]!,
                (long)e.Payload[1]!,
                (long)e.Payload[2]!,
                (long)e.Payload[3]!,
                (long)e.Payload[4]!)),];

    public static TestEventListener ListenForWarnings()
        => new(OpenTelemetrySdkEventSource.Log, EventLevel.Warning);

    internal readonly struct SpanLimitWarning(
        string exporterName,
        long spanCount,
        long droppedAttributeCount,
        long droppedEventCount,
        long droppedLinkCount)
    {
        public string ExporterName { get; } = exporterName;

        public long SpanCount { get; } = spanCount;

        public long DroppedAttributeCount { get; } = droppedAttributeCount;

        public long DroppedEventCount { get; } = droppedEventCount;

        public long DroppedLinkCount { get; } = droppedLinkCount;
    }
}
