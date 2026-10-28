// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests;

public class AttributeLimitOptionsTests
{
    [Fact]
    public void Constructor_Defaults_UsesDefaultCountLimitAndUnlimitedValueLength()
    {
        var options = new AttributeLimitOptions();

        Assert.Equal(128, options.AttributeCountLimit);
        Assert.Null(options.AttributeValueLengthLimit);
    }

    [Fact]
    public void AttributeCountLimit_NegativeValue_ThrowsWithoutChangingLimit()
    {
        var options = new AttributeLimitOptions { AttributeCountLimit = 5 };

        Assert.Throws<ArgumentOutOfRangeException>("value", () => options.AttributeCountLimit = -1);
        Assert.Equal(5, options.AttributeCountLimit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    public void AttributeCountLimit_NonNegativeValue_AcceptsValue(int value)
    {
        var options = new AttributeLimitOptions { AttributeCountLimit = value };

        Assert.Equal(value, options.AttributeCountLimit);
    }

    [Fact]
    public void AttributeValueLengthLimit_NegativeValue_ThrowsWithoutChangingLimit()
    {
        var options = new AttributeLimitOptions { AttributeValueLengthLimit = 5 };

        Assert.Throws<ArgumentOutOfRangeException>("value", () => options.AttributeValueLengthLimit = -1);
        Assert.Equal(5, options.AttributeValueLengthLimit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    public void AttributeValueLengthLimit_ValidValue_AcceptsValue(int? value)
    {
        var options = new AttributeLimitOptions { AttributeValueLengthLimit = 5 };
        options.AttributeValueLengthLimit = value;

        Assert.Equal(value, options.AttributeValueLengthLimit);
    }
}
