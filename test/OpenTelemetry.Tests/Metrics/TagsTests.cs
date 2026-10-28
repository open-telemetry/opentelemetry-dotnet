// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics.Tests;

public class TagsTests
{
    public static TheoryData<object[]> GetEqualWideValues()
    {
        var ticks = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc).Ticks;
        object[][] equalValues =
        [
            [0.0, -0.0],
            [double.NaN, -double.NaN, BitConverter.Int64BitsToDouble(0x7FF0000000000001)],
            [0m, 0.00m, new decimal(0, 0, 0, true, 0), new decimal(0, 0, 0, true, 28)],
            [1m, 1.0m, 1.0000000000000000000000000000m],
            [-120m, -120.0m, -120.000m],
            [0.5m, 0.50m, 0.5000000000000000000000000000m],
            [7922816251426433759354395033m, 7922816251426433759354395033.0m],
            [new decimal(0, 0, 10, false, 0), new decimal(0, 0, 100, false, 1)],
            [new DateTime(ticks, DateTimeKind.Utc), new DateTime(ticks, DateTimeKind.Local)],
            [
                new DateTimeOffset(ticks, TimeSpan.Zero),
                new DateTimeOffset(ticks + TimeSpan.FromHours(2).Ticks, TimeSpan.FromHours(2)),
            ],
        ];

        return new TheoryData<object[]>(equalValues);
    }

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

    [Theory]
    [InlineData(typeof(long))]
    [InlineData(typeof(ulong))]
    [InlineData(typeof(double))]
    [InlineData(typeof(decimal))]
    [InlineData(typeof(Guid))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(DateTimeOffset))]
    [InlineData(typeof(TimeSpan))]
    public void Lookup_DoesNotConcentrateCollidingWideValuesInOneBucket(Type type)
    {
        const int CardinalityLimit = 2000;

        var values = Enumerable.Range(1, CardinalityLimit + 1)
            .Select(i => CreateCollidingWideValue(type, (uint)i))
            .ToArray();

        Assert.Equal(values.Length, values.Distinct().Count());
        Assert.Single(values.Select(value => value.GetHashCode()).Distinct());

        var comparer = new CountingTagsComparer();
        var lookup = new System.Collections.Concurrent.ConcurrentDictionary<Tags, int>(comparer);

        for (var i = 0; i < CardinalityLimit; i++)
        {
            Assert.True(lookup.TryAdd(new Tags([new("customer", values[i])]), i));
        }

        comparer.EqualsCalls = 0;
        Assert.False(lookup.TryGetValue(new Tags([new("customer", values[CardinalityLimit])]), out _));

        Assert.True(
            comparer.EqualsCalls <= 8,
            $"A lookup with {type} values performed {comparer.EqualsCalls} comparisons.");

        for (var i = 0; i < CardinalityLimit; i++)
        {
            Assert.True(lookup.TryGetValue(new Tags([new("customer", values[i])]), out var index));
            Assert.Equal(i, index);
        }
    }

    [Fact]
    public void ComputeHashCode_GuidValuesUseEveryByte()
    {
        for (var index = 0; index < 16; index++)
        {
            var hashes = new HashSet<int>();

            for (var value = 1; value <= byte.MaxValue; value++)
            {
                var bytes = new byte[16];
                bytes[index] = (byte)value;
                hashes.Add(Tags.ComputeHashCode([new("customer", new Guid(bytes))]));
            }

            Assert.True(hashes.Count >= 250, $"Only {hashes.Count} distinct hashes for Guid byte {index}.");
        }
    }

    [Fact]
    public void ComputeHashCode_WideValuesWhoseHalvesCancelOutInSystemHashCodeAreDistributed()
    {
        // Adding the 32-bit halves separately to System.HashCode still allows
        // collisions: the contribution from one word can cancel the next one.
        const uint InversePrime3 = 2828982549U;
        const uint Prime4 = 668265263U;

        var hashes = new HashSet<int>();

        for (uint i = 0; i < 2048; i++)
        {
            var bits = unchecked(((ulong)(0U - (i * 64U * Prime4 * InversePrime3)) << 32)
                | ((i << 21) * InversePrime3));
            hashes.Add(Tags.ComputeHashCode([new("customer", unchecked((long)bits))]));
        }

        Assert.True(hashes.Count >= 2040, $"Only {hashes.Count} distinct hashes for 2048 distinct values.");
    }

    [Theory]
    [MemberData(nameof(GetEqualWideValues))]
    public void ComputeHashCode_EqualWideValuesShareAHashCode(object[] values)
    {
        Guard.ThrowIfNull(values);

        var expected = new Tags([new("key", values[0])]);

        foreach (var value in values)
        {
            var actual = new Tags([new("key", value)]);

            Assert.Equal(values[0], value);
            Assert.True(
                expected.GetHashCode() == actual.GetHashCode(),
                $"Equal {value.GetType()} values {values[0]} and {value} have different tag hashes.");
            Assert.Equal(expected.GetHashCode(), Tags.ComputeHashCode([new("key", value)]));
            Assert.True(expected.Equals(actual));
            Assert.True(expected.Equals([new("key", value)]));
        }
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
#endif

    private static object CreateCollidingWideValue(Type type, uint i)
    {
        // These types fold their words with XOR in GetHashCode. Equal words
        // cancel, even though the values themselves are distinct.
        var bits = ((ulong)i << 32) | i;

        return type switch
        {
            _ when type == typeof(long) => (long)bits,
            _ when type == typeof(ulong) => bits,
            _ when type == typeof(double) => BitConverter.Int64BitsToDouble((long)bits),
#if NETFRAMEWORK
            // Framework hashes decimals through double, which loses these low bits,
            // and combines a different subset of a Guid's fields.
            _ when type == typeof(decimal) => new decimal((int)i, 0, 1, false, 0),
            _ when type == typeof(Guid) => new Guid((int)i, 0, (short)i, 0, 0, 0, 0, 0, 0, 0, 0),
#else
            _ when type == typeof(decimal) => new decimal((int)i, (int)i, 0, false, 0),
            _ when type == typeof(Guid) => new Guid((int)i, 0, 0, (byte)i, (byte)(i >> 8), 0, 0, 0, 0, 0, 0),
#endif
            _ when type == typeof(DateTime) => new DateTime((long)bits),
            _ when type == typeof(DateTimeOffset) => new DateTimeOffset((long)bits, TimeSpan.Zero),
            _ when type == typeof(TimeSpan) => new TimeSpan((long)bits),
            _ => throw new NotSupportedException(),
        };
    }

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
}
