// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

namespace OpenTelemetry.Tests;

/// <summary>
/// Event listener for testing event sources.
/// </summary>
#pragma warning disable CA1812
internal sealed class TestEventListener : EventListener
#pragma warning restore CA1812
{
    /// <summary>Unique Id used to identify events from the test thread.</summary>
    private readonly Guid activityId;

    /// <summary>
    /// Lock for event writing tracking.
    /// </summary>
    private readonly AutoResetEvent eventWritten;

    /// <summary>A queue of events that have been logged.</summary>
    private ConcurrentQueue<EventWrittenEventArgs> events;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestEventListener"/> class.
    /// </summary>
    /// <param name="eventSource">The optional <see cref="EventSource"/> to enable.</param>
    /// <param name="minLevel">The optional <see cref="EventLevel"/> to use with <paramref name="eventSource"/>.</param>
    public TestEventListener(EventSource? eventSource = null, EventLevel minLevel = EventLevel.Verbose)
    {
        this.activityId = Guid.NewGuid();
        EventSource.SetCurrentThreadActivityId(this.activityId);

        this.events = [];
        this.eventWritten = new AutoResetEvent(false);
        this.OnOnEventWritten = e =>
        {
            this.events.Enqueue(e);
            this.eventWritten.Set();
        };

        if (eventSource is not null)
        {
            this.EnableEvents(eventSource, minLevel);
        }
    }

    /// <summary>Gets or sets the handler for event source creation.</summary>
    public Action<EventSource>? OnOnEventSourceCreated { get; set; }

    /// <summary>Gets or sets the handler for event source writes.</summary>
    public Action<EventWrittenEventArgs> OnOnEventWritten { get; set; }

    /// <summary>Gets the events that have been written.</summary>
    public IList<EventWrittenEventArgs> Messages
    {
        get
        {
            if (this.events.IsEmpty)
            {
                this.eventWritten.WaitOne(TimeSpan.FromSeconds(5));
            }

            return [..this.events];
        }
    }

    /// <summary>
    /// Clears all event messages so that testing can assert expected counts.
    /// </summary>
    public void ClearMessages() => this.events = [];

    public override void Dispose()
    {
        this.eventWritten.Dispose();
        base.Dispose();
    }

    /// <summary>Handler for event source writes.</summary>
    /// <param name="eventData">The event data that was written.</param>
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.ActivityId == this.activityId)
        {
            this.OnOnEventWritten(eventData);
        }
    }

    /// <summary>Handler for event source creation.</summary>
    /// <param name="eventSource">The event source that was created.</param>
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        // Check for null because this method is called by the base class constructor before we can initialize it
        var callback = this.OnOnEventSourceCreated;
        callback?.Invoke(eventSource);
    }
}
