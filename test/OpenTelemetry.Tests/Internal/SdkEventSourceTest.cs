// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
#nullable enable

using System.Diagnostics;
using System.Diagnostics.Tracing;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Internal.Tests;

[Collection("Uses-OpenTelemetrySdkEventSource")] // Prevent parallel execution with other tests that exercise the SdkEventSource
public class SdkEventSourceTest : IDisposable
{
    private readonly SdkEventListener listener = new();

    public void Dispose()
    {
        this.listener.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ActivityTrackingWorks()
    {
        using TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("TestSource")
            .Build();

        // Clear any events that were emitted during Build.
        this.listener.Events.Clear();

        const int numActivities = 4;

        using ActivitySource activitySource = new("TestSource");
        for (int i = 0; i < numActivities; i++)
        {
            using Activity? activity = activitySource.StartActivity($"Test Activity {i}");
        }

        // There should be 2 events for each activity: ActivityStart and ActivityStop.
        Assert.Equal(numActivities * 2, this.listener.Events.Count);

        HashSet<Guid> activityIds = [];
        for (int i = 0; i < numActivities; i++)
        {
            EventWrittenEventArgs startEvent = this.listener.Events[i * 2];
            EventWrittenEventArgs stopEvent = this.listener.Events[(i * 2) + 1];

            Assert.Equal("ActivityStart", startEvent.EventName);
            Assert.Equal("ActivityStop", stopEvent.EventName);

            // Start and Stop should be matched on ActivityId.
            Assert.Equal(startEvent.ActivityId, stopEvent.ActivityId);

            // ActivityIds should be unique.
            Assert.True(activityIds.Add(startEvent.ActivityId));
        }
    }

    private sealed class SdkEventListener : EventListener
    {
        private static readonly string SdkEventSourceName = EventSource.GetName(typeof(OpenTelemetrySdkEventSource));
        private readonly HashSet<EventSource> eventSourcesEnabled = [];

        public List<EventWrittenEventArgs> Events { get; } = [];

        public override void Dispose()
        {
            try
            {
                foreach (EventSource eventSource in this.eventSourcesEnabled)
                {
                    this.DisableEvents(eventSource);
                }
            }
            finally
            {
                base.Dispose();
            }
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == SdkEventSourceName)
            {
                this.eventSourcesEnabled.Add(eventSource);
                this.EnableEvents(eventSource, EventLevel.Informational, OpenTelemetrySdkEventSource.Keywords.Activities);
            }
            else if (eventSource.Name == "System.Threading.Tasks.TplEventSource")
            {
                // In addition to the OpenTelemetrySdkEventSource we need
                // the TPL EventSource to enable activity tracking. When
                // enabled and an EventSource writes events with
                // EventOpCode.Start/Stop, then the ActivityTracker will
                // generate new path-like ActivityIDs.
                // Also note that activity tracking requires that the
                // Start/Stop events are a matched pair, named like
                // "xyzStart" and "xyzStop". ActivityTracker matches the
                // stop event with the start event by recognizing those
                // exact suffixes.
                this.eventSourcesEnabled.Add(eventSource);
                const EventKeywords taskFlowActivityIds = (EventKeywords)0x80;
                this.EnableEvents(eventSource, EventLevel.Informational, taskFlowActivityIds);
            }

            base.OnEventSourceCreated(eventSource);
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventSource.Name == SdkEventSourceName)
            {
                this.Events.Add(eventData);
            }

            base.OnEventWritten(eventData);
        }
    }
}
