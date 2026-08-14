// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace OpenTelemetry.Exporter.Console.Tests;

public class ConsoleKvListAttributeTests
{
    private readonly List<KeyValuePair<string, string>> droppedTags = [];
    private readonly ConsoleTagWriter tagWriter;

    public ConsoleKvListAttributeTests()
    {
        this.tagWriter = new ConsoleTagWriter((key, type) => this.droppedTags.Add(new(key, type)));
    }

    [Fact]
    public void EmptyKvList()
    {
        var kvList = new List<KeyValuePair<string, object?>>();

        Assert.True(this.tagWriter.TryTransformTag("key", kvList, out var result));
        Assert.Equal("key", result.Key);
        Assert.Equal("{}", result.Value);
    }

    [Fact]
    public void KvListWithStringEntries()
    {
        var kvList = new List<KeyValuePair<string, object?>>
        {
            new("innerKey", "innerValue"),
            new("other", "value"),
        };

        Assert.True(this.tagWriter.TryTransformTag("key", kvList, out var result));
        Assert.Equal("key", result.Key);
        Assert.Equal("""{"innerKey":"innerValue","other":"value"}""", result.Value);
    }

    [Fact]
    public void KvListWithNumericAndBooleanEntriesAreWrittenAsJsonLiterals()
    {
        var kvList = new List<KeyValuePair<string, object?>>
        {
            new("int", 1),
            new("long", 2L),
            new("byte", (byte)3),
            new("sbyte", (sbyte)-4),
            new("short", (short)5),
            new("ushort", (ushort)6),
            new("uint", 7u),
            new("double", 1.5d),
            new("float", 2.5f),
            new("boolTrue", true),
            new("boolFalse", false),
        };

        Assert.True(this.tagWriter.TryTransformTag("key", kvList, out var result));
        Assert.Equal(
            """{"int":1,"long":2,"byte":3,"sbyte":-4,"short":5,"ushort":6,"uint":7,"double":1.5,"float":2.5,"boolTrue":true,"boolFalse":false}""",
            result.Value);
    }

    [Fact]
    public void KvListWithNonFiniteFloatingPointEntriesAreWrittenAsStrings()
    {
        // NaN and infinity have no JSON representation, so they are quoted.
        var kvList = new List<KeyValuePair<string, object?>>
        {
            new("nan", double.NaN),
            new("positiveInfinity", double.PositiveInfinity),
            new("negativeInfinity", double.NegativeInfinity),
        };

        Assert.True(this.tagWriter.TryTransformTag("key", kvList, out var result));
        Assert.Equal(
            """{"nan":"NaN","positiveInfinity":"Infinity","negativeInfinity":"-Infinity"}""",
            result.Value);
    }

    [Fact]
    public void KvListWithNullValue()
    {
        var kvList = new List<KeyValuePair<string, object?>>
        {
            new("nullKey", null),
            new("stringKey", "value"),
        };

        Assert.True(this.tagWriter.TryTransformTag("key", kvList, out var result));
        Assert.Equal("""{"nullKey":null,"stringKey":"value"}""", result.Value);
    }

    [Fact]
    public void KvListWithTypesConvertedToStrings()
    {
        // Types the tag writer has no dedicated representation for fall back to
        // Convert.ToString and are quoted.
        var kvList = new List<KeyValuePair<string, object?>>
        {
            new("char", 'x'),
            new("decimal", 1.5m),
            new("ulong", 8ul),
        };

        Assert.True(this.tagWriter.TryTransformTag("key", kvList, out var result));
        Assert.Equal("""{"char":"x","decimal":"1.5","ulong":"8"}""", result.Value);
    }

    [Fact]
    public void KvListWithArrayValues()
    {
        int[] ints = [1, 2, 3];
        string?[] strings = ["a", null];
        bool[] bools = [true, false];

        var kvList = new List<KeyValuePair<string, object?>>
        {
            new("ints", ints),
            new("strings", strings),
            new("bools", bools),
            new("empty", Array.Empty<int>()),
        };

        Assert.True(this.tagWriter.TryTransformTag("key", kvList, out var result));
        Assert.Equal(
            """{"ints":[1,2,3],"strings":["a",null],"bools":[true,false],"empty":[]}""",
            result.Value);
    }

    [Fact]
    public void KvListWithByteArrayValue()
    {
        // The console exporter has no byte array representation, so a byte[] is
        // written as a JSON array of numbers rather than being dropped.
        var kvList = new List<KeyValuePair<string, object?>>
        {
            new("bytes", new byte[] { 1, 2, 3 }),
        };

        Assert.True(this.tagWriter.TryTransformTag("key", kvList, out var result));
        Assert.Equal("""{"bytes":[1,2,3]}""", result.Value);
        Assert.Empty(this.droppedTags);
    }

    [Fact]
    public void NestedKvList()
    {
        var innerKvList = new List<KeyValuePair<string, object?>>
        {
            new("nestedKey", "nestedValue"),
        };
        var outerKvList = new List<KeyValuePair<string, object?>>
        {
            new("inner", innerKvList),
            new("int", 1),
        };

        Assert.True(this.tagWriter.TryTransformTag("key", outerKvList, out var result));
        Assert.Equal("""{"inner":{"nestedKey":"nestedValue"},"int":1}""", result.Value);
    }

    [Fact]
    public void DictionaryAsKvList()
    {
        var dict = new Dictionary<string, object?>
        {
            ["alpha"] = "a",
            ["beta"] = 2L,
        };

        Assert.True(this.tagWriter.TryTransformTag("key", dict, out var result));
        Assert.Equal("""{"alpha":"a","beta":2}""", result.Value);
    }

    [Fact]
    public void KvListKeysAndValuesAreEscaped()
    {
        var kvList = new List<KeyValuePair<string, object?>>
        {
            new("quo\"te", "back\\slash"),
            new("new\nline", "<html>"),
        };

        Assert.True(this.tagWriter.TryTransformTag("key", kvList, out var result));

        // The exact escape sequences are an implementation detail of the JSON
        // encoder, so the value is asserted after decoding.
        using var document = JsonDocument.Parse(result.Value);
        Assert.Equal("back\\slash", document.RootElement.GetProperty("quo\"te").GetString());
        Assert.Equal("<html>", document.RootElement.GetProperty("new\nline").GetString());
    }

    [Fact]
    public void KvListNestedUpToTheDepthLimitIsWrittenAsJson()
    {
        var level3 = new List<KeyValuePair<string, object?>> { new("value", "leaf") };
        var level2 = new List<KeyValuePair<string, object?>> { new("level3", level3) };
        var level1 = new List<KeyValuePair<string, object?>> { new("level2", level2) };

        Assert.True(this.tagWriter.TryTransformTag("key", level1, out var result));
        Assert.Equal("""{"level2":{"level3":{"value":"leaf"}}}""", result.Value);
    }

    [Fact]
    public void KvListBeyondTheDepthLimitFallsBackToAString()
    {
        // A list which contains itself recurses until MaxRecursionDepth is
        // reached, at which point the value is written as a plain string.
        var kvList = SelfReferencingKvList();
        var expectedFallback = Convert.ToString(kvList, CultureInfo.InvariantCulture);

        Assert.True(this.tagWriter.TryTransformTag("key", kvList, out var result));

        using var document = JsonDocument.Parse(result.Value);
        var element = document.RootElement;
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(1L, element.GetProperty("int").GetInt64());

            var self = element.GetProperty("self");
            if (i < 2)
            {
                Assert.Equal(JsonValueKind.Object, self.ValueKind);
                element = self;
                continue;
            }

            // The fallback is a string and not embedded as JSON.
            Assert.Equal(JsonValueKind.String, self.ValueKind);
            Assert.Equal(expectedFallback, self.GetString());
        }
    }

    [Fact]
    public void KvListDepthIsResetBetweenTags()
    {
        // The recursion counter is shared per thread, so writing the same tag
        // twice has to produce the same output.
        var kvList = SelfReferencingKvList();

        Assert.True(this.tagWriter.TryTransformTag("key", kvList, out var first));
        Assert.True(this.tagWriter.TryTransformTag("key", kvList, out var second));

        Assert.Equal(first.Value, second.Value);
    }

    [Fact]
    public void KvListDepthIsNotCorruptedByAThrowingFallback()
    {
        // The value at the depth limit throws while it is being converted to a
        // string. The frame it is written from never increased the recursion
        // depth, so the failure must not decrease it either.
        var throwing = new KvListWithCustomToString(toStringValue: null);
        var level3 = new List<KeyValuePair<string, object?>> { new("throwing", throwing) };
        var level2 = new List<KeyValuePair<string, object?>> { new("level3", level3) };
        var level1 = new List<KeyValuePair<string, object?>> { new("level2", level2) };

        Assert.True(this.tagWriter.TryTransformTag("key", level1, out var result));
        Assert.Equal("""{"level2":{"level3":{}}}""", result.Value);

        // A subsequent tag still stops recursing at the depth limit.
        var kvList = SelfReferencingKvList();

        Assert.True(this.tagWriter.TryTransformTag("key", kvList, out var afterFailure));

        using var document = JsonDocument.Parse(afterFailure.Value);
        var deepest = document.RootElement
            .GetProperty("self")
            .GetProperty("self")
            .GetProperty("self");

        Assert.Equal(JsonValueKind.String, deepest.ValueKind);
    }

    [Fact]
    public void KvListEnumerationFailureDropsTag()
    {
        Assert.False(this.tagWriter.TryTransformTag("key", FaultyKvList(), out var result));
        Assert.Equal(default, result);

        var droppedTag = Assert.Single(this.droppedTags);
        Assert.Equal("key", droppedTag.Key);
    }

    [Fact]
    public void KvListNestedEnumerationFailureDropsEntry()
    {
        var kvList = new List<KeyValuePair<string, object?>>
        {
            new("key1", "value1"),
            new("faulty", FaultyKvList()),
            new("key2", 2),
        };

        Assert.True(this.tagWriter.TryTransformTag("key", kvList, out var result));

        // Only the faulty entry is dropped.
        Assert.Equal("""{"key1":"value1","key2":2}""", result.Value);

        var droppedTag = Assert.Single(this.droppedTags);
        Assert.Equal("faulty", droppedTag.Key);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"fake":1}""")]
    [InlineData("[1,2]")]
    [InlineData("{oops")]
    public void KvListBeyondTheDepthLimitWithJsonLikeToStringIsWrittenAsAString(string toStringValue)
    {
        // The depth limit fallback writes Convert.ToString of the value. A type
        // whose ToString returns text which looks like JSON has to be quoted
        // like any other fallback string instead of being embedded as JSON.
        var fake = new KvListWithCustomToString(toStringValue);
        var level3 = new List<KeyValuePair<string, object?>> { new("fake", fake) };
        var level2 = new List<KeyValuePair<string, object?>> { new("level3", level3) };
        var level1 = new List<KeyValuePair<string, object?>> { new("level2", level2) };

        Assert.True(this.tagWriter.TryTransformTag("key", level1, out var result));

        using var document = JsonDocument.Parse(result.Value);
        var fakeElement = document.RootElement
            .GetProperty("level2")
            .GetProperty("level3")
            .GetProperty("fake");

        Assert.Equal(JsonValueKind.String, fakeElement.ValueKind);
        Assert.Equal(toStringValue, fakeElement.GetString());
    }

    private static IEnumerable<KeyValuePair<string, object?>> FaultyKvList()
    {
        yield return new KeyValuePair<string, object?>("key1", "value1");
        throw new InvalidOperationException("simulated failure");
    }

    private static List<KeyValuePair<string, object?>> SelfReferencingKvList()
    {
        var list = new List<KeyValuePair<string, object?>>();
        list.Add(new("int", 1));
        list.Add(new("self", list));
        return list;
    }

    private sealed class KvListWithCustomToString : IEnumerable<KeyValuePair<string, object?>>
    {
        private readonly string? toStringValue;

        // A null toStringValue makes ToString throw.
        public KvListWithCustomToString(string? toStringValue)
        {
            this.toStringValue = toStringValue;
        }

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            yield return new KeyValuePair<string, object?>("inner", "value");
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public override string ToString()
            => this.toStringValue ?? throw new InvalidOperationException("simulated failure");
    }
}
