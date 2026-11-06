// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Api.Tests;

[Collection(EventSourceCollectionDefinition.Name)]
public class EventSourceTests
{
    [Fact]
    public void EventSourceTests_OpenTelemetryApiEventSource() =>
        EventSourceTestHelper.ValidateEventSourceIds<OpenTelemetryApiEventSource>();

    [Fact]
    public void EventSourceTests_OpenTelemetryApiEventSource_WritesEvents() =>
        EventSourceTestHelper.ExerciseEventSource<OpenTelemetryApiEventSource>();

    [Fact]
    public void EventSourceTests_OpenTelemetryApiEventSource_WritesTracestateSpanEvents()
    {
        // Methods with ReadOnlySpan<char> parameters cannot be invoked using reflection,
        // so they are not covered by ExerciseEventSource().
        var log = OpenTelemetryApiEventSource.Log;

        using var listener = new TestEventListener();
        listener.EnableEvents(log, EventLevel.Warning, EventKeywords.All);

        log.TracestateKeyIsInvalid("key".AsSpan());
        log.TracestateValueIsInvalid("value".AsSpan());

        var events = listener.CurrentMessages;

        Assert.Equal([4, 5], events.Select(x => x.EventId));
        Assert.Equal("key", events[0].Payload![0]);
        Assert.Equal("value", events[1].Payload![0]);
    }
}
