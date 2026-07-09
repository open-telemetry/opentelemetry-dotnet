// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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

    [Fact]
    public void Equals_Span_ReturnsTrue_ForEqualContents()
    {
        var tags = new Tags(
        [
            new("key1", "value1"),
            new("key2", 123),
        ]);

        // A different backing array with identical contents must be equal.
        ReadOnlySpan<KeyValuePair<string, object?>> span =
        [
            new("key1", "value1"),
            new("key2", 123),
        ];

        Assert.True(tags.Equals(span));
    }

    [Fact]
    public void Equals_Span_ReturnsFalse_ForDifferentValues()
    {
        var tags = new Tags(
        [
            new("key1", "value1"),
            new("key2", 123),
        ]);

        ReadOnlySpan<KeyValuePair<string, object?>> span =
        [
            new("key1", "value1"),
            new("key2", 456),
        ];

        Assert.False(tags.Equals(span));
    }

    [Fact]
    public void Equals_Span_ReturnsFalse_ForDifferentLengths()
    {
        var tags = new Tags(
        [
            new("key1", "value1"),
        ]);

        ReadOnlySpan<KeyValuePair<string, object?>> span =
        [
            new("key1", "value1"),
            new("key2", "value2"),
        ];

        Assert.False(tags.Equals(span));
    }

    [Fact]
    public void ComputeHashCode_Span_MatchesInstanceHashCode()
    {
        // The span-based hash must exactly match the hash of an equivalent Tags
        // instance; otherwise the metrics alternate lookup would silently miss
        // and fall back to the slower path.
        var tags = new Tags(
        [
            new("key1", "value1"),
            new("key2", 123),
        ]);

        ReadOnlySpan<KeyValuePair<string, object?>> span =
        [
            new("key1", "value1"),
            new("key2", 123),
        ];

        Assert.Equal(tags.GetHashCode(), Tags.ComputeHashCode(span));
    }
}
