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
    public void ComputeHashCode_IsIndependentOfKeyStringInstance()
    {
        var literalKey = "key1";
        var copiedKey = new string(literalKey.ToCharArray());
        Assert.NotSame(literalKey, copiedKey);

        var expected = Tags.ComputeHashCode([new(literalKey, "value1")]);

        Assert.Equal(expected, Tags.ComputeHashCode([new(copiedKey, "value1")]));
        Assert.Equal(expected, Tags.ComputeHashCode([new(literalKey, "value1")]));
        Assert.Equal(expected, new Tags([new(copiedKey, "value1")]).GetHashCode());
    }

    [Fact]
    public void ComputeHashCode_KeysSharingACacheSlotKeepTheirOwnHashes()
    {
        var key1 = "abXcd";
        var key2 = "abYcd";

        var expected1 = Tags.ComputeHashCode([new(new string(key1.ToCharArray()), true)]);
        var expected2 = Tags.ComputeHashCode([new(new string(key2.ToCharArray()), true)]);

        Assert.NotEqual(expected1, expected2);

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(expected1, Tags.ComputeHashCode([new(key1, true)]));
            Assert.Equal(expected2, Tags.ComputeHashCode([new(key2, true)]));
        }

        var hashes = new HashSet<int>();

        for (var i = 0; i < 100; i++)
        {
            hashes.Add(Tags.ComputeHashCode([new("ab" + i.ToString("D3", CultureInfo.InvariantCulture) + "cd", true)]));
        }

        Assert.True(hashes.Count >= 99, $"Only {hashes.Count} distinct hashes for 100 distinct keys.");
    }

#if NETFRAMEWORK
    [Fact]
    public void ComputeHashCode_LegacyCollidingStringValuesDoNotShareAHashCode()
    {
        var values = CreateLegacyCollidingValues(256);

        Assert.Equal(values.Length, values.Distinct(StringComparer.Ordinal).Count());

        var hashes = values
            .Select(value => new Tags([new("tenant", value)]).GetHashCode())
            .Distinct()
            .Count();

        Assert.True(hashes >= values.Length - 1, $"Only {hashes} distinct hashes for {values.Length} distinct values.");
    }

    [Fact]
    public void Lookup_DoesNotConcentrateLegacyCollidingValuesInOneBucket()
    {
        const int CardinalityLimit = 2000;

        var comparer = new CountingTagsComparer();
        var lookup = new System.Collections.Concurrent.ConcurrentDictionary<Tags, int>(comparer);

        var values = CreateLegacyCollidingValues(CardinalityLimit + 1);

        for (var i = 0; i < CardinalityLimit; i++)
        {
            Assert.True(lookup.TryAdd(new Tags([new("tenant", values[i])]), i));
        }

        comparer.EqualsCalls = 0;
        Assert.False(lookup.TryGetValue(new Tags([new("tenant", values[CardinalityLimit])]), out _));
        var legacyCollidingComparisons = comparer.EqualsCalls;

        comparer.EqualsCalls = 0;
        Assert.False(lookup.TryGetValue(new Tags([new("tenant", "benign-value")]), out _));
        var benignComparisons = comparer.EqualsCalls;

        Assert.True(legacyCollidingComparisons <= 8, $"Expected at most a handful of comparisons, but a legacy-colliding lookup performed {legacyCollidingComparisons}.");
        Assert.True(benignComparisons <= 8, $"Expected at most a handful of comparisons, but a benign lookup performed {benignComparisons}.");
    }

    [Fact]
    public void Lookup_DoesNotConcentrateLegacyCollidingKeysInOneBucket()
    {
        const int CardinalityLimit = 2000;

        var comparer = new CountingTagsComparer();
        var lookup = new System.Collections.Concurrent.ConcurrentDictionary<Tags, int>(comparer);

        var keys = CreateLegacyCollidingValues(CardinalityLimit + 1);

        for (var i = 0; i < CardinalityLimit; i++)
        {
            Assert.True(lookup.TryAdd(new Tags([new(keys[i], "value")]), i));
        }

        comparer.EqualsCalls = 0;
        Assert.False(lookup.TryGetValue(new Tags([new(keys[CardinalityLimit], "value")]), out _));
        var legacyCollidingComparisons = comparer.EqualsCalls;

        comparer.EqualsCalls = 0;
        Assert.False(lookup.TryGetValue(new Tags([new("benign-key", "value")]), out _));
        var benignComparisons = comparer.EqualsCalls;

        Assert.True(legacyCollidingComparisons <= 8, $"Expected at most a handful of comparisons, but a legacy-colliding-key lookup performed {legacyCollidingComparisons}.");
        Assert.True(benignComparisons <= 8, $"Expected at most a handful of comparisons, but a benign lookup performed {benignComparisons}.");
    }

    private static string[] CreateLegacyCollidingValues(int count)
        => [.. Enumerable.Range(0, count).Select(i => "\0" + new string('A', 64) + i.ToString("D5", CultureInfo.InvariantCulture))];

    private sealed class CountingTagsComparer : IEqualityComparer<Tags>
    {
        public int EqualsCalls { get; set; }

        public bool Equals(Tags x, Tags y)
        {
            this.EqualsCalls++;
            return TagsComparer.Instance.Equals(x, y);
        }

        public int GetHashCode(Tags obj) => TagsComparer.Instance.GetHashCode(obj);
    }
#endif
}
