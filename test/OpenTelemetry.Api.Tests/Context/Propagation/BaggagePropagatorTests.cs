// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Xunit;

namespace OpenTelemetry.Context.Propagation.Tests;

public class BaggagePropagatorTests
{
    private static readonly Func<IDictionary<string, string>, string, IEnumerable<string>> Getter =
        (d, k) =>
        {
            if (d.TryGetValue(k, out var v))
            {
                return [v];
            }

            return [];
        };

    private static readonly Func<IList<KeyValuePair<string, string>>, string, IEnumerable<string>> GetterList =
        (d, k) =>
        {
            return d.Where(i => i.Key == k).Select(i => i.Value);
        };

    private static readonly Action<IDictionary<string, string>, string, string> Setter = (carrier, name, value) =>
    {
        carrier[name] = value;
    };

    private readonly BaggagePropagator baggage = new();

    [Fact]
    public void ValidateFieldsProperty()
    {
        Assert.Equal(new HashSet<string> { BaggagePropagator.BaggageHeaderName }, this.baggage.Fields);
        Assert.Single(this.baggage.Fields);
    }

    [Fact]
    public void ValidateDefaultCarrierExtraction()
    {
        var propagationContext = this.baggage.Extract<string>(default, null!, null!);
        Assert.Equal(default, propagationContext);
    }

    [Fact]
    public void ValidateDefaultGetterExtraction()
    {
        var carrier = new Dictionary<string, string>();
        var propagationContext = this.baggage.Extract(default, carrier, null!);
        Assert.Equal(default, propagationContext);
    }

    [Fact]
    public void ValidateNoBaggageExtraction()
    {
        var carrier = new Dictionary<string, string>();
        var propagationContext = this.baggage.Extract(default, carrier, Getter);
        Assert.Equal(default, propagationContext);
    }

    [Fact]
    public void ValidateOneBaggageExtraction()
    {
        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, "name=test" },
        };

        var propagationContext = this.baggage.Extract(default, carrier, Getter);
        Assert.False(propagationContext == default);
        Assert.Single(propagationContext.Baggage.GetBaggage());

        var baggage = propagationContext.Baggage.GetBaggage().FirstOrDefault();

        Assert.Equal("name", baggage.Key);
        Assert.Equal("test", baggage.Value);
    }

    [Fact]
    public void ValidateMultipleBaggageExtraction()
    {
        var carrier = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>(BaggagePropagator.BaggageHeaderName, "name1=test1"),
            new KeyValuePair<string, string>(BaggagePropagator.BaggageHeaderName, "name2=test2"),
            new KeyValuePair<string, string>(BaggagePropagator.BaggageHeaderName, "name2=test2"),
        };

        var propagationContext = this.baggage.Extract(default, carrier, GetterList);

        Assert.False(propagationContext == default);
        Assert.True(propagationContext.ActivityContext == default);

        Assert.Equal(2, propagationContext.Baggage.Count);

        var array = propagationContext.Baggage.GetBaggage().ToArray();

        Assert.Equal("name1", array[0].Key);
        Assert.Equal("test1", array[0].Value);

        Assert.Equal("name2", array[1].Key);
        Assert.Equal("test2", array[1].Value);
    }

    [Fact]
    public void ValidateLongBaggageExtraction()
    {
        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, $"name={new string('x', 8186)},clientId=1234" },
        };

        var propagationContext = this.baggage.Extract(default, carrier, Getter);
        Assert.False(propagationContext == default);
        Assert.Single(propagationContext.Baggage.GetBaggage());

        var array = propagationContext.Baggage.GetBaggage().ToArray();

        Assert.Equal("name", array[0].Key);
        Assert.Equal(new string('x', 8186), array[0].Value);
    }

    [Fact]
    public void ValidateSpecialCharsBaggageExtraction()
    {
        var encodedKey = WebUtility.UrlEncode("key2");
        var encodedValue = WebUtility.UrlEncode("!x_x,x-x&x(x\");:");
        var escapedKey = Uri.EscapeDataString("key()3");
        var escapedValue = Uri.EscapeDataString("value()!&;:");

        Assert.Equal("key2", encodedKey);
        Assert.Equal("!x_x%2Cx-x%26x(x%22)%3B%3A", encodedValue);
        Assert.Equal("key%28%293", escapedKey);
        Assert.Equal("value%28%29%21%26%3B%3A", escapedValue);

        var initialBaggage = $"key+1=value+1,{encodedKey}={encodedValue},{escapedKey}={escapedValue}";
        var carrier = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>(BaggagePropagator.BaggageHeaderName, initialBaggage),
        };

        var propagationContext = this.baggage.Extract(default, carrier, GetterList);

        Assert.False(propagationContext == default);
        Assert.True(propagationContext.ActivityContext == default);

        Assert.Equal(3, propagationContext.Baggage.Count);

        var actualBaggage = propagationContext.Baggage.GetBaggage();

        Assert.Equal(3, actualBaggage.Count);

        Assert.True(actualBaggage.ContainsKey("key+1"));
        Assert.Equal("value+1", actualBaggage["key+1"]);

        Assert.True(actualBaggage.ContainsKey("key2"));
        Assert.Equal("!x_x,x-x&x(x\");:", actualBaggage["key2"]);

        Assert.True(!actualBaggage.ContainsKey("key()3"));
        Assert.Equal("value()!&;:", actualBaggage["key()3"]);
    }

    [Fact]
    public void ValidateEmptyBaggageInjection()
    {
        var carrier = new Dictionary<string, string>();
        this.baggage.Inject(default, carrier, Setter);

        Assert.Empty(carrier);
    }

    [Fact]
    public void ValidateBaggageInjection()
    {
        var carrier = new Dictionary<string, string>();
        var propagationContext = new PropagationContext(
            default,
            new Baggage(new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" },
            }));

        this.baggage.Inject(propagationContext, carrier, Setter);

        Assert.Single(carrier);
        Assert.Equal("key1=value1,key2=value2", carrier[BaggagePropagator.BaggageHeaderName]);
    }

    [Fact]
    public void ValidateSpecialCharsBaggageInjection()
    {
        var carrier = new Dictionary<string, string>();
        var propagationContext = new PropagationContext(
            default,
            new Baggage(new Dictionary<string, string>
            {
                { "key 1", "value 1" },
                { "key2", "!x_x,x-x&x(x\");:" },
            }));

        this.baggage.Inject(propagationContext, carrier, Setter);

        Assert.Single(carrier);
        Assert.Equal("key+1=value+1,key2=!x_x%2Cx-x%26x(x%22)%3B%3A", carrier[BaggagePropagator.BaggageHeaderName]);
    }

    [Fact]
    public void ValidateMultipleEqualsInValue()
    {
        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, "SomeKey=SomeValue=equals" },
        };

        var propagationContext = this.baggage.Extract(default, carrier, Getter);
        Assert.Single(propagationContext.Baggage.GetBaggage());

        var baggage = propagationContext.Baggage.GetBaggage().FirstOrDefault();

        Assert.Equal("SomeKey", baggage.Key);
        Assert.Equal("SomeValue=equals", baggage.Value);
    }

    [Fact]
    public void ValidateEmptyValueSkipped()
    {
        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, "SomeKey=" },
        };

        var propagationContext = this.baggage.Extract(default, carrier, Getter);
        Assert.Empty(propagationContext.Baggage.GetBaggage());
    }

    [Fact]
    public void ValidateOWSOnExtraction()
    {
        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, "SomeKey \t = \t SomeValue \t , \t SomeKey2 \t = \t SomeValue2" },
        };

        var propagationContext = this.baggage.Extract(default, carrier, Getter);

        Assert.Equal(2, propagationContext.Baggage.GetBaggage().Count);

        var baggage = propagationContext.Baggage.GetBaggage().ToArray();

        Assert.Equal("SomeKey", baggage[0].Key);
        Assert.Equal("SomeValue", baggage[0].Value);

        Assert.Equal("SomeKey2", baggage[1].Key);
        Assert.Equal("SomeValue2", baggage[1].Value);
    }

    [Fact]
    public void ValidateSemicolonMetadataIgnoredOnExtraction()
    {
        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, "SomeKey=SomeValue;metadata" },
        };

        var propagationContext = this.baggage.Extract(default, carrier, Getter);
        Assert.Single(propagationContext.Baggage.GetBaggage());

        var baggage = propagationContext.Baggage.GetBaggage().FirstOrDefault();

        Assert.Equal("SomeKey", baggage.Key);
        Assert.Equal("SomeValue", baggage.Value);
    }

    [Fact]
    public void ValidateOptionalWhiteSpaceExtractionDoesNotCorruptOnReinjection()
    {
        // Simulates a header emitted by .NET 10's W3C propagator
        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, "correlationId = 12345, userId = user-abc" },
        };

        var extractedContext = this.baggage.Extract(default, carrier, Getter);

        var outboundCarrier = new Dictionary<string, string>();
        this.baggage.Inject(extractedContext, outboundCarrier, Setter);

        Assert.Equal("correlationId=12345,userId=user-abc", outboundCarrier[BaggagePropagator.BaggageHeaderName]);
    }

    [Fact]
    public void ValidateOptionalWhiteSpaceBeforeSemicolonIgnored()
    {
        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, "SomeKey=SomeValue ; propertyKey=propertyValue" },
        };

        var propagationContext = this.baggage.Extract(default, carrier, Getter);

        var baggage = Assert.Single(propagationContext.Baggage.GetBaggage());

        Assert.Equal("SomeKey", baggage.Key);
        Assert.Equal("SomeValue", baggage.Value);
    }

    [Fact]
    public void ValidatePercentEncoding()
    {
        var originalValue = "\t \"\';=asdf!@#$%^&*()";
        var encodedValue = Uri.EscapeDataString(originalValue);

        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, $"SomeKey={encodedValue}" },
        };

        var propagationContext = this.baggage.Extract(default, carrier, Getter);
        Assert.Single(propagationContext.Baggage.GetBaggage());

        var baggage = propagationContext.Baggage.GetBaggage().FirstOrDefault();

        Assert.Equal("SomeKey", baggage.Key);
        Assert.Equal(originalValue, baggage.Value);
    }

    [Fact]
    public void ValidateInvalidFormatSkipped()
    {
        var carrier = new Dictionary<string, string>
        {
            // "noequals" has no = sign, "=orphanvalue" has no key
            // "validkey=validvalue," has trailing comma
            { BaggagePropagator.BaggageHeaderName, "noequals,=orphanvalue,validkey=validvalue," },
        };
        var propagationContext = this.baggage.Extract(default, carrier, Getter);
        Assert.Single(propagationContext.Baggage.GetBaggage());

        var baggage = propagationContext.Baggage.GetBaggage().FirstOrDefault();

        Assert.Equal("validkey", baggage.Key);
        Assert.Equal("validvalue", baggage.Value);
    }

    [Fact]
    public void ValidatePercentEncodedComplexCharactersDecodesCorrectly()
    {
        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, "SomeKey=%09%20%22%27%3B%3Dasdf%21%40%23%24%25%5E%26%2A%28%29" },
        };

        var propagationContext = this.baggage.Extract(default, carrier, Getter);

        Assert.Single(propagationContext.Baggage.GetBaggage());

        var baggage = propagationContext.Baggage.GetBaggage().FirstOrDefault();

        Assert.Equal("SomeKey", baggage.Key);
        Assert.Equal("\t \"';=asdf!@#$%^&*()", baggage.Value);
    }

    [Fact]
    public void ValidateInjectionOfSixtyFourEntries()
    {
        var baggageDict = new Dictionary<string, string>();
        for (var i = 0; i < 64; i++)
        {
            baggageDict[$"key{i}"] = "value";
        }

        var propagationContext = new PropagationContext(default, new Baggage(baggageDict));

        var carrier = new Dictionary<string, string>();

        this.baggage.Inject(propagationContext, carrier, Setter);

        Assert.Single(carrier);

        var baggageHeader = carrier[BaggagePropagator.BaggageHeaderName];
        var entries = baggageHeader.Split(',');

        Assert.Equal(64, entries.Length);
    }

    [Fact]
    public void ValidateInjectionOf8192Bytes()
    {
        var longValue = new string('0', 8190);

        var propagationContext = new PropagationContext(
            default,
            new Baggage(new Dictionary<string, string>
            {
                { "a", longValue },
            }));

        var carrier = new Dictionary<string, string>();

        this.baggage.Inject(propagationContext, carrier, Setter);

        Assert.Single(carrier);

        var baggageHeader = carrier[BaggagePropagator.BaggageHeaderName];

        Assert.Equal(8192, baggageHeader.Length);
    }

    [Fact]
    public void ValidateMaxByteManyEntriesInjection()
    {
        var baggageDict = new Dictionary<string, string>();

        for (var i = 0; i < 512; i++)
        {
            baggageDict[$"{i:D3}"] = "0123456789a";
        }

        var propagationContext = new PropagationContext(default, new Baggage(baggageDict));

        var carrier = new Dictionary<string, string>();

        this.baggage.Inject(propagationContext, carrier, Setter);

        Assert.Single(carrier);

        var baggageHeader = carrier[BaggagePropagator.BaggageHeaderName];

        Assert.True(baggageHeader.Length <= 8192);
    }

    [Fact]
    public void ValidateRoundTripPreservesData()
    {
        var originalBaggage = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value with spaces" },
            { "key3", "special!@#$%^&*()" },
        };

        var propagationContext = new PropagationContext(default, new Baggage(originalBaggage));

        var carrier = new Dictionary<string, string>();
        this.baggage.Inject(propagationContext, carrier, Setter);

        var extractedContext = this.baggage.Extract(default, carrier, Getter);
        var extractedBaggage = extractedContext.Baggage.GetBaggage();

        Assert.Equal(3, extractedBaggage.Count);
        Assert.Equal("value1", extractedBaggage["key1"]);
        Assert.Equal("value with spaces", extractedBaggage["key2"]);
        Assert.Equal("special!@#$%^&*()", extractedBaggage["key3"]);
    }

    [Fact]
    public void ValidateValueWithMultipleEqualsPreservesEquals()
    {
        var propagationContext = new PropagationContext(
            default,
            new Baggage(new Dictionary<string, string>
            {
                { "key", "value=more=equals" },
            }));

        var carrier = new Dictionary<string, string>();

        this.baggage.Inject(propagationContext, carrier, Setter);

        var extractedContext = this.baggage.Extract(default, carrier, Getter);
        var extractedBaggage = extractedContext.Baggage.GetBaggage();

        Assert.Single(extractedBaggage);
        Assert.Equal("value=more=equals", extractedBaggage["key"]);
    }

    [Fact]
    public void ValidateSpecialCharactersInjectionForValue()
    {
        var propagationContext = new PropagationContext(
            default,
            new Baggage(new Dictionary<string, string>
            {
                { "key", "\t \"';=asdf!@#$%^&*()" },
            }));

        var carrier = new Dictionary<string, string>();

        this.baggage.Inject(propagationContext, carrier, Setter);

        var baggageHeader = carrier[BaggagePropagator.BaggageHeaderName];

        Assert.Contains("%09", baggageHeader, StringComparison.Ordinal);
        Assert.Contains("%20", baggageHeader, StringComparison.Ordinal);
        Assert.Contains("%22", baggageHeader, StringComparison.Ordinal);

        var extractedContext = this.baggage.Extract(default, carrier, Getter);
        var extractedBaggage = extractedContext.Baggage.GetBaggage();

        Assert.Equal("\t \"';=asdf!@#$%^&*()", extractedBaggage["key"]);
    }

    [Fact]
    public void KeyValidTcharSymbolInjectedUnchanged()
    {
        var propagationContext = new PropagationContext(
            default,
            new Baggage(new Dictionary<string, string>
            {
                { "my.key-name_here", "value" },
            }));

        var carrier = new Dictionary<string, string>();
        this.baggage.Inject(propagationContext, carrier, Setter);

        Assert.Equal("my.key-name_here=value", carrier[BaggagePropagator.BaggageHeaderName]);
    }

    // -------------------------------------------------------------------------
    // Keys — incorrect path
    // The current implementation URL-decodes keys on extract (#5479).
    // These tests document what the correct behaviour SHOULD be: keys arriving
    // on the wire that happen to contain %-sequences or '+' must be treated as
    // literal token strings — they are NOT decoded.
    //
    // '%' is a valid tchar, so "key%20name" is a valid token whose name is
    // literally "key%20name", not "key name". '+' is also a valid tchar.
    // -------------------------------------------------------------------------
    [Fact]
    public void KeyPercentSequenceInKeyPreservedLiterallyOnExtract()
    {
        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, "key%20name=value,valid-key=valid-value" },
        };

        var context = this.baggage.Extract(default, carrier, Getter);
        var baggage = context.Baggage.GetBaggage();

        Assert.Equal(2, baggage.Count);
        Assert.True(baggage.ContainsKey("key%20name"));
        Assert.False(baggage.ContainsKey("key name"));
    }

    [Fact]
    public void KeyPlusInKeyPreservedLiterallyOnExtract()
    {
        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, "key+name=value" },
        };

        var context = this.baggage.Extract(default, carrier, Getter);
        var entry = Assert.Single(context.Baggage.GetBaggage());

        Assert.Equal("key+name", entry.Key);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\"")]
    [InlineData("(")]
    [InlineData(")")]
    [InlineData(",")]
    [InlineData("/")]
    [InlineData(":")]
    [InlineData(";")]
    [InlineData("<")]
    [InlineData("=")]
    [InlineData(">")]
    [InlineData("?")]
    [InlineData("@")]
    [InlineData("[")]
    [InlineData("\\")]
    [InlineData("]")]
    [InlineData("{")]
    [InlineData("}")]
    public void KeyWithDelimiterCharEntirePairDroppedOnExtract(string delimiter)
    {
        var invalidKey = $"key{delimiter}name";
        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, $"{invalidKey}=should-drop,valid-key=valid-value" },
        };

        var context = this.baggage.Extract(default, carrier, Getter);
        var entry = Assert.Single(context.Baggage.GetBaggage());

        Assert.Equal("valid-key", entry.Key);
        Assert.Equal("valid-value", entry.Value);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("(")]
    [InlineData(":")]
    [InlineData(";")]
    [InlineData("@")]
    public void KeyWithDelimiterCharEntirePairDroppedOnInject(string delimiter)
    {
        var propagationContext = new PropagationContext(
            default,
            new Baggage(new Dictionary<string, string>
            {
                { $"invalid{delimiter}key", "should-be-dropped" },
                { "valid-key", "valid-value" },
            }));

        var carrier = new Dictionary<string, string>();
        this.baggage.Inject(propagationContext, carrier, Setter);

        Assert.Single(carrier);
        Assert.Equal("valid-key=valid-value", carrier[BaggagePropagator.BaggageHeaderName]);
    }

    [Theory]
    [InlineData("!")]
    [InlineData("#")]
    [InlineData("&")]
    [InlineData("*")]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData("/")]
    [InlineData(":")]
    [InlineData("=")]
    [InlineData("?")]
    [InlineData("@")]
    [InlineData("~")]
    public void ValueValidBaggageOctetCharPassesThroughUnchanged(string octet)
    {
        // These characters are valid unencoded baggage-octets and must not be
        // transformed on either inject or extract.
        var value = $"val{octet}ue";
        var propagationContext = new PropagationContext(
            default,
            new Baggage(new Dictionary<string, string> { { "key", value } }));

        var carrier = new Dictionary<string, string>();
        this.baggage.Inject(propagationContext, carrier, Setter);

        // Confirm it was injected without encoding
        Assert.Contains(value, carrier[BaggagePropagator.BaggageHeaderName], StringComparison.Ordinal);

        // Confirm it round-trips back unchanged
        var extracted = this.baggage.Extract(default, carrier, Getter);
        Assert.Equal(value, extracted.Baggage.GetBaggage()["key"]);
    }

    [Theory]
    [InlineData("v=1")]
    [InlineData("v=1=2")]
    [InlineData("a=b=c=d")]
    public void ValueWithEqualsSignsExtractedCorrectly(string value)
    {
        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, $"key={value}" },
        };

        var context = this.baggage.Extract(default, carrier, Getter);
        var entry = Assert.Single(context.Baggage.GetBaggage());

        Assert.Equal("key", entry.Key);
        Assert.Equal(value, entry.Value);
    }

    [Theory]
    [InlineData("key%201=value%201", "key%201", "value 1")]
    [InlineData("key=val+ue", "key", "val+ue")]
    [InlineData("key=val%2Bue", "key", "val+ue")]
    [InlineData("key=val%20ue", "key", "val ue")]
    [InlineData("key=value=1", "key", "value=1")]
    [InlineData("key=%20%21%22%23%24%25%26%27%28%29%2A%2B%2C-.%2F0123456789%3A%3B%3C%3D%3E%3F%40ABCDEFGHIJKLMNOPQRSTUVWXYZ%5B%5C%5D%5E_%60abcdefghijklmnopqrstuvwxyz%7B%7C%7D~", "key", " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~")]
    public void ValidateMiscTests(string propagatedBaggage, string key, string expectedDecodedValue)
    {
        var carrier = new List<KeyValuePair<string, string>>
        {
            new(BaggagePropagator.BaggageHeaderName, propagatedBaggage),
        };

        var propagationContext = this.baggage.Extract(default, carrier, GetterList);

        Assert.False(propagationContext == default);
        Assert.True(propagationContext.ActivityContext == default);

        Assert.Equal(1, propagationContext.Baggage.Count);

        var actualBaggage = propagationContext.Baggage.GetBaggage();

        Assert.Single(actualBaggage);

        Assert.Contains(key, actualBaggage);
        Assert.Equal(expectedDecodedValue, actualBaggage[key]);
    }

    [Fact]
    public void ValidatePlusInValueIsLiteralOnExtract()
    {
        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, "key=value+1" },
        };

        var propagationContext = this.baggage.Extract(default, carrier, Getter);
        Assert.Single(propagationContext.Baggage.GetBaggage());

        var entry = propagationContext.Baggage.GetBaggage().First();
        Assert.Equal("key", entry.Key);
        Assert.Equal("value+1", entry.Value); // '+' stays as '+', not ' '
    }

    [Theory]
    [InlineData("!")]
    [InlineData("#")]
    [InlineData("$")]
    [InlineData("%")]
    [InlineData("&")]
    [InlineData("'")]
    [InlineData("*")]
    [InlineData("+")]
    [InlineData("-")]
    [InlineData(".")]
    [InlineData("^")]
    [InlineData("_")]
    [InlineData("`")]
    [InlineData("|")]
    [InlineData("~")]
    public void ValidateValidTcharInKeyIsAcceptedOnExtract(string specialChar)
    {
        // This test is for all tchar characters are valid.
        var key = $"key{specialChar}name";
        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, $"prefix{key}suffix=value" },
        };

        var propagationContext = this.baggage.Extract(default, carrier, Getter);
        Assert.Single(propagationContext.Baggage.GetBaggage());
        Assert.Equal(key, propagationContext.Baggage.GetBaggage().First().Key);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\"")]
    [InlineData("(")]
    [InlineData(")")]
    [InlineData(",")]
    [InlineData("/")]
    [InlineData(":")]
    [InlineData(";")]
    [InlineData("<")]
    [InlineData("=")]
    [InlineData(">")]
    [InlineData("?")]
    [InlineData("@")]
    [InlineData("[")]
    [InlineData("\\")]
    [InlineData("]")]
    [InlineData("{")]
    [InlineData("}")]
    public void ValidateKeyWithInvalidTcharDroppedOnExtract(string invalidChar)
    {
        var invalidKey = $"key{invalidChar}name";
        var carrier = new Dictionary<string, string>
        {
            {
                BaggagePropagator.BaggageHeaderName,
                $"{invalidKey}=should-be-dropped,valid-key=valid-value"
            },
        };

        var propagationContext = this.baggage.Extract(default, carrier, Getter);
        Assert.Single(propagationContext.Baggage.GetBaggage());
        Assert.Equal("valid-key", propagationContext.Baggage.GetBaggage().First().Key);
    }

    [Theory]
    [InlineData("%21", "!")]
    [InlineData("%22", "\"")]
    [InlineData("%23", "#")]
    [InlineData("%24", "$")]
    [InlineData("%25", "%")]
    [InlineData("%26", "&")]
    [InlineData("%27", "'")]
    [InlineData("%28", "(")]
    [InlineData("%29", ")")]
    [InlineData("%2A", "*")]
    [InlineData("%2B", "+")]
    [InlineData("%2C", ",")]
    [InlineData("%2D", "-")]
    [InlineData("%2E", ".")]
    [InlineData("%2F", "/")]
    [InlineData("%3A", ":")]
    [InlineData("%3B", ";")]
    [InlineData("%3C", "<")]
    [InlineData("%3D", "=")]
    [InlineData("%3E", ">")]
    [InlineData("%3F", "?")]
    [InlineData("%40", "@")]
    [InlineData("%5B", "[")]
    [InlineData("%5C", "\\")]
    [InlineData("%5D", "]")]
    [InlineData("%5E", "^")]
    [InlineData("%5F", "_")]
    [InlineData("%60", "`")]
    [InlineData("%7B", "{")]
    [InlineData("%7C", "|")]
    [InlineData("%7D", "}")]
    [InlineData("%7E", "~")]
    [InlineData("%20", " ")]
    public void ValidatePercentEncodedValueCharacterDecodesCorrectly(string encoded, string expected)
    {
        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, $"key=prefix{encoded}suffix" },
        };

        var propagationContext = this.baggage.Extract(default, carrier, Getter);
        Assert.Single(propagationContext.Baggage.GetBaggage());
        Assert.Equal(expected, propagationContext.Baggage.GetBaggage().First().Value);
    }

    [Theory]
    [InlineData("key=%")]
    [InlineData("key=%2")]
    [InlineData("key=%GG")]
    public void ValidateMalformedPercentSequenceInValueIsHandledGracefully(string headerValue)
    {
        var carrier = new Dictionary<string, string>
        {
            { BaggagePropagator.BaggageHeaderName, headerValue },
        };

        var exception = Record.Exception(() =>
            this.baggage.Extract(default, carrier, Getter));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(" ", "%20")]
    [InlineData("\"", "%22")]
    [InlineData(",", "%2C")]
    [InlineData(";", "%3B")]
    [InlineData("\\", "%5C")]
    public void ValidateCharacterOutsideBaggageOctetIsPercentEncodedOnInject(
        string rawChar, string expectedEncoded)
    {
        var propagationContext = new PropagationContext(
            default,
            new Baggage(new Dictionary<string, string> { { "key", $"val{rawChar}ue" } }));

        var carrier = new Dictionary<string, string>();
        this.baggage.Inject(propagationContext, carrier, Setter);

        Assert.Single(carrier);
        Assert.Contains(expectedEncoded, carrier[BaggagePropagator.BaggageHeaderName], StringComparison.Ordinal);
    }

    // =========================================================================
    // ROUND-TRIP
    // These tests inject baggage and then extract from the same carrier,
    // verifying that the full pipeline preserves the original data.
    // =========================================================================

    [Fact]
    public void RoundTripValueWithMultipleEqualsPreservedExactly()
    {
        var carrier = new Dictionary<string, string>();
        this.baggage.Inject(
            new PropagationContext(default, new Baggage(new Dictionary<string, string> { { "key", "value=more=equals" }, })), carrier, Setter);

        var extracted = this.baggage.Extract(default, carrier, Getter).Baggage.GetBaggage();
        Assert.Equal("value=more=equals", extracted["key"]);
    }

    [Fact]
    public void RoundTripValueWithSpacePreservedAsSpace()
    {
        var carrier = new Dictionary<string, string>();
        this.baggage.Inject(
            new PropagationContext(default, new Baggage(new Dictionary<string, string> { { "key", "value with space" }, })), carrier, Setter);

        // The intermediate header must use %20, not '+'
        Assert.Contains("%20", carrier[BaggagePropagator.BaggageHeaderName], StringComparison.Ordinal);
        Assert.DoesNotContain("+", carrier[BaggagePropagator.BaggageHeaderName], StringComparison.Ordinal);

        var extracted = this.baggage.Extract(default, carrier, Getter).Baggage.GetBaggage();
        Assert.Equal("value with space", extracted["key"]);
    }

    [Fact]
    public void RoundTripValueWithAllMandatoryEncodeCharsPreservedExactly()
    {
        const string original = "val ue\"wi,th;back\\slash";

        var carrier = new Dictionary<string, string>();
        this.baggage.Inject(new PropagationContext(default, new Baggage(new Dictionary<string, string> { { "key", original }, })), carrier, Setter);

        var extracted = this.baggage.Extract(default, carrier, Getter).Baggage.GetBaggage();
        Assert.Equal(original, extracted["key"]);
    }

    [Fact]
    public void RoundTripMixedValidAndInvalidKeysOnlyValidKeysSurvive()
    {
        var carrier = new Dictionary<string, string>();
        this.baggage.Inject(
            new PropagationContext(default, new Baggage(new Dictionary<string, string> { { "valid-key",  "valid-value" }, { "invalid key", "should-be-dropped" }, })), carrier, Setter);

        var extracted = this.baggage.Extract(default, carrier, Getter).Baggage.GetBaggage();
        Assert.Single(extracted);
        Assert.Equal("valid-value", extracted["valid-key"]);
    }
}
