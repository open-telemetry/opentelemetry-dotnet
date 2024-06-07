// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Context.Propagation.Tests;

public class BaggagePropagatorTest
{
    private static readonly Func<IDictionary<string, string>, string, IEnumerable<string>> Getter =
        (d, k) =>
        {
            d.TryGetValue(k, out var v);
            return new string[] { v };
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
        var propagationContext = this.baggage.Extract<string>(default, null, null);
        Assert.Equal(default, propagationContext);
    }

    [Fact]
    public void ValidateDefaultGetterExtraction()
    {
        var carrier = new Dictionary<string, string>();
        var propagationContext = this.baggage.Extract(default, carrier, null);
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

    [Theory]
    [InlineData("key%201=value%201", "key%201", "value 1")]
    [InlineData("key=val+ue", "key", "val+ue")]
    [InlineData("key=val%2Bue", "key", "val+ue")]
    [InlineData("key=val%20ue", "key", "val ue")]
    [InlineData("key=value=1", "key", "value=1")]
    [InlineData("key=%20%21%22%23%24%25%26%27%28%29%2A%2B%2C-.%2F0123456789%3A%3B%3C%3D%3E%3F%40ABCDEFGHIJKLMNOPQRSTUVWXYZ%5B%5C%5D%5E_%60abcdefghijklmnopqrstuvwxyz%7B%7C%7D~", "key", " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~")]
    public void ValidateSpecialCharsBaggageExtraction(string propagatedBaggage, string key, string expectedDecodedValue)
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
    public void ValidateBaggageMultipleItemsExtraction()
    {
        var carrier = new List<KeyValuePair<string, string>>
        {
            new(BaggagePropagator.BaggageHeaderName, "  key1  =val1,key2=  val2  ,key3="),
        };

        var propagationContext = this.baggage.Extract(default, carrier, GetterList);

        Assert.False(propagationContext == default);
        Assert.True(propagationContext.ActivityContext == default);

        var actualBaggage = propagationContext.Baggage.GetBaggage();

        Assert.Equal(3, actualBaggage.Count);

        Assert.True(actualBaggage.ContainsKey("key1"));
        Assert.Equal("val1", actualBaggage["key1"]);

        Assert.True(actualBaggage.ContainsKey("key2"));
        Assert.Equal("val2", actualBaggage["key2"]);

        Assert.True(actualBaggage.ContainsKey("key3"));
        Assert.Equal(string.Empty, actualBaggage["key3"]);
    }

    [Theory]
    [InlineData("key(=value")]
    [InlineData(@"key=\\")]
    [InlineData("key=val%")]
    [InlineData("key=%gg")]
    [InlineData("key=val%2")]
    [InlineData("key=val ue")]
    [InlineData("key=val%IJK")]
    public void ValidateInvalidPairsBaggageExtraction(string propagatedBaggage)
    {
        var carrier = new List<KeyValuePair<string, string>>
        {
            new(BaggagePropagator.BaggageHeaderName, propagatedBaggage),
        };

        var propagationContext = this.baggage.Extract(default, carrier, GetterList);

        Assert.True(propagationContext.ActivityContext == default);

        var actualBaggage = propagationContext.Baggage.GetBaggage();

        Assert.Empty(actualBaggage);
    }

    [Fact]
    public void ValidateAllValuesAreRejectedIfAnyIsInvalidDuringExtraction()
    {
        var carrier = new List<KeyValuePair<string, string>>
        {
            new(BaggagePropagator.BaggageHeaderName, "key1=val1,key2=%2,key3=val3"),
        };

        var propagationContext = this.baggage.Extract(default, carrier, GetterList);

        Assert.True(propagationContext.ActivityContext == default);

        var actualBaggage = propagationContext.Baggage.GetBaggage();

        Assert.Empty(actualBaggage);
    }

    [Fact]
    public void ValidateEmptyBaggageInjection()
    {
        var carrier = new Dictionary<string, string>();
        this.baggage.Inject(default, carrier, Setter);

        Assert.Empty(carrier);
    }

    [Fact]
    public void ValidateMultipleItemsBaggageInjection()
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
    public void ValidateOnlyInvalidItemsAreRejectedDuringInjection()
    {
        var carrier = new Dictionary<string, string>();
        var propagationContext = new PropagationContext(
            default,
            new Baggage(new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key 2", "value2" },
            }));

        this.baggage.Inject(propagationContext, carrier, Setter);

        Assert.Single(carrier);
        Assert.Equal("key1=value1", carrier[BaggagePropagator.BaggageHeaderName]);
    }

    [Theory]
    [InlineData("key 1", "value 1")]
    [InlineData("", "value3")]
    public void ValidateInvalidPairsBaggageInjection(string key, string value)
    {
        var carrier = new Dictionary<string, string>();
        var propagationContext = new PropagationContext(
            default,
            new Baggage(new Dictionary<string, string>
            {
                { key, value },
            }));
        this.baggage.Inject(propagationContext, carrier, Setter);
        Assert.Empty(carrier);
    }

    [Fact]
    public void ValidateSpecialCharsBaggageInjection()
    {
        var carrier = new Dictionary<string, string>();
        var propagationContext = new PropagationContext(
            default,
            new Baggage(new Dictionary<string, string>
            {
                { "key2", "!x_x,x-x&x(x\");:" },
                { "key3", " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" },
            }));

        this.baggage.Inject(propagationContext, carrier, Setter);

        Assert.Single(carrier);
        Assert.Equal("key2=%21x_x%2Cx-x%26x%28x%22%29%3B%3A,key3=%20%21%22%23%24%25%26%27%28%29%2A%2B%2C-.%2F0123456789%3A%3B%3C%3D%3E%3F%40ABCDEFGHIJKLMNOPQRSTUVWXYZ%5B%5C%5D%5E_%60abcdefghijklmnopqrstuvwxyz%7B%7C%7D~", carrier[BaggagePropagator.BaggageHeaderName]);
    }
}
