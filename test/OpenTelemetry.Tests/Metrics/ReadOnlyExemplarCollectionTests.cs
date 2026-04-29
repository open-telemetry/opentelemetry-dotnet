// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class ReadOnlyExemplarCollectionTests
{
    [Fact]
    public void Verify_Equals_SameReference()
    {
        var exemplars = new Exemplar[0];
        var collection1 = new ReadOnlyExemplarCollection(exemplars);
        var collection2 = new ReadOnlyExemplarCollection(exemplars);

        Assert.True(collection1.Equals(collection2));
        Assert.True(collection1.Equals((object)collection2));
    }

    [Fact]
    public void Verify_Equals_DifferentReference()
    {
        var collection1 = new ReadOnlyExemplarCollection(new Exemplar[0]);
        var collection2 = new ReadOnlyExemplarCollection(new Exemplar[0]);

        Assert.False(collection1.Equals(collection2));
        Assert.False(collection1.Equals((object)collection2));
    }

    [Fact]
    public void VerifyOperator_Equals()
    {
        var exemplars = new Exemplar[0];
        var collection1 = new ReadOnlyExemplarCollection(exemplars);
        var collection2 = new ReadOnlyExemplarCollection(exemplars);
        var collection3 = new ReadOnlyExemplarCollection(new Exemplar[0]);

        Assert.True(collection1 == collection2);
        Assert.False(collection1 == collection3);
    }

    [Fact]
    public void VerifyOperator_NotEquals()
    {
        var exemplars = new Exemplar[0];
        var collection1 = new ReadOnlyExemplarCollection(exemplars);
        var collection2 = new ReadOnlyExemplarCollection(exemplars);
        var collection3 = new ReadOnlyExemplarCollection(new Exemplar[0]);

        Assert.False(collection1 != collection2);
        Assert.True(collection1 != collection3);
    }

    [Fact]
    public void Verify_GetHashCode_SameReference()
    {
        var exemplars = new Exemplar[0];
        var collection1 = new ReadOnlyExemplarCollection(exemplars);
        var collection2 = new ReadOnlyExemplarCollection(exemplars);

        Assert.Equal(collection1.GetHashCode(), collection2.GetHashCode());
    }

    [Fact]
    public void Verify_Empty_Equality()
    {
        Assert.True(ReadOnlyExemplarCollection.Empty == ReadOnlyExemplarCollection.Empty);
        Assert.Equal(ReadOnlyExemplarCollection.Empty.GetHashCode(), ReadOnlyExemplarCollection.Empty.GetHashCode());
    }
}
