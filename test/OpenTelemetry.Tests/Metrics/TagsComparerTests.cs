// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET9_0_OR_GREATER
using System.Collections.Concurrent;
#endif

namespace OpenTelemetry.Metrics.Tests;

public class TagsComparerTests
{
    [Fact]
    public void Equals_And_GetHashCode_DelegateToTags()
    {
        var comparer = TagsComparer.Instance;

        var tags1 = new Tags(
        [
            new("key1", "value1"),
            new("key2", 123),
        ]);

        var tags2 = new Tags(
        [
            new("key1", "value1"),
            new("key2", 123),
        ]);

        var different = new Tags(
        [
            new("key1", "value1"),
            new("key2", 456),
        ]);

        Assert.True(comparer.Equals(tags1, tags2));
        Assert.Equal(comparer.GetHashCode(tags1), comparer.GetHashCode(tags2));
        Assert.False(comparer.Equals(tags1, different));
    }

#if NET9_0_OR_GREATER
    [Fact]
    public void AlternateGetHashCode_Span_MatchesTagsHashCode()
    {
        var comparer = TagsComparer.Instance;

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

        Assert.Equal(tags.GetHashCode(), comparer.GetHashCode(span));
    }

    [Fact]
    public void AlternateEquals_Span_MatchesContent()
    {
        var comparer = TagsComparer.Instance;

        var tags = new Tags(
        [
            new("key1", "value1"),
            new("key2", 123),
        ]);

        ReadOnlySpan<KeyValuePair<string, object?>> equal =
        [
            new("key1", "value1"),
            new("key2", 123),
        ];

        ReadOnlySpan<KeyValuePair<string, object?>> different =
        [
            new("key1", "value1"),
            new("key2", 456),
        ];

        Assert.True(comparer.Equals(equal, tags));
        Assert.False(comparer.Equals(different, tags));
    }

    [Fact]
    public void Create_Span_ProducesEqualTags()
    {
        var comparer = TagsComparer.Instance;

        ReadOnlySpan<KeyValuePair<string, object?>> span =
        [
            new("key1", "value1"),
            new("key2", 123),
        ];

        var created = comparer.Create(span);

        var expected = new Tags(
        [
            new("key1", "value1"),
            new("key2", 123),
        ]);

        Assert.True(created.Equals(expected));
        Assert.Equal(expected.GetHashCode(), created.GetHashCode());
    }

    [Fact]
    public void AlternateLookup_ResolvesEntryAddedAsTags()
    {
        // End-to-end: a ConcurrentDictionary keyed by Tags (using TagsComparer)
        // must resolve a lookup performed with a tags span, mirroring how the
        // metrics hot path resolves an existing MetricPoint.
        var dictionary = new ConcurrentDictionary<Tags, int>(TagsComparer.Instance);

        var key = new Tags(
        [
            new("key1", "value1"),
            new("key2", 123),
        ]);

        dictionary[key] = 42;

        var lookup = dictionary.GetAlternateLookup<ReadOnlySpan<KeyValuePair<string, object?>>>();

        // A different backing array with identical contents must hit.
        ReadOnlySpan<KeyValuePair<string, object?>> probe =
        [
            new("key1", "value1"),
            new("key2", 123),
        ];

        Assert.True(lookup.TryGetValue(probe, out var value));
        Assert.Equal(42, value);

        ReadOnlySpan<KeyValuePair<string, object?>> miss =
        [
            new("key1", "value1"),
        ];

        Assert.False(lookup.TryGetValue(miss, out _));
    }
#endif
}
