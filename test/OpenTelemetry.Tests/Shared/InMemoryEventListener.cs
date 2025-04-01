// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

namespace OpenTelemetry.Tests;

internal sealed class InMemoryEventListener : EventListener
{
    public ConcurrentQueue<EventWrittenEventArgs> Events = new();

    public InMemoryEventListener(EventSource eventSource, EventLevel minLevel = EventLevel.Verbose)
    {
        this.EnableEvents(eventSource, minLevel);
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        this.Events.Enqueue(eventData);
    }
}
