// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Context.Propagation.Tests;

/// <summary>
/// Tests for the metadata-aware extraction path introduced in PR #7376:
/// <see cref="BaggagePropagator.TryExtractBaggageWithMetadata"/> and
/// <see cref="Baggage.GetBaggageWithMetadata"/>.
///
/// These tests sit alongside the existing <see cref="BaggagePropagatorTests"/>
/// rather than inside it so the metadata surface can grow independently.
/// </summary>
public class BaggagePropagatorMetadataTests
{
    private const int MaxBaggageLength = 8192;
    private const int MaxBaggageItems = 180;

    // =========================================================================
    // TryExtractBaggageWithMetadata -- extraction logic
    // =========================================================================

    [Fact]
    public void ExtractWithMetadata_SingleEntryWithMetadata_PopulatesBothDictionaries()
    {
        var headers = new[] { "SomeKey=SomeValue;prop1=v1;prop2" };

        var ok = BaggagePropagator.TryExtractBaggageWithMetadata(
            headers,
            out var baggage,
            out var withMetadata);

        Assert.True(ok);

        // Plain dictionary must carry the decoded value.
        Assert.NotNull(baggage);
        Assert.Equal("SomeValue", baggage["SomeKey"]);

        // Metadata dictionary must carry both value and raw property string.
        Assert.NotNull(withMetadata);
        var entry = withMetadata["SomeKey"];
        Assert.Equal("SomeValue", entry.Value);
        Assert.Equal("prop1=v1;prop2", entry.Metadata);
    }

    [Fact]
    public void ExtractWithMetadata_EntryWithoutMetadata_MetadataIsNull()
    {
        var headers = new[] { "key=value" };

        BaggagePropagator.TryExtractBaggageWithMetadata(
            headers,
            out var baggage,
            out var withMetadata);

        Assert.NotNull(baggage);
        Assert.NotNull(withMetadata);

        var entry = withMetadata["key"];
        Assert.Equal("value", entry.Value);
        Assert.Null(entry.Metadata);
    }

    [Fact]
    public void ExtractWithMetadata_TrailingSemicolonOnly_MetadataIsNull()
    {
        // "key=value;" -- semicolon present but nothing meaningful after it.
        var headers = new[] { "key=value;" };

        BaggagePropagator.TryExtractBaggageWithMetadata(
            headers,
            out _,
            out var withMetadata);

        Assert.NotNull(withMetadata);
        Assert.Null(withMetadata["key"].Metadata);
    }

    [Fact]
    public void ExtractWithMetadata_SemicolonWithOnlyWhitespace_MetadataIsNull()
    {
        // "key=value;   " -- whitespace after semicolon trims to empty -> null.
        var headers = new[] { "key=value;   " };

        BaggagePropagator.TryExtractBaggageWithMetadata(
            headers,
            out _,
            out var withMetadata);

        Assert.NotNull(withMetadata);
        Assert.Null(withMetadata["key"].Metadata);
    }

    [Fact]
    public void ExtractWithMetadata_MixedEntries_EachEntryCorrectlyPopulated()
    {
        // First entry has metadata; second does not.
        var headers = new[] { "key1=val1;meta=foo,key2=val2" };

        BaggagePropagator.TryExtractBaggageWithMetadata(
            headers,
            out var baggage,
            out var withMetadata);

        Assert.NotNull(baggage);
        Assert.NotNull(withMetadata);

        Assert.Equal("val1", withMetadata["key1"].Value);
        Assert.Equal("meta=foo", withMetadata["key1"].Metadata);

        Assert.Equal("val2", withMetadata["key2"].Value);
        Assert.Null(withMetadata["key2"].Metadata);
    }

    [Fact]
    public void ExtractWithMetadata_ValueAndMetadataConsistentWithPlainBaggage()
    {
        // For every key, BaggageEntry.Value must equal the plain baggage value.
        var headers = new[] { "a=1;m=x,b=2,c=3;p" };

        BaggagePropagator.TryExtractBaggageWithMetadata(
            headers,
            out var baggage,
            out var withMetadata);

        Assert.NotNull(baggage);
        Assert.NotNull(withMetadata);
        Assert.Equal(baggage.Count, withMetadata.Count);

        foreach (var key in baggage.Keys)
        {
            Assert.Equal(baggage[key], withMetadata[key].Value);
        }
    }

    [Fact]
    public void ExtractWithMetadata_DuplicateKey_LastWriterWins()
    {
        // Same-key entries in one header value: last one on the wire wins,
        // matching TryExtractBaggage behaviour.
        var headers = new[] { "key=first;meta=old,key=second;meta=new" };

        BaggagePropagator.TryExtractBaggageWithMetadata(
            headers,
            out var baggage,
            out var withMetadata);

        Assert.NotNull(baggage);
        Assert.NotNull(withMetadata);

        Assert.Equal("second", baggage["key"]);
        Assert.Equal("second", withMetadata["key"].Value);
        Assert.Equal("meta=new", withMetadata["key"].Metadata);
    }

    [Fact]
    public void ExtractWithMetadata_EmptyCollection_ReturnsFalseAndBothNull()
    {
        var ok = BaggagePropagator.TryExtractBaggageWithMetadata(
            [],
            out var baggage,
            out var withMetadata);

        Assert.False(ok);
        Assert.Null(baggage);
        Assert.Null(withMetadata);
    }

    [Fact]
    public void ExtractWithMetadata_AllEntriesInvalid_ReturnsFalse()
    {
        // No valid key=value pairs -- neither output should be populated.
        var headers = new[] { "noequals,=orphanvalue" };

        var ok = BaggagePropagator.TryExtractBaggageWithMetadata(
            headers,
            out var baggage,
            out var withMetadata);

        Assert.False(ok);
        Assert.Null(baggage);
        Assert.Null(withMetadata);
    }

    [Fact]
    public void ExtractWithMetadata_OWSAroundValuePreservedInEntry()
    {
        // Optional whitespace around the value must be trimmed before storing,
        // matching TryExtractBaggage. The metadata portion starts after the
        // first semicolon, not after OWS.
        var headers = new[] { "key= SomeValue ;meta=1" };

        BaggagePropagator.TryExtractBaggageWithMetadata(
            headers,
            out var baggage,
            out var withMetadata);

        Assert.NotNull(baggage);
        Assert.Equal("SomeValue", baggage["key"]);
        Assert.Equal("SomeValue", withMetadata!["key"].Value);
        Assert.Equal("meta=1", withMetadata["key"].Metadata);
    }

    [Fact]
    public void ExtractWithMetadata_OWSBeforeSemicolon_TrimmedFromValue()
    {
        // "key=SomeValue ; propertyKey=propertyValue" --
        // the space before ';' is OWS that belongs to the value field, not the
        // metadata, so the stored value must be "SomeValue" (trimmed).
        var headers = new[] { "key=SomeValue ; propertyKey=propertyValue" };

        BaggagePropagator.TryExtractBaggageWithMetadata(
            headers,
            out var baggage,
            out var withMetadata);

        Assert.NotNull(baggage);
        Assert.Equal("SomeValue", baggage["key"]);
        Assert.Equal("SomeValue", withMetadata!["key"].Value);
    }

    [Fact]
    public void ExtractWithMetadata_MaxBaggageLengthEnforced()
    {
        // Build a header that exceeds MaxBaggageLength. The extractor must
        // stop before processing entries that push past the limit, just as
        // TryExtractBaggage does.
        var longValue = new string('x', MaxBaggageLength - "name=".Length);
        var headers = new[] { $"name={longValue},extra=entry" };

        BaggagePropagator.TryExtractBaggageWithMetadata(
            headers,
            out var baggage,
            out var withMetadata);

        Assert.NotNull(baggage);
        Assert.NotNull(withMetadata);
        Assert.False(baggage.ContainsKey("extra"), "Entry beyond MaxBaggageLength must be dropped.");
    }

    [Fact]
    public void ExtractWithMetadata_MaxBaggageItemsEnforced()
    {
        // Build a header with more than MaxBaggageItems valid entries.
        var entries = Enumerable.Range(0, MaxBaggageItems + 20).Select(i => $"k{i:D3}=v");
        var headers = new[] { string.Join(",", entries) };

        BaggagePropagator.TryExtractBaggageWithMetadata(
            headers,
            out var baggage,
            out var withMetadata);

        Assert.NotNull(baggage);
        Assert.NotNull(withMetadata);
        Assert.Equal(MaxBaggageItems, baggage.Count);
        Assert.Equal(MaxBaggageItems, withMetadata.Count);
    }

    [Fact]
    public void ExtractWithMetadata_PercentEncodedValue_DecodedInBothDictionaries()
    {
        // Percent encoding must be decoded the same way as in TryExtractBaggage.
        var headers = new[] { "key=%C3%A9;charset=utf-8" };

        BaggagePropagator.TryExtractBaggageWithMetadata(
            headers,
            out var baggage,
            out var withMetadata);

        Assert.NotNull(baggage);
        Assert.Equal("\u00E9", baggage["key"]);
        Assert.Equal("\u00E9", withMetadata!["key"].Value);
        Assert.Equal("charset=utf-8", withMetadata["key"].Metadata);
    }

    // =========================================================================
    // Baggage struct -- GetBaggageWithMetadata surface
    // =========================================================================

    [Fact]
    public void GetBaggageWithMetadata_AfterMetadataExtraction_ReturnsPopulatedDictionary()
    {
        // Simulate the code path that will eventually be wired into Extract:
        // construct a Baggage using the internal ctor that accepts both
        // dictionaries, then verify GetBaggageWithMetadata surfaces the entries.
        var plain = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["key"] = "value",
        };
        var entries = new Dictionary<string, BaggageEntry>(StringComparer.Ordinal)
        {
            ["key"] = new BaggageEntry("value", "meta=1"),
        };

        // Use the internal two-argument ctor added in PR #7376.
        var b = new Baggage(plain, entries);
        var result = b.GetBaggageWithMetadata();

        Assert.Single(result);
        Assert.Equal("value", result["key"].Value);
        Assert.Equal("meta=1", result["key"].Metadata);
    }

    [Fact]
    public void GetBaggageWithMetadata_ProgrammaticBaggage_ReturnsEmptyDictionary()
    {
        // Baggage constructed without metadata extraction has no wire properties.
        var b = Baggage.Create(new Dictionary<string, string> { ["key"] = "value" });
        var result = b.GetBaggageWithMetadata();

        Assert.Empty(result);
    }

    [Fact]
    public void GetBaggageWithMetadata_DefaultBaggage_ReturnsEmptyDictionary()
    {
        var result = default(Baggage).GetBaggageWithMetadata();
        Assert.Empty(result);
    }

    [Fact]
    public void GetBaggageWithMetadata_ConsistentWithGetBaggage_PerKey()
    {
        // For every key returned by GetBaggageWithMetadata, the Value field
        // must equal what GetBaggage returns for the same key.
        var plain = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["a"] = "1",
            ["b"] = "2",
        };
        var entries = new Dictionary<string, BaggageEntry>(StringComparer.Ordinal)
        {
            ["a"] = new BaggageEntry("1", "meta=x"),
            ["b"] = new BaggageEntry("2", null),
        };

        var b = new Baggage(plain, entries);
        var plainView = b.GetBaggage();
        var metaView = b.GetBaggageWithMetadata();

        foreach (var kvp in metaView)
        {
            Assert.Equal(plainView[kvp.Key], kvp.Value.Value);
        }
    }

    /// <summary>
    /// Known limitation: <see cref="Baggage.Equals"/> and
    /// <see cref="Baggage.GetHashCode"/> only compare the plain baggage
    /// dictionary. Two <see cref="Baggage"/> instances that differ only in
    /// their <c>baggageWithMetadata</c> field compare as equal.
    ///
    /// This is intentional for the current PoC. If equality semantics are
    /// tightened later, this test should be updated or removed.
    /// See https://github.com/open-telemetry/opentelemetry-dotnet/pull/7376.
    /// </summary>
    [Fact]
    public void Equals_BaggagesDifferingOnlyInMetadata_AreConsideredEqual()
    {
        var plain = new Dictionary<string, string>(StringComparer.Ordinal) { ["k"] = "v" };

        var withMeta = new Baggage(
            plain,
            new Dictionary<string, BaggageEntry>(StringComparer.Ordinal)
            {
                ["k"] = new BaggageEntry("v", "prop=1"),
            });

        var withoutMeta = new Baggage(plain);

        Assert.Equal(withMeta, withoutMeta);
        Assert.Equal(withMeta.GetHashCode(), withoutMeta.GetHashCode());
    }

    // =========================================================================
    // BaggageEntry type -- equality contract
    // =========================================================================

    [Fact]
    public void BaggageEntry_EqualityOperator_SameValueAndMetadata_ReturnsTrue()
    {
        var a = new BaggageEntry("val", "meta=1");
        var b = new BaggageEntry("val", "meta=1");

        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void BaggageEntry_EqualityOperator_DifferentValue_ReturnsFalse()
    {
        var a = new BaggageEntry("val1", "meta=1");
        var b = new BaggageEntry("val2", "meta=1");

        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void BaggageEntry_EqualityOperator_DifferentMetadata_ReturnsFalse()
    {
        var a = new BaggageEntry("val", "meta=1");
        var b = new BaggageEntry("val", "meta=2");

        Assert.False(a == b);
    }

    [Fact]
    public void BaggageEntry_EqualityOperator_NullVsNonNullMetadata_ReturnsFalse()
    {
        var a = new BaggageEntry("val", null);
        var b = new BaggageEntry("val", "meta=1");

        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void BaggageEntry_EqualityOperator_BothMetadataNull_ReturnsTrue()
    {
        var a = new BaggageEntry("val", null);
        var b = new BaggageEntry("val", null);

        Assert.True(a == b);
    }

    [Fact]
    public void BaggageEntry_Equals_BoxedObject_ReturnsTrue()
    {
        var a = new BaggageEntry("val", "meta=1");
        object boxed = new BaggageEntry("val", "meta=1");

        Assert.True(a.Equals(boxed));
    }

    [Fact]
    public void BaggageEntry_Equals_WrongType_ReturnsFalse()
    {
        var a = new BaggageEntry("val", "meta=1");
        Assert.False(a.Equals("not a BaggageEntry"));
    }

    [Fact]
    public void BaggageEntry_GetHashCode_EqualInstances_SameHash()
    {
        var a = new BaggageEntry("val", "meta=1");
        var b = new BaggageEntry("val", "meta=1");

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void BaggageEntry_GetHashCode_NullMetadata_DoesNotThrow()
    {
        var a = new BaggageEntry("val", null);
        _ = a.GetHashCode(); // must not throw
    }

    [Fact]
    public void BaggageEntry_GetHashCode_DifferentInstances_DifferentHash()
    {
        // Hash collisions are theoretically possible but highly unlikely for
        // short strings with a 17/23 mixing polynomial.
        var a = new BaggageEntry("val", "meta=1");
        var b = new BaggageEntry("val", "meta=2");

        Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void BaggageEntry_ValueAndMetadataProperties_RoundTripCorrectly()
    {
        var entry = new BaggageEntry("decoded-value", "prop1=v1;prop2");

        Assert.Equal("decoded-value", entry.Value);
        Assert.Equal("prop1=v1;prop2", entry.Metadata);
    }
}
