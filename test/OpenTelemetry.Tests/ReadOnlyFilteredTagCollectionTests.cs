// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Tests;

public class ReadOnlyFilteredTagCollectionTests
{
    [Fact]
    public void Verify_Equals_SameReference()
    {
        var tags = new KeyValuePair<string, object?>[] { new("key", "value") };
        var collection1 = new ReadOnlyFilteredTagCollection(excludedKeys: null, tags, count: 1);
        var collection2 = new ReadOnlyFilteredTagCollection(excludedKeys: null, tags, count: 1);

        Assert.True(collection1.Equals(collection2));
        Assert.True(collection1.Equals((object)collection2));
    }

    [Fact]
    public void Verify_Equals_DifferentReference()
    {
        var collection1 = new ReadOnlyFilteredTagCollection(excludedKeys: null, [new("key", "value")], count: 1);
        var collection2 = new ReadOnlyFilteredTagCollection(excludedKeys: null, [new("key", "value")], count: 1);

        Assert.False(collection1.Equals(collection2));
        Assert.False(collection1.Equals((object)collection2));
    }

    [Fact]
    public void Verify_Equals_DifferentCount()
    {
        var tags = new KeyValuePair<string, object?>[] { new("a", 1), new("b", 2) };
        var collection1 = new ReadOnlyFilteredTagCollection(excludedKeys: null, tags, count: 1);
        var collection2 = new ReadOnlyFilteredTagCollection(excludedKeys: null, tags, count: 2);

        Assert.False(collection1.Equals(collection2));
    }

    [Fact]
    public void VerifyOperator_Equals()
    {
        var tags = new KeyValuePair<string, object?>[] { new("key", "value") };
        var collection1 = new ReadOnlyFilteredTagCollection(excludedKeys: null, tags, count: 1);
        var collection2 = new ReadOnlyFilteredTagCollection(excludedKeys: null, tags, count: 1);
        var collection3 = new ReadOnlyFilteredTagCollection(excludedKeys: null, [new("key", "value")], count: 1);

        Assert.True(collection1 == collection2);
        Assert.False(collection1 == collection3);
    }

    [Fact]
    public void VerifyOperator_NotEquals()
    {
        var tags = new KeyValuePair<string, object?>[] { new("key", "value") };
        var collection1 = new ReadOnlyFilteredTagCollection(excludedKeys: null, tags, count: 1);
        var collection2 = new ReadOnlyFilteredTagCollection(excludedKeys: null, tags, count: 1);
        var collection3 = new ReadOnlyFilteredTagCollection(excludedKeys: null, [new("key", "value")], count: 1);

        Assert.False(collection1 != collection2);
        Assert.True(collection1 != collection3);
    }

    [Fact]
    public void Verify_GetHashCode_SameReference()
    {
        var tags = new KeyValuePair<string, object?>[] { new("key", "value") };
        var collection1 = new ReadOnlyFilteredTagCollection(excludedKeys: null, tags, count: 1);
        var collection2 = new ReadOnlyFilteredTagCollection(excludedKeys: null, tags, count: 1);

        Assert.Equal(collection1.GetHashCode(), collection2.GetHashCode());
    }
}
