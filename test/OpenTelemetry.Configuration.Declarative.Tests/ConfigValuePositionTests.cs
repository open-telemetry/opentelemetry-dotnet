// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class ConfigValuePositionTests
{
    [Fact]
    public void EqualityOperator_SameLineAndColumn_ReturnsTrue()
    {
        var p1 = new ConfigValuePosition(1, 2);
        var p2 = new ConfigValuePosition(1, 2);

        Assert.True(p1 == p2, "Equal positions should be equal via == operator.");
    }

    [Fact]
    public void EqualityOperator_DifferentLine_ReturnsFalse()
    {
        var p1 = new ConfigValuePosition(1, 2);
        var p2 = new ConfigValuePosition(3, 2);

        Assert.False(p1 == p2);
    }

    [Fact]
    public void EqualityOperator_DifferentColumn_ReturnsFalse()
    {
        var p1 = new ConfigValuePosition(1, 2);
        var p2 = new ConfigValuePosition(1, 9);

        Assert.False(p1 == p2);
    }

    [Fact]
    public void EqualityOperator_UnknownPositions_ReturnsTrue()
        => Assert.True(ConfigValuePosition.Unknown == ConfigValuePosition.Unknown, "Unknown positions should be equal to themselves.");

    [Fact]
    public void InequalityOperator_SameLineAndColumn_ReturnsFalse()
    {
        var p1 = new ConfigValuePosition(5, 10);
        var p2 = new ConfigValuePosition(5, 10);

        Assert.False(p1 != p2);
    }

    [Fact]
    public void InequalityOperator_DifferentLine_ReturnsTrue()
    {
        var p1 = new ConfigValuePosition(1, 2);
        var p2 = new ConfigValuePosition(2, 2);

        Assert.True(p1 != p2, "Different-line positions should be unequal via != operator.");
    }

    [Fact]
    public void InequalityOperator_DifferentColumn_ReturnsTrue()
    {
        var p1 = new ConfigValuePosition(1, 2);
        var p2 = new ConfigValuePosition(1, 3);

        Assert.True(p1 != p2, "Different-column positions should be unequal via != operator.");
    }

    [Fact]
    public void Equals_ObjectOverload_BoxedEqualPosition_ReturnsTrue()
    {
        var p1 = new ConfigValuePosition(4, 8);
        object p2 = new ConfigValuePosition(4, 8);

        Assert.True(p1.Equals(p2), "Boxed equal position should return true from Equals.");
    }

    [Fact]
    public void Equals_ObjectOverload_BoxedDifferentPosition_ReturnsFalse()
    {
        var p1 = new ConfigValuePosition(1, 2);
        object p2 = new ConfigValuePosition(3, 4);

        Assert.False(p1.Equals(p2));
    }

    [Fact]
    public void Equals_ObjectOverload_Null_ReturnsFalse()
    {
        var p1 = new ConfigValuePosition(1, 2);

        Assert.False(p1.Equals(null));
    }

    [Fact]
    public void Equals_ObjectOverload_DifferentType_ReturnsFalse()
    {
        var p1 = new ConfigValuePosition(1, 2);

        Assert.False(p1.Equals("not a ConfigValuePosition"));
    }

    [Fact]
    public void GetHashCode_EqualPositions_ProduceSameHash()
    {
        var p1 = new ConfigValuePosition(7, 13);
        var p2 = new ConfigValuePosition(7, 13);

        Assert.Equal(p1.GetHashCode(), p2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_UnknownPosition_DoesNotThrow()
        => _ = ConfigValuePosition.Unknown.GetHashCode();

    [Fact]
    public void GetHashCode_DifferentPositions_LikelyDifferentHash()
    {
        var p1 = new ConfigValuePosition(1, 2);
        var p2 = new ConfigValuePosition(2, 1);

        // Not strictly required, but two distinct positions with swapped line/column
        // should produce different hashes to avoid trivial collisions.
        Assert.NotEqual(p1.GetHashCode(), p2.GetHashCode());
    }
}
