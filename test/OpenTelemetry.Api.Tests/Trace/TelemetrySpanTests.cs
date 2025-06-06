// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class TelemetrySpanTests
{
    [Fact]
    public void CheckRecordExceptionData()
    {
        string message = "message";

        using Activity activity = new Activity("exception-test");
        using TelemetrySpan telemetrySpan = new TelemetrySpan(activity);
        telemetrySpan.RecordException(new ArgumentNullException(message, new InvalidOperationException("new-exception")));
        Assert.Single(activity.Events);

        Assert.NotNull(telemetrySpan.Activity);
        var @event = telemetrySpan.Activity.Events.FirstOrDefault(q => q.Name == SemanticConventions.AttributeExceptionEventName);
        Assert.Equal(message, @event.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionMessage).Value);
        Assert.Equal(typeof(ArgumentNullException).Name, @event.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionType).Value);
    }

    [Fact]
    public void CheckRecordExceptionData2()
    {
        string type = "ArgumentNullException";
        string message = "message";
        string stack = "stack";

        using Activity activity = new Activity("exception-test");
        using TelemetrySpan telemetrySpan = new TelemetrySpan(activity);
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
        using Activity activity = new Activity("exception-test");
        using TelemetrySpan telemetrySpan = new TelemetrySpan(activity);
        telemetrySpan.RecordException(string.Empty, string.Empty, string.Empty);
        Assert.Empty(activity.Events);

        telemetrySpan.RecordException(null);
        Assert.Empty(activity.Events);
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

        Assert.Single(activity.Links);
        var link = activity.Links.First();
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

        Assert.Single(activity.Links);
        var link = activity.Links.First();
        Assert.NotNull(link.Tags);
        Assert.Single(link.Tags);
        var tag = link.Tags.First();
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
}
