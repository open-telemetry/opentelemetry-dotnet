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
        telemetrySpan.RecordException(new ArgumentNullException(message, new Exception("new-exception")));
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
}
