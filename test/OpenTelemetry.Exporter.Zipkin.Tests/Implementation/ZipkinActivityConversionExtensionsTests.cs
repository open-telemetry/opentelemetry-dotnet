// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;
using Xunit;

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
        using var activity = new Activity("TestActivity");
        activity.SetTag(key, value);

        var tags = PooledList<KeyValuePair<string, object?>>.Create();
        ExtractTags(activity, ref tags);

        var tag = Assert.Single(tags);
        Assert.Equal(key, tag.Key);
        Assert.Equal(value, tag.Value);
    }

    [Theory]
    [InlineData("int", null)]
    [InlineData("string", null)]
    [InlineData("bool", null)]
    [InlineData("double", null)]
    public void CheckNullValueProcessTag(string key, object? value)
    {
        using var activity = new Activity("TestActivity");
        activity.SetTag(key, value);

        var tags = PooledList<KeyValuePair<string, object?>>.Create();
        ExtractTags(activity, ref tags);

        Assert.Empty(tags);
    }

    private static void ExtractTags(Activity activity, ref PooledList<KeyValuePair<string, object?>> tags)
    {
        foreach (var tag in activity.TagObjects)
        {
            if (tag.Value != null)
            {
                PooledList<KeyValuePair<string, object?>>.Add(ref tags, new KeyValuePair<string, object?>(tag.Key, tag.Value));
            }
        }
    }
}
