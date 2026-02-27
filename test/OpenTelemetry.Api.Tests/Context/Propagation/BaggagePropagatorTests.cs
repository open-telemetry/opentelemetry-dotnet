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

        Assert.True(actualBaggage.ContainsKey("key 1"));
        Assert.Equal("value 1", actualBaggage["key 1"]);

        Assert.True(actualBaggage.ContainsKey("key2"));
        Assert.Equal("!x_x,x-x&x(x\");:", actualBaggage["key2"]);

        Assert.True(actualBaggage.ContainsKey("key()3"));
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

    [Fact(Skip = "Fails due to spec mismatch, tracked in https://github.com/open-telemetry/opentelemetry-dotnet/issues/5210")]
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

    [Fact(Skip = "Fails due to spec mismatch, tracked in https://github.com/open-telemetry/opentelemetry-dotnet/issues/5210")]
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
        for (int i = 0; i < 64; i++)
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

        for (int i = 0; i < 512; i++)
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

    [Fact(Skip = "Fails due to spec mismatch, tracked in https://github.com/open-telemetry/opentelemetry-dotnet/issues/5210")]
    public void ValidateSpecialCharactersInjection()
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

        Assert.Contains("%09", baggageHeader, StringComparison.Ordinal);  // Tab
        Assert.Contains("%20", baggageHeader, StringComparison.Ordinal);  // Space
        Assert.Contains("%22", baggageHeader, StringComparison.Ordinal);  // Quote

        var extractedContext = this.baggage.Extract(default, carrier, Getter);
        var extractedBaggage = extractedContext.Baggage.GetBaggage();

        Assert.Equal("\t \"';=asdf!@#$%^&*()", extractedBaggage["key"]);
    }
}
