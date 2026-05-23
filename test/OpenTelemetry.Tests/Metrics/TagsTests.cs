// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class TagsTests
{
    [Fact]
    public void Equals_ReturnsTrue_ForEqualTags()
    {
        var tag1 = new Tags(
        [
            new("key1", "value1"),
            new("key2", 123),
        ]);

        var tag2 = new Tags(
        [
            new("key1", "value1"),
            new("key2", 123),
        ]);

        Assert.True(tag1.Equals(tag2));
        Assert.True(tag1 == tag2);
    }

    [Fact]
    public void Equals_ReturnsFalse_ForDifferentValues()
    {
        var tag1 = new Tags(
        [
            new("key1", "value1"),
            new("key2", 123),
        ]);

        var tag2 = new Tags(
        [
            new("key1", "value1"),
            new("key2", 456),
        ]);

        Assert.False(tag1.Equals(tag2));
        Assert.True(tag1 != tag2);
    }

    [Fact]
    public void Equals_ReturnsFalse_ForDifferentLengths()
    {
        var tag1 = new Tags(
        [
            new("key1", "value1"),
        ]);

        var tag2 = new Tags(
        [
            new("key1", "value1"),
            new("key2", "value2"),
        ]);

        Assert.False(tag1.Equals(tag2));
    }
}
