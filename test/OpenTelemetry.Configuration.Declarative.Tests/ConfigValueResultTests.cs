// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class ConfigValueResultTests
{
    [Fact]
    public void EqualityOperator_SameOutcomeValueAndPosition_ReturnsTrue()
    {
        var r1 = Build("k", ConfigValue.String("v").WithPosition(new(1, 2))).GetString("k");
        var r2 = Build("k", ConfigValue.String("v").WithPosition(new(1, 2))).GetString("k");

        Assert.True(r1 == r2, "Results with same outcome, value, and position should be equal via == operator.");
    }

    [Fact]
    public void EqualityOperator_DifferentValue_ReturnsFalse()
    {
        var r1 = Build("k", ConfigValue.String("a")).GetString("k");
        var r2 = Build("k", ConfigValue.String("b")).GetString("k");

        Assert.False(r1 == r2);
    }

    [Fact]
    public void EqualityOperator_DifferentOutcome_ReturnsFalse()
    {
        var r1 = Build("k", ConfigValue.String("v")).GetString("k");
        var r2 = EmptyProperties().GetString("k");

        Assert.False(r1 == r2);
    }

    [Fact]
    public void InequalityOperator_SameOutcomeValueAndPosition_ReturnsFalse()
    {
        var r1 = Build("k", ConfigValue.String("v").WithPosition(new(3, 7))).GetString("k");
        var r2 = Build("k", ConfigValue.String("v").WithPosition(new(3, 7))).GetString("k");

        Assert.False(r1 != r2);
    }

    [Fact]
    public void InequalityOperator_DifferentValue_ReturnsTrue()
    {
        var r1 = Build("k", ConfigValue.String("a")).GetString("k");
        var r2 = Build("k", ConfigValue.String("b")).GetString("k");

        Assert.True(r1 != r2);
    }

    [Fact]
    public void Equals_ObjectOverload_BoxedEqualResult_ReturnsTrue()
    {
        var r1 = Build("k", ConfigValue.String("v").WithPosition(new(1, 1))).GetString("k");
        object r2 = Build("k", ConfigValue.String("v").WithPosition(new(1, 1))).GetString("k");

        Assert.True(r1.Equals(r2), "Boxed equal result should return true from Equals.");
    }

    [Fact]
    public void Equals_ObjectOverload_BoxedDifferentResult_ReturnsFalse()
    {
        var r1 = Build("k", ConfigValue.String("a")).GetString("k");
        object r2 = Build("k", ConfigValue.String("b")).GetString("k");

        Assert.False(r1.Equals(r2));
    }

    [Fact]
    public void Equals_ObjectOverload_Null_ReturnsFalse()
    {
        var r1 = Build("k", ConfigValue.String("v")).GetString("k");

        Assert.False(r1.Equals(null));
    }

    [Fact]
    public void Equals_ObjectOverload_DifferentType_ReturnsFalse()
    {
        var r1 = Build("k", ConfigValue.String("v")).GetString("k");

        Assert.False(r1.Equals("not a ConfigValueResult"));
    }

    [Fact]
    public void GetHashCode_EqualResults_ProduceSameHash()
    {
        var r1 = Build("k", ConfigValue.String("v").WithPosition(new(5, 3))).GetString("k");
        var r2 = Build("k", ConfigValue.String("v").WithPosition(new(5, 3))).GetString("k");

        Assert.Equal(r1.GetHashCode(), r2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_AbsentResult_DoesNotThrow()
        => _ = EmptyProperties().GetString("missing").GetHashCode();

    [Fact]
    public void Equals_TypedOverload_SameOutcomeValueAndPosition_ReturnsTrue()
    {
        var r1 = Build("k", ConfigValue.String("v").WithPosition(new(2, 4))).GetString("k");
        var r2 = Build("k", ConfigValue.String("v").WithPosition(new(2, 4))).GetString("k");

        Assert.True(r1.Equals(r2), "Results with same outcome, value, and position should be equal via typed Equals.");
    }

    [Fact]
    public void Equals_TypedOverload_DifferentOutcome_ReturnsFalse()
    {
        var r1 = Build("k", ConfigValue.String("v")).GetString("k");
        var r2 = EmptyProperties().GetString("k");

        Assert.False(r1.Equals(r2));
    }

    [Fact]
    public void Equals_TypedOverload_DifferentValue_ReturnsFalse()
    {
        var r1 = Build("k", ConfigValue.String("a")).GetString("k");
        var r2 = Build("k", ConfigValue.String("b")).GetString("k");

        Assert.False(r1.Equals(r2));
    }

    [Fact]
    public void Equals_TypedOverload_DifferentPosition_ReturnsFalse()
    {
        var r1 = Build("k", ConfigValue.String("v").WithPosition(new(1, 1))).GetString("k");
        var r2 = Build("k", ConfigValue.String("v").WithPosition(new(2, 2))).GetString("k");

        Assert.False(r1.Equals(r2));
    }

    [Fact]
    public void Equals_TypedOverload_NullValue_BothAbsent_ReturnsTrue()
    {
        var r1 = EmptyProperties().GetString("missing");
        var r2 = EmptyProperties().GetString("missing");

        Assert.True(r1.Equals(r2), "Two absent results should be equal via typed Equals.");
    }

    private static ConfigProperties EmptyProperties() => ConfigProperties.Empty;

    private static ConfigProperties Build(string key, ConfigValue value)
        => new ConfigPropertiesBuilder().Add(key, value).Build();
}
