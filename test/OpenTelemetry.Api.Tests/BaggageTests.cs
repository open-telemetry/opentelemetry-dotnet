// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Xunit;

namespace OpenTelemetry.Tests;

public class BaggageTests
{
    private const string K1 = "Key1";
    private const string K2 = "Key2";
    private const string K3 = "Key3";

    private const string V1 = "Value1";
    private const string V2 = "Value2";
    private const string V3 = "Value3";

    [Fact]
    public void EmptyTest()
    {
        Assert.Empty(Baggage.GetBaggage());
        Assert.Empty(Baggage.Current.GetBaggage());
    }

    [Fact]
    public void SetAndGetTest()
    {
        var list = new List<KeyValuePair<string, string>>(2)
        {
            new(K1, V1),
            new(K2, V2),
        };

        Baggage.SetBaggage(K1, V1);
        var baggage = Baggage.Current.SetBaggage(K2, V2);
        Baggage.Current = baggage;

        Assert.NotEmpty(Baggage.GetBaggage());
        Assert.Equal(list, Baggage.GetBaggage(Baggage.Current));

        Assert.Equal(V1, Baggage.GetBaggage(K1));
#pragma warning disable CA1308 // Normalize strings to uppercase
        Assert.Equal(V1, Baggage.GetBaggage(K1.ToLower(CultureInfo.InvariantCulture)));
#pragma warning restore CA1308 // Normalize strings to uppercase
        Assert.Equal(V1, Baggage.GetBaggage(K1.ToUpper(CultureInfo.InvariantCulture)));
        Assert.Null(Baggage.GetBaggage("NO_KEY"));
        Assert.Equal(V2, Baggage.Current.GetBaggage(K2));

        Assert.Throws<ArgumentException>(() => Baggage.GetBaggage(null!));
    }

    [Fact]
    public void SetExistingKeyTest()
    {
        var list = new List<KeyValuePair<string, string>>(2)
        {
            new(K1, V1),
        };

        Baggage.Current.SetBaggage(new KeyValuePair<string, string?>(K1, V1));
        var baggage = Baggage.SetBaggage(K1, V1);
        Baggage.SetBaggage(new Dictionary<string, string?> { [K1] = V1 }, baggage);

        Assert.Equal(list, Baggage.GetBaggage());
    }

    [Fact]
    public void SetNullValueTest()
    {
        var baggage = Baggage.Current;
        baggage = Baggage.SetBaggage(K1, V1, baggage);

        Assert.Equal(1, Baggage.Current.Count);
        Assert.Equal(1, baggage.Count);

        Baggage.Current.SetBaggage(K2, null);

        Assert.Equal(1, Baggage.Current.Count);

        Assert.Empty(Baggage.SetBaggage(K1, null).GetBaggage());

        Baggage.SetBaggage(K1, V1);
        Baggage.SetBaggage(new Dictionary<string, string?>
        {
            [K1] = null,
            [K2] = V2,
        });
        Assert.Equal(1, Baggage.Current.Count);
        Assert.Contains(Baggage.GetBaggage(), kvp => kvp.Key == K2);
    }

    [Fact]
    public void RemoveTest()
    {
        var empty = Baggage.Current;
        var empty2 = Baggage.RemoveBaggage(K1);
        Assert.True(empty == empty2);

        var baggage = Baggage.SetBaggage(new Dictionary<string, string?>
        {
            [K1] = V1,
            [K2] = V2,
            [K3] = V3,
        });

        var baggage2 = Baggage.RemoveBaggage(K1, baggage);

        Assert.Equal(3, baggage.Count);
        Assert.Equal(2, baggage2.Count);

        Assert.DoesNotContain(new KeyValuePair<string, string>(K1, V1), baggage2.GetBaggage());
    }

    [Fact]
    public void ClearTest()
    {
        var baggage = Baggage.SetBaggage(new Dictionary<string, string?>
        {
            [K1] = V1,
            [K2] = V2,
            [K3] = V3,
        });

        Assert.Equal(3, baggage.Count);

        Baggage.ClearBaggage();

        Assert.Equal(0, Baggage.Current.Count);
    }

    [Fact]
    public void ContextFlowTest()
    {
        var baggage = Baggage.SetBaggage(K1, V1);
        var baggage2 = Baggage.Current.SetBaggage(K2, V2);
        Baggage.Current = baggage2;
        var baggage3 = Baggage.SetBaggage(K3, V3);

        Assert.Equal(1, baggage.Count);
        Assert.Equal(2, baggage2.Count);
        Assert.Equal(3, baggage3.Count);

        Baggage.Current = baggage;

        var baggage4 = Baggage.SetBaggage(K3, V3);

        Assert.Equal(2, baggage4.Count);
        Assert.DoesNotContain(new KeyValuePair<string, string>(K2, V2), baggage4.GetBaggage());
    }

    [Fact]
    public void EnumeratorTest()
    {
        var list = new List<KeyValuePair<string, string>>(2)
        {
            new(K1, V1),
            new(K2, V2),
        };

        var baggage = Baggage.SetBaggage(K1, V1);
        baggage = Baggage.SetBaggage(K2, V2, baggage);

        var enumerator = Baggage.GetEnumerator(baggage);

        Assert.True(enumerator.MoveNext());
        var tag1 = enumerator.Current;
        Assert.True(enumerator.MoveNext());
        var tag2 = enumerator.Current;
        Assert.False(enumerator.MoveNext());

        Assert.Equal(list, [tag1, tag2]);

        Baggage.ClearBaggage();

        enumerator = Baggage.GetEnumerator();

        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void EqualsTest()
    {
        var bc1 = new Baggage(new Dictionary<string, string> { [K1] = V1, [K2] = V2 });
        var bc2 = new Baggage(new Dictionary<string, string> { [K1] = V1, [K2] = V2 });
        var bc3 = new Baggage(new Dictionary<string, string> { [K2] = V2, [K1] = V1 });
        var bc4 = new Baggage(new Dictionary<string, string> { [K1] = V1, [K2] = V1 });
        var bc5 = new Baggage(new Dictionary<string, string> { [K1] = V2, [K2] = V1 });

        Assert.True(bc1.Equals(bc2));

        Assert.False(bc1.Equals(bc3));
        Assert.False(bc1.Equals(bc4));
        Assert.False(bc2.Equals(bc4));
        Assert.False(bc3.Equals(bc4));
        Assert.False(bc5.Equals(bc4));
        Assert.False(bc4.Equals(bc5));
    }

    [Fact]
    public void CreateBaggageTest()
    {
        var baggage = Baggage.Create(null);

        Assert.Equal(default, baggage);

        baggage = Baggage.Create(new Dictionary<string, string>
        {
            [K1] = V1,
            ["key2"] = "value2",
            ["KEY2"] = "VALUE2",
            ["KEY3"] = "VALUE3",
            ["Key3"] = null!, // Note: This causes Key3 to be removed
        });

        Assert.Equal(2, baggage.Count);
        Assert.Contains(baggage.GetBaggage(), kvp => kvp.Key == K1);
        Assert.Equal("VALUE2", Baggage.GetBaggage("key2", baggage));
    }

    [Fact]
    public void EqualityTests()
    {
        var emptyBaggage = Baggage.Create(null);

        var baggage = Baggage.SetBaggage(K1, V1);

        Assert.NotEqual(emptyBaggage, baggage);

        Assert.True(emptyBaggage != baggage);

        baggage = Baggage.ClearBaggage(baggage);

        Assert.Equal(emptyBaggage, baggage);

        baggage = Baggage.SetBaggage(K1, V1);

        var baggage2 = Baggage.SetBaggage(null!);

        Assert.Equal(baggage, baggage2);

        Assert.False(baggage.Equals(this));
        Assert.True(baggage.Equals((object)baggage2));
    }

    [Fact]
    public void GetHashCodeTests()
    {
        var baggage = Baggage.Current;
        var emptyBaggage = Baggage.Create(null);

        Assert.Equal(emptyBaggage.GetHashCode(), baggage.GetHashCode());

        baggage = Baggage.SetBaggage(K1, V1, baggage);

        Assert.NotEqual(emptyBaggage.GetHashCode(), baggage.GetHashCode());

        var expectedBaggage = Baggage.Create(new Dictionary<string, string> { [K1] = V1 });

        Assert.Equal(expectedBaggage.GetHashCode(), baggage.GetHashCode());
    }

    [Fact]
    public async Task AsyncLocalTests()
    {
        Baggage.SetBaggage("key1", "value1");

        await InnerTask();

        Baggage.SetBaggage("key4", "value4");

        Assert.Equal(4, Baggage.Current.Count);
        Assert.Equal("value1", Baggage.GetBaggage("key1"));
        Assert.Equal("value2", Baggage.GetBaggage("key2"));
        Assert.Equal("value3", Baggage.GetBaggage("key3"));
        Assert.Equal("value4", Baggage.GetBaggage("key4"));

        static async Task InnerTask()
        {
            Baggage.SetBaggage("key2", "value2");

            await Task.Yield();

            Baggage.SetBaggage("key3", "value3");

            // key2 & key3 changes don't flow backward automatically
        }
    }

    [Fact]
    public void ThreadSafetyTest()
    {
        Baggage.SetBaggage("rootKey", "rootValue"); // Note: Required to establish a root ExecutionContext containing the BaggageHolder we use as a lock

        Parallel.For(0, 100, (i) =>
        {
            Baggage.SetBaggage($"key{i}", $"value{i}");
        });

        Assert.Equal(101, Baggage.Current.Count);
    }
}
