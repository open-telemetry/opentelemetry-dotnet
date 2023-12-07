// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace Benchmarks.Helper;

internal static class ActivityHelper
{
    public static Activity CreateTestActivity()
    {
        var startTimestamp = DateTime.UtcNow;
        var endTimestamp = startTimestamp.AddSeconds(60);
        var eventTimestamp = DateTime.UtcNow;

        var traceId = ActivityTraceId.CreateFromString("e8ea7e9ac72de94e91fabc613f9686b2".AsSpan());
        var parentSpanId = ActivitySpanId.CreateFromBytes(new byte[] { 12, 23, 34, 45, 56, 67, 78, 89 });

        var attributes = new Dictionary<string, object>
        {
            { "stringKey", "value" },
            { "longKey", 1L },
            { "longKey2", 1 },
            { "doubleKey", 1D },
            { "doubleKey2", 1F },
            { "boolKey", true },
        };

        var events = new List<ActivityEvent>
        {
            new ActivityEvent(
                "Event1",
                eventTimestamp,
                new ActivityTagsCollection(new Dictionary<string, object>
                {
                    { "key", "value" },
                })),
            new ActivityEvent(
                "Event2",
                eventTimestamp,
                new ActivityTagsCollection(new Dictionary<string, object>
                {
                    { "key", "value" },
                })),
        };

        var linkedSpanId = ActivitySpanId.CreateFromString("888915b6286b9c41".AsSpan());

        using var activitySource = new ActivitySource(nameof(CreateTestActivity));

        var tags = attributes.Select(kvp => new KeyValuePair<string, object>(kvp.Key, kvp.Value.ToString()));
        var links = new[]
        {
            new ActivityLink(new ActivityContext(
                traceId,
                linkedSpanId,
                ActivityTraceFlags.Recorded)),
        };

        using var listener = new ActivityListener()
        {
            ShouldListenTo = a => a.Name == nameof(CreateTestActivity),
            Sample = (ref ActivityCreationOptions<ActivityContext> a) => ActivitySamplingResult.AllDataAndRecorded,
        };

        ActivitySource.AddActivityListener(listener);

        var activity = activitySource.StartActivity(
            "Name",
            ActivityKind.Client,
            parentContext: new ActivityContext(traceId, parentSpanId, ActivityTraceFlags.Recorded),
            tags,
            links,
            startTime: startTimestamp);

        foreach (var evnt in events)
        {
            activity.AddEvent(evnt);
        }

        activity.SetEndTime(endTimestamp);
        activity.Stop();

        return activity;
    }
}
