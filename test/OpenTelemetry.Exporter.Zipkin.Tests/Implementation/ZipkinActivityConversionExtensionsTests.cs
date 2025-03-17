// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;
using Xunit;
using static OpenTelemetry.Exporter.Zipkin.Implementation.ZipkinActivityConversionExtensions;

namespace OpenTelemetry.Exporter.Zipkin.Implementation.Tests;

public class ZipkinActivityConversionExtensionsTests
{
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
}
