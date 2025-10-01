// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using Xunit;

namespace OpenTelemetry.Api.Tests;

#pragma warning disable CA1062 // Validate arguments of public methods
public class PercentEncodingHelperTests
{
    [Theory]
    [InlineData(new string[] { "key1=val1,key2=val2", "key3=val3", "key4=val4" }, new string[] { "key1", "key2", "key3", "key4" }, new string[] { "val1", "val2", "val3", "val4" })] // Multiple headers
    [InlineData(new string[] { "key1=val%201,key2=val2" }, new string[] { "key1", "key2" }, new string[] { "val 1", "val2" })]
    [InlineData(new string[] { "key1,key2=val2" }, new string[] { "key2" }, new string[] { "val2" })]
    [InlineData(new string[] { "key=Am%C3%A9lie" }, new string[] { "key" }, new string[] { "Am\u00E9lie" })] // Valid percent-encoded value
    [InlineData(new string[] { "key1=val1,key2=val2==3" }, new string[] { "key1", "key2" }, new string[] { "val1", "val2==3" })] // Valid value with equal sign
    [InlineData(new string[] { "key1=,key2=val2" }, new string[] { "key2" }, new string[] { "val2" })] // Empty value for key1
    [InlineData(new string[] { "=val1,key2=val2" }, new string[] { "key2" }, new string[] { "val2" })] // Empty key for key1
    [InlineData(new string[] { "Am\u00E9lie=val" }, new string[] { }, new string[] { }, false)] // Invalid key
    [InlineData(new string[] { "key=invalid%encoding" }, new string[] { "key" }, new string[] { "invalid%encoding" })] // Invalid value
    [InlineData(new string[] { "key=v1+v2" }, new string[] { "key" }, new string[] { "v1+v2" })]
#if NET
    [InlineData(new string[] { "key=a%E0%80Am%C3%A9lie" }, new string[] { "key" }, new string[] { "a\uFFFD\uFFFDAm\u00E9lie" })]
#else
    [InlineData(new string[] { "key=a%E0%80Am%C3%A9lie" }, new string[] { "key" }, new string[] { "a\uFFFDAm\u00E9lie" })]
#endif
    public void ValidateBaggageExtraction(string[] baggage, string[] expectedKey, string[] expectedValue, bool canExtractExpected = true)
    {
        var canExtract = PercentEncodingHelper.TryExtractBaggage(baggage, out var extractedBaggage);

        Assert.Equal(canExtractExpected, canExtract);
        if (!canExtractExpected)
        {
            Assert.Null(extractedBaggage);
            return;
        }

        Assert.Equal(expectedKey.Length, extractedBaggage!.Count);
        for (int i = 0; i < expectedKey.Length; i++)
        {
            Assert.True(extractedBaggage!.ContainsKey(expectedKey[i]));
            Assert.Equal(expectedValue[i], extractedBaggage[expectedKey[i]]);
        }
    }

    [Theory]
    [InlineData("key1", "value 1", "key1=value%201")]
    [InlineData("key2", "!x_x,x-x&x(x\");:", "key2=%21x_x%2Cx-x%26x%28x%22%29%3B%3A")]
    [InlineData("key2", """!x_x,x-x&x(x\");:""", "key2=%21x_x%2Cx-x%26x%28x%5C%22%29%3B%3A")]
    public void ValidateBaggageEncoding(string key, string value, string expectedEncoded)
    {
        var encodedValue = PercentEncodingHelper.PercentEncodeBaggage(key, value);
        Assert.Equal(expectedEncoded, encodedValue);
    }

    [Fact]
    public void ValidateBaggageExtraction_ExceedsItemLimit()
    {
        var baggageItems = new List<string>();
        for (int i = 0; i < 200; i++)
        {
            baggageItems.Add($"key{i}=value{i}");
        }

        var baggage = string.Join(",", baggageItems);
        var canExtract = PercentEncodingHelper.TryExtractBaggage([baggage], out var extractedBaggage);

        Assert.True(canExtract);
        Assert.NotNull(extractedBaggage);
        Assert.Equal(180, extractedBaggage!.Count); // Max 180 items
        for (int i = 0; i < 180; i++)
        {
            Assert.True(extractedBaggage!.ContainsKey($"key{i}"));
            Assert.Equal($"value{i}", extractedBaggage[$"key{i}"]);
        }
    }

    [Fact]
    public void ValidateBaggageExtraction_ExceedsLengthLimit()
    {
        var baggage = $"name={new string('x', 8186)},clientId=1234";
        var canExtract = PercentEncodingHelper.TryExtractBaggage([baggage], out var extractedBaggage);

        Assert.True(canExtract);
        Assert.NotNull(extractedBaggage);

        Assert.Single(extractedBaggage!); // Only one item should be extracted due to length limit
        Assert.Equal("name", extractedBaggage!.Keys.First());
        Assert.Equal(new string('x', 8186), extractedBaggage["name"]);
    }
}
