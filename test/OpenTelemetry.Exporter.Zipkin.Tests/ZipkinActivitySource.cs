// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Exporter.Zipkin.Tests;

internal static class ZipkinActivitySource
{
    private static readonly ActivitySource ActivitySource = new(nameof(ZipkinActivitySource));

    internal static Activity CreateTestActivity(
       bool isRootSpan = false,
       bool setAttributes = true,
       Dictionary<string, object>? additionalAttributes = null,
       bool addEvents = true,
       bool addLinks = true,
       ActivityKind kind = ActivityKind.Client,
       ActivityStatusCode statusCode = ActivityStatusCode.Unset,
       string? statusDescription = null,
       DateTime? dateTime = null)
    {
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => options.Parent.TraceFlags.HasFlag(ActivityTraceFlags.Recorded)
                ? ActivitySamplingResult.AllDataAndRecorded
                : ActivitySamplingResult.AllData,
        };

        ActivitySource.AddActivityListener(activityListener);

        var startTimestamp = DateTime.UtcNow;
        var endTimestamp = startTimestamp.AddSeconds(60);
        var eventTimestamp = DateTime.UtcNow;
        var traceId = ActivityTraceId.CreateFromString("e8ea7e9ac72de94e91fabc613f9686b2".AsSpan());

        dateTime ??= DateTime.UtcNow;

        var parentSpanId = isRootSpan ? default : ActivitySpanId.CreateFromBytes([12, 23, 34, 45, 56, 67, 78, 89]);

        var attributes = new Dictionary<string, object>
        {
            { "stringKey", "value" },
            { "longKey", 1L },
            { "longKey2", 1 },
            { "doubleKey", 1D },
            { "doubleKey2", 1F },
            { "longArrayKey", new long[] { 1, 2 } },
            { "boolKey", true },
            { "boolArrayKey", new bool[] { true, false } },
            { "http.host", "http://localhost:44312/" }, // simulating instrumentation tag adding http.host
            { "dateTimeKey", dateTime.Value },
            { "dateTimeArrayKey", new DateTime[] { dateTime.Value } },
        };
        if (additionalAttributes != null)
        {
            foreach (var attribute in additionalAttributes)
            {
                if (!attributes.ContainsKey(attribute.Key))
                {
                    attributes.Add(attribute.Key, attribute.Value);
                }
            }
        }

        var events = new List<ActivityEvent>
        {
            new(
                "Event1",
                eventTimestamp,
                new(new Dictionary<string, object?>
                {
                    { "key", "value" },
                })),
            new(
                "Event2",
                eventTimestamp,
                new(new Dictionary<string, object?>
                {
                    { "key", "value" },
                })),
        };

        var linkedSpanId = ActivitySpanId.CreateFromString("888915b6286b9c41".AsSpan());

        var tags = setAttributes ?
                attributes.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value))
                : null;
        var links = addLinks ?
                new[]
                {
                    new ActivityLink(new(
                        traceId,
                        linkedSpanId,
                        ActivityTraceFlags.Recorded)),
                }
                : null;

        var activity = ActivitySource.StartActivity(
            "Name",
            kind,
            parentContext: new(traceId, parentSpanId, ActivityTraceFlags.Recorded),
            tags,
            links,
            startTime: startTimestamp)!;

        Assert.NotNull(activity);

        if (addEvents)
        {
            foreach (var evnt in events)
            {
                activity.AddEvent(evnt);
            }
        }

        activity.SetStatus(statusCode, statusDescription);

        activity.SetEndTime(endTimestamp);
        activity.Stop();

        return activity;
    }
}
