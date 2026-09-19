// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

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

    [Fact]
    public void Equals_ReturnsFalse_ForDifferentKeysWithMatchingFingerprintsAndEqualValues()
    {
        var tag1 = new Tags([new("abXcd", true)]);
        var tag2 = new Tags([new("abYcd", true)]);

        Assert.Equal(tag1.GetHashCode(), tag2.GetHashCode());
        Assert.False(tag1.Equals(tag2));
        Assert.True(tag1 != tag2);

        ReadOnlySpan<KeyValuePair<string, object?>> span = [new("abYcd", true)];
        Assert.False(tag1.Equals(span));
        Assert.True(tag2.Equals(span));
    }

    [Fact]
    public void ComputeHashCode_DistinguishesSameLengthKeysVaryingInPrefixOrSuffix()
    {
        var hashes = new HashSet<int>();

        for (var i = 0; i < 100; i++)
        {
            var suffix = i.ToString("D2", CultureInfo.InvariantCulture);
            hashes.Add(Tags.ComputeHashCode([new("flag_" + suffix, true)]));
            hashes.Add(Tags.ComputeHashCode([new(suffix + "_flag", true)]));
        }

        Assert.Equal(200, hashes.Count);
        Assert.NotEqual(Tags.ComputeHashCode([new(string.Empty, true)]), Tags.ComputeHashCode([new("a", true)]));
        Assert.NotEqual(Tags.ComputeHashCode([new("a", true)]), Tags.ComputeHashCode([new("b", true)]));
    }
}
