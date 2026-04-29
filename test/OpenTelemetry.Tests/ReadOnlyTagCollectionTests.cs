// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Tests;

public class ReadOnlyTagCollectionTests
{
    [Fact]
    public void Verify_Equals_SameReference()
    {
        var tags = new KeyValuePair<string, object?>[] { new("key", "value") };
        var collection1 = new ReadOnlyTagCollection(tags);
        var collection2 = new ReadOnlyTagCollection(tags);

        Assert.True(collection1.Equals(collection2));
        Assert.True(collection1.Equals((object)collection2));
    }

    [Fact]
    public void Verify_Equals_DifferentReference()
    {
        var collection1 = new ReadOnlyTagCollection([new("key", "value")]);
        var collection2 = new ReadOnlyTagCollection([new("key", "value")]);

        Assert.False(collection1.Equals(collection2));
        Assert.False(collection1.Equals((object)collection2));
    }

    [Fact]
    public void VerifyOperator_Equals()
    {
        var tags = new KeyValuePair<string, object?>[] { new("key", "value") };
        var collection1 = new ReadOnlyTagCollection(tags);
        var collection2 = new ReadOnlyTagCollection(tags);
        var collection3 = new ReadOnlyTagCollection([new("key", "value")]);

        Assert.True(collection1 == collection2);
        Assert.False(collection1 == collection3);
    }

    [Fact]
    public void VerifyOperator_NotEquals()
    {
        var tags = new KeyValuePair<string, object?>[] { new("key", "value") };
        var collection1 = new ReadOnlyTagCollection(tags);
        var collection2 = new ReadOnlyTagCollection(tags);
        var collection3 = new ReadOnlyTagCollection([new("key", "value")]);

        Assert.False(collection1 != collection2);
        Assert.True(collection1 != collection3);
    }

    [Fact]
    public void Verify_GetHashCode_SameReference()
    {
        var tags = new KeyValuePair<string, object?>[] { new("key", "value") };
        var collection1 = new ReadOnlyTagCollection(tags);
        var collection2 = new ReadOnlyTagCollection(tags);

        Assert.Equal(collection1.GetHashCode(), collection2.GetHashCode());
    }
}
