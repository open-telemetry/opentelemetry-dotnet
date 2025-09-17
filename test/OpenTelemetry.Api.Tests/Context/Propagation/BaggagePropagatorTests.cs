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

        Assert.True(actualBaggage.ContainsKey("key%28%293"));
        Assert.Equal("value()!&;:", actualBaggage["key%28%293"]);
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
        Assert.Equal("key 1=value%201,key2=%21x_x%2Cx-x%26x%28x%22%29%3B%3A", carrier[BaggagePropagator.BaggageHeaderName]);
    }
}
