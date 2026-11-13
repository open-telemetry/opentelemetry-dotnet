// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Trace.Tests;

public class TelemetrySpanTests
{
    [Fact]
    public void CheckRecordExceptionData()
    {
        var message = "message";

        using var activity = new Activity("exception-test");
        using var telemetrySpan = new TelemetrySpan(activity);
        telemetrySpan.RecordException(new ArgumentNullException(message, new InvalidOperationException("new-exception")));
        Assert.Single(activity.Events);

        Assert.NotNull(telemetrySpan.Activity);
        var @event = telemetrySpan.Activity.Events.FirstOrDefault(q => q.Name == SemanticConventions.AttributeExceptionEventName);
        Assert.Equal(message, @event.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionMessage).Value);
        Assert.Equal(nameof(ArgumentNullException), @event.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionType).Value);
    }

    [Fact]
    public void CheckRecordExceptionData2()
    {
        var type = "ArgumentNullException";
        var message = "message";
        var stack = "stack";

        using var activity = new Activity("exception-test");
        using var telemetrySpan = new TelemetrySpan(activity);
        telemetrySpan.RecordException(type, message, stack);
        Assert.Single(activity.Events);

        Assert.NotNull(telemetrySpan.Activity);
        var @event = telemetrySpan.Activity.Events.FirstOrDefault(q => q.Name == SemanticConventions.AttributeExceptionEventName);
        Assert.Equal(message, @event.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionMessage).Value);
        Assert.Equal(type, @event.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionType).Value);
        Assert.Equal(stack, @event.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionStacktrace).Value);
    }

    [Fact]
    public void CheckRecordExceptionEmpty()
    {
        using var activity = new Activity("exception-test");
        using var telemetrySpan = new TelemetrySpan(activity);
        telemetrySpan.RecordException(string.Empty, string.Empty, string.Empty);
        Assert.Empty(activity.Events);

        telemetrySpan.RecordException(null);
        Assert.Empty(activity.Events);
    }

    [Fact]
    public void RecordExceptionDoesNotFormatExceptionWhenNotRecording()
    {
        using var activity = new Activity("exception-test");
        activity.IsAllDataRequested = false;
        using var telemetrySpan = new TelemetrySpan(activity);

        var exception = new ExceptionWithThrowingToString();

        telemetrySpan.RecordException(exception);
    }

    [Fact]
    public void ParentIds()
    {
        using var parentActivity = new Activity("parentOperation");
        parentActivity.Start(); // can't generate the Id until the operation is started
        using var parentSpan = new TelemetrySpan(parentActivity);

        // ParentId should be unset
        Assert.Equal(default, parentSpan.ParentSpanId);
        Assert.NotNull(parentActivity.Id);

        using var childActivity = new Activity("childOperation");
        childActivity.SetParentId(parentActivity.Id);
        using var childSpan = new TelemetrySpan(childActivity);

        Assert.Equal(parentSpan.Context.SpanId, childSpan.ParentSpanId);
    }

    [Fact]
    public void CheckAddLinkData()
    {
        using var activity = new Activity("test-activity");
        activity.Start();
        using var span = new TelemetrySpan(activity);

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var context = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded);

        span.AddLink(context);

        var link = Assert.Single(activity.Links);
        Assert.Equal(traceId, link.Context.TraceId);
        Assert.Equal(spanId, link.Context.SpanId);
        Assert.Null(link.Tags);
    }

    [Fact]
    public void CheckAddLinkAttributes()
    {
        using var activity = new Activity("test-activity");
        activity.Start();
        using var span = new TelemetrySpan(activity);

        var traceId = ActivityTraceId.CreateRandom();
        var spanId = ActivitySpanId.CreateRandom();
        var context = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded);

        var attributes = new SpanAttributes();
        attributes.Add("key1", "value1");

        span.AddLink(context, attributes);

        var link = Assert.Single(activity.Links);
        Assert.NotNull(link.Tags);
        var tag = Assert.Single(link.Tags);
        Assert.Equal("key1", tag.Key);
        Assert.Equal("value1", tag.Value);
    }

    [Fact]
    public void CheckAddLinkNotRecording()
    {
        using var activity = new Activity("test-activity");

        // Simulate not recording
        activity.IsAllDataRequested = false;
        using var span = new TelemetrySpan(activity);

        var context = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None);

        span.AddLink(context, null);

        Assert.Empty(activity.Links);
    }

    [Fact]
    public void SetAttributeOverloadsRecordTags()
    {
        using var activity = new Activity("test-activity");
        using var span = new TelemetrySpan(activity);

        var strings = new[] { "a", null };
        var ints = new[] { 1, 2 };
        var bools = new[] { true, false };
        var doubles = new[] { 1.5, 2.5 };

        Assert.Same(span, span.SetAttribute("string", "value"));
        Assert.Same(span, span.SetAttribute("int", 42));
        Assert.Same(span, span.SetAttribute("bool", true));
        Assert.Same(span, span.SetAttribute("double", 1.25));
        Assert.Same(span, span.SetAttribute("strings", strings));
        Assert.Same(span, span.SetAttribute("ints", ints));
        Assert.Same(span, span.SetAttribute("bools", bools));
        Assert.Same(span, span.SetAttribute("doubles", doubles));

        Assert.Equal("value", activity.GetTagItem("string"));
        Assert.Equal(42, activity.GetTagItem("int"));
        Assert.True(Assert.IsType<bool>(activity.GetTagItem("bool")));
        Assert.Equal(1.25, activity.GetTagItem("double"));
        Assert.Same(strings, activity.GetTagItem("strings"));
        Assert.Same(ints, activity.GetTagItem("ints"));
        Assert.Same(bools, activity.GetTagItem("bools"));
        Assert.Same(doubles, activity.GetTagItem("doubles"));
    }

    [Fact]
    public void AddEventOverloadsRecordEvents()
    {
        using var activity = new Activity("test-activity");
        using var span = new TelemetrySpan(activity);

        var timestamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var attributes = new SpanAttributes();
        attributes.Add("key", "value");

        Assert.Same(span, span.AddEvent("name-only"));
        Assert.Same(span, span.AddEvent("with-timestamp", timestamp));
        Assert.Same(span, span.AddEvent("with-attributes", attributes));
        Assert.Same(span, span.AddEvent("with-both", timestamp, attributes));
        Assert.Same(span, span.AddEvent("null-attributes", (SpanAttributes?)null));
        Assert.Same(span, span.AddEvent("null-attributes-and-timestamp", timestamp, null));

        var events = activity.Events.ToArray();
        Assert.Equal(6, events.Length);

        Assert.Equal("name-only", events[0].Name);
        Assert.Empty(events[0].Tags);

        Assert.Equal("with-timestamp", events[1].Name);
        Assert.Equal(timestamp, events[1].Timestamp);

        Assert.Equal("with-attributes", events[2].Name);
        Assert.Equal("value", Assert.Single(events[2].Tags).Value);

        Assert.Equal("with-both", events[3].Name);
        Assert.Equal(timestamp, events[3].Timestamp);
        Assert.Equal("value", Assert.Single(events[3].Tags).Value);

        Assert.Empty(events[4].Tags);
        Assert.Empty(events[5].Tags);
    }

    [Fact]
    public void AddLinkWithNullAttributesRecordsLink()
    {
        using var activity = new Activity("test-activity");
        using var span = new TelemetrySpan(activity);

        var context = new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

        Assert.Same(span, span.AddLink(context, null));

        var link = Assert.Single(activity.Links);
        Assert.Equal(context.TraceId, link.Context.TraceId);
        Assert.Null(link.Tags);
    }

    [Fact]
    public void UpdateNameChangesDisplayName()
    {
        using var activity = new Activity("original");
        using var span = new TelemetrySpan(activity);

        Assert.Same(span, span.UpdateName("updated"));

        Assert.Equal("updated", activity.DisplayName);
    }

    [Fact]
    public void SetStatusUpdatesActivityStatus()
    {
        using var activity = new Activity("test-activity");
        using var span = new TelemetrySpan(activity);

        span.SetStatus(Status.Error.WithDescription("failed"));

        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("failed", activity.StatusDescription);
    }

    [Fact]
    public void EndStopsActivity()
    {
        using var activity = new Activity("test-activity");
        activity.Start();
        using var span = new TelemetrySpan(activity);

        span.End();

        Assert.True(activity.IsStopped);
    }

    [Fact]
    public void EndWithTimestampSetsEndTimeAndStopsActivity()
    {
        using var activity = new Activity("test-activity");
        activity.Start();
        using var span = new TelemetrySpan(activity);

        var endTimestamp = new DateTimeOffset(activity.StartTimeUtc.AddMinutes(5));

        span.End(endTimestamp);

        Assert.True(activity.IsStopped);
        Assert.Equal(TimeSpan.FromMinutes(5), activity.Duration);
    }

    [Fact]
    public void ActivateSetsCurrentActivity()
    {
        using var activity = new Activity("test-activity");
        activity.Start();
        using var span = new TelemetrySpan(activity);

        var previous = Activity.Current;
        try
        {
            Activity.Current = null;

            span.Activate();

            Assert.Same(activity, Activity.Current);
        }
        finally
        {
            Activity.Current = previous;
        }
    }

    [Fact]
    public void ContextAndIsRecordingReflectActivity()
    {
        using var activity = new Activity("test-activity");
        activity.Start();
        using var span = new TelemetrySpan(activity);

        Assert.True(span.IsRecording);
        Assert.Equal(activity.TraceId, span.Context.TraceId);
        Assert.Equal(activity.SpanId, span.Context.SpanId);

        activity.IsAllDataRequested = false;

        Assert.False(span.IsRecording);
    }

    [Fact]
    public void NotRecordingSpanIgnoresMutations()
    {
        using var activity = new Activity("test-activity");
        activity.IsAllDataRequested = false;
        using var span = new TelemetrySpan(activity);

        var strings = new[] { "a" };
        var ints = new[] { 1 };
        var bools = new[] { true };
        var doubles = new[] { 1.0 };

        span.SetAttribute("string", "value")
            .SetAttribute("int", 1)
            .SetAttribute("bool", true)
            .SetAttribute("double", 1.0)
            .SetAttribute("strings", strings)
            .SetAttribute("ints", ints)
            .SetAttribute("bools", bools)
            .SetAttribute("doubles", doubles)
            .AddEvent("name")
            .AddEvent("name", DateTimeOffset.UtcNow)
            .AddEvent("name", new SpanAttributes())
            .AddEvent("name", DateTimeOffset.UtcNow, new SpanAttributes())
            .AddLink(new SpanContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None))
            .RecordException(new InvalidOperationException("error"))
            .RecordException("type", "message", "stacktrace");

        Assert.Empty(activity.TagObjects);
        Assert.Empty(activity.Events);
        Assert.Empty(activity.Links);
    }

    [Fact]
    public void SpanWithoutActivityIsNoop()
    {
        using var span = new TelemetrySpan(null);

        Assert.False(span.IsRecording);
        Assert.Equal(default, span.Context);
        Assert.Equal(default, span.ParentSpanId);

        Assert.Same(span, span.UpdateName("name"));
        Assert.Same(span, span.SetAttribute("key", "value"));
        Assert.Same(span, span.AddEvent("name"));
        Assert.Same(span, span.AddLink(default));
        Assert.Same(span, span.RecordException(new InvalidOperationException("error")));

        span.SetStatus(Status.Ok);
        span.End();
        span.End(DateTimeOffset.UtcNow);
        span.Activate();
        Assert.Null(Activity.Current);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1064", Justification = "The private exception type is test-only and verifies RecordException does not format unrecorded exceptions.")]
    private sealed class ExceptionWithThrowingToString : Exception
    {
        public ExceptionWithThrowingToString()
        {
        }

        public ExceptionWithThrowingToString(string message)
            : base(message)
        {
        }

        public ExceptionWithThrowingToString(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1065", Justification = "The throwing override verifies RecordException does not format unrecorded exceptions.")]
        public override string ToString() => throw new InvalidOperationException();
    }
}
