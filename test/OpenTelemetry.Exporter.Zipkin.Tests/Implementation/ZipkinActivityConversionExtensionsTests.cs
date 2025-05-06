// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter.Zipkin.Tests.TestUtility;
using OpenTelemetry.Internal;
using Xunit;
using static OpenTelemetry.Exporter.Zipkin.Implementation.ZipkinActivityConversionExtensions;

namespace OpenTelemetry.Exporter.Zipkin.Implementation.Tests;

public class ZipkinActivityConversionExtensionsTests : IDisposable
{
    private readonly ActivityListener _listener;

    public ZipkinActivityConversionExtensionsTests()
    {
        // Setup activity listener once for all tests
        _listener = new ActivityListener
        {
            ShouldListenTo = s => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [Theory]
    [InlineData("int", 1)]
    [InlineData("string", "s")]
    [InlineData("bool", true)]
    [InlineData("double", 1.0)]
    public void CheckProcessTag(string key, object value)
    {
        var attributeEnumerationState = new TagEnumerationState
        {
            Tags = PooledList<KeyValuePair<string, object?>>.Create(),
        };

        using var activity = new Activity("TestActivity");
        activity.SetTag(key, value);

        attributeEnumerationState.EnumerateTags(activity);

        Assert.Equal(key, attributeEnumerationState.Tags[0].Key);
        Assert.Equal(value, attributeEnumerationState.Tags[0].Value);
    }

    [Theory]
    [InlineData("int", null)]
    [InlineData("string", null)]
    [InlineData("bool", null)]
    [InlineData("double", null)]
    public void CheckNullValueProcessTag(string key, object? value)
    {
        var attributeEnumerationState = new TagEnumerationState
        {
            Tags = PooledList<KeyValuePair<string, object?>>.Create(),
        };

        using var activity = new Activity("TestActivity");
        activity.SetTag(key, value);

        attributeEnumerationState.EnumerateTags(activity);

        Assert.Empty(attributeEnumerationState.Tags);
    }


    [Fact]
    public void EnumerateTags_WithActivitySourceTags_ProcessesSourceTagsWithPrefix()
    {
        // Arrange
        var attributeEnumerationState = CreateAttributeEnumerationState();
        var source = ActivitySourceBuilder.Create("TestSource")
            .WithVersion("1.0.0")
            .WithTag("source-tag", "source-value")
            .Build();
        using var activity = source.StartActivity("TestActivity");

        // Act
        attributeEnumerationState.EnumerateTags(activity);

        // Assert
        Assert.Contains(attributeEnumerationState.Tags, tag =>
            tag.Key == "instrumentation.scope.source-tag" && tag.Value?.ToString() == "source-value");
    }

    [Fact]
    public void EnumerateTags_WithNullActivitySourceTags_IgnoresNullValues()
    {
        // Arrange
        var attributeEnumerationState = CreateAttributeEnumerationState();
        var source = ActivitySourceBuilder.Create("TestSource")
            .WithVersion("1.0.0")
            .WithTag("null-tag", null)
            .Build();
        using var activity = source.StartActivity("TestActivity");

        // Act
        attributeEnumerationState.EnumerateTags(activity);

        // Assert
        Assert.Empty(attributeEnumerationState.Tags);
    }

    [Fact]
    public void EnumerateTags_WithEmptyActivitySourceAndActivityTag_ProcessesActivityTag()
    {
        // Arrange
        var attributeEnumerationState = CreateAttributeEnumerationState();
        var source = ActivitySourceBuilder.Create("TestSource")
            .WithVersion("1.0.0")
            .Build();
        using var activity = source.StartActivity("TestActivity");
        activity.SetTag("activity-tag", "value");

        // Act
        attributeEnumerationState.EnumerateTags(activity);

        // Assert
        Assert.Single(attributeEnumerationState.Tags);
        Assert.Equal("activity-tag", attributeEnumerationState.Tags[0].Key);
        Assert.Equal("value", attributeEnumerationState.Tags[0].Value);
    }

    [Fact]
    public void EnumerateTags_WithSpecialSourceTags_SetsSpecialProperties()
    {
        // Arrange
        var attributeEnumerationState = CreateAttributeEnumerationState();
        var source = ActivitySourceBuilder.Create("TestSource")
            .WithVersion("1.0.0")
            .WithTag("peer.service", "test-service")
            .Build();
        using var activity = source.StartActivity("TestActivity");

        // Act
        attributeEnumerationState.EnumerateTags(activity);

        // Assert
        Assert.Equal("test-service", attributeEnumerationState.PeerService);
    }

    private TagEnumerationState CreateAttributeEnumerationState() => new TagEnumerationState
    {
        Tags = PooledList<KeyValuePair<string, object?>>.Create(),
    };

    public void Dispose()
    {
        _listener?.Dispose();
    }
}
