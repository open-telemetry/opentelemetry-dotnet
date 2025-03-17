// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class ActivityExtensionsTests
{
    private const string ActivityName = "Test Activity";

    [Fact]
    public void SetStatus()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .Build();

        using var source = new ActivitySource(activitySourceName);
        using var activity = source.StartActivity(ActivityName);
        activity.SetStatus(Status.Ok);
        activity?.Stop();

        Assert.Equal(Status.Ok, activity.GetStatus());
    }

    [Fact]
    public void SetStatusWithDescription()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .Build();

        using var source = new ActivitySource(activitySourceName);
        using var activity = source.StartActivity(ActivityName);
        activity.SetStatus(Status.Error.WithDescription("Not Found"));
        activity?.Stop();

        var status = activity.GetStatus();
        Assert.Equal(StatusCode.Error, status.StatusCode);
        Assert.Equal("Not Found", status.Description);
    }

    [Fact]
    public void SetStatusWithDescriptionTwice()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .Build();

        using var source = new ActivitySource(activitySourceName);
        using var activity = source.StartActivity(ActivityName);
        activity.SetStatus(Status.Error.WithDescription("Not Found"));
        activity.SetStatus(Status.Ok);
        activity?.Stop();

        var status = activity.GetStatus();
        Assert.Equal(StatusCode.Ok, status.StatusCode);
        Assert.Null(status.Description);
    }

    [Fact]
    public void SetStatusWithIgnoredDescription()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .Build();

        using var source = new ActivitySource(activitySourceName);
        using var activity = source.StartActivity(ActivityName);
        activity.SetStatus(Status.Ok.WithDescription("This should be ignored."));
        activity?.Stop();

        var status = activity.GetStatus();
        Assert.Equal(StatusCode.Ok, status.StatusCode);
        Assert.Null(status.Description);
    }

    [Fact]
    public void SetCancelledStatus()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .Build();

        using var source = new ActivitySource(activitySourceName);
        using var activity = source.StartActivity(ActivityName);
        activity.SetStatus(Status.Error);
        activity?.Stop();

        Assert.True(activity.GetStatus().StatusCode.Equals(Status.Error.StatusCode));
    }

    [Fact]
    public void GetStatusWithNoStatusInActivity()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .Build();

        using var source = new ActivitySource(activitySourceName);
        using var activity = source.StartActivity(ActivityName);
        activity?.Stop();

        Assert.Equal(Status.Unset, activity.GetStatus());
    }

    [Fact]
    public void LastSetStatusWins()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .Build();

        using var source = new ActivitySource(activitySourceName);
        using var activity = source.StartActivity(ActivityName);
        activity.SetStatus(Status.Error);
        activity.SetStatus(Status.Ok);
        activity?.Stop();

        Assert.Equal(Status.Ok, activity.GetStatus());
    }

    [Fact]
    public void CheckRecordException()
    {
        var message = "message";
        var exception = new ArgumentNullException(message, new Exception(message));
        using var activity = new Activity("test-activity");
        activity.RecordException(exception);

        var @event = activity.Events.FirstOrDefault(e => e.Name == SemanticConventions.AttributeExceptionEventName);
        Assert.Equal(message, @event.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionMessage).Value);
        Assert.Equal("System.ArgumentNullException", @event.Tags.FirstOrDefault(t => t.Key == SemanticConventions.AttributeExceptionType).Value);
    }

    [Fact]
    public void RecordExceptionWithAdditionalTags()
    {
        var message = "message";
        var exception = new ArgumentNullException(message, new Exception(message));
        using var activity = new Activity("test-activity");

        var tags = new TagList
        {
            { "key1", "value1" },
            { "key2", "value2" },
        };

        activity.RecordException(exception, tags);

        // Additional tags passed in override attributes added from the exception
        tags.Add(SemanticConventions.AttributeExceptionMessage, "SomeOtherExceptionMessage");
        tags.Add(SemanticConventions.AttributeExceptionType, "SomeOtherExceptionType");

        activity.RecordException(exception, tags);

        var events = activity.Events.ToArray();
        Assert.Equal(2, events.Length);

        Assert.Equal(SemanticConventions.AttributeExceptionEventName, events[0].Name);
        Assert.Equal(message, events[0].Tags.First(t => t.Key == SemanticConventions.AttributeExceptionMessage).Value);
        Assert.Equal("System.ArgumentNullException", events[0].Tags.First(t => t.Key == SemanticConventions.AttributeExceptionType).Value);
        Assert.Equal("value1", events[0].Tags.First(t => t.Key == "key1").Value);
        Assert.Equal("value2", events[0].Tags.First(t => t.Key == "key2").Value);

        Assert.Equal(SemanticConventions.AttributeExceptionEventName, events[1].Name);
        Assert.Equal("SomeOtherExceptionMessage", events[1].Tags.First(t => t.Key == SemanticConventions.AttributeExceptionMessage).Value);
        Assert.Equal("SomeOtherExceptionType", events[1].Tags.First(t => t.Key == SemanticConventions.AttributeExceptionType).Value);
        Assert.Equal("value1", events[1].Tags.First(t => t.Key == "key1").Value);
        Assert.Equal("value2", events[1].Tags.First(t => t.Key == "key2").Value);
    }

    [Fact]
    public void GetTagValueEmpty()
    {
        using var activity = new Activity("Test");

        Assert.Null(activity.GetTagValue("Tag1"));
    }

    [Fact]
    public void GetTagValue()
    {
        using var activity = new Activity("Test");
        activity.SetTag("Tag1", "Value1");

        Assert.Equal("Value1", activity.GetTagValue("Tag1"));
        Assert.Null(activity.GetTagValue("tag1"));
        Assert.Null(activity.GetTagValue("Tag2"));
    }

    [Theory]
    [InlineData("Key", "Value", true)]
    [InlineData("CustomTag", null, false)]
    public void TryCheckFirstTag(string tagName, object? expectedTagValue, bool expectedResult)
    {
        using var activity = new Activity("Test");
        activity.SetTag("Key", "Value");

        var result = activity.TryCheckFirstTag(tagName, out var tagValue);
        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedTagValue, tagValue);
    }

    [Fact]
    public void TryCheckFirstTagReturnsFalseForActivityWithNoTags()
    {
        using var activity = new Activity("Test");

        var result = activity.TryCheckFirstTag("Key", out var tagValue);
        Assert.False(result);
        Assert.Null(tagValue);
    }
}
