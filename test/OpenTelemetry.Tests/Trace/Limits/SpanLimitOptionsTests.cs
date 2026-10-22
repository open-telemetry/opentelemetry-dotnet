// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Trace.Tests;

public class SpanLimitOptionsTests
{
    [Fact]
    public void Constructor_Defaults_UsesDefaultCountLimitsAndUnlimitedValueLength()
    {
        var options = new SpanLimitOptions();

        Assert.Equal(128, options.AttributeCountLimit);
        Assert.Null(options.AttributeValueLengthLimit);
        Assert.Equal(128, options.EventCountLimit);
        Assert.Equal(128, options.LinkCountLimit);
        Assert.Equal(128, options.AttributePerEventCountLimit);
        Assert.Equal(128, options.AttributePerLinkCountLimit);
    }

    [Fact]
    public void CountLimits_NegativeValue_ThrowArgumentOutOfRangeException()
    {
        var options = new SpanLimitOptions();

        Assert.Throws<ArgumentOutOfRangeException>("value", () => options.AttributeCountLimit = -1);
        Assert.Throws<ArgumentOutOfRangeException>("value", () => options.EventCountLimit = -1);
        Assert.Throws<ArgumentOutOfRangeException>("value", () => options.LinkCountLimit = -1);
        Assert.Throws<ArgumentOutOfRangeException>("value", () => options.AttributePerEventCountLimit = -1);
        Assert.Throws<ArgumentOutOfRangeException>("value", () => options.AttributePerLinkCountLimit = -1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    public void CountLimits_NonNegativeValue_AcceptValue(int value)
    {
        var options = new SpanLimitOptions
        {
            AttributeCountLimit = value,
            EventCountLimit = value,
            LinkCountLimit = value,
            AttributePerEventCountLimit = value,
            AttributePerLinkCountLimit = value,
        };

        Assert.Equal(value, options.AttributeCountLimit);
        Assert.Equal(value, options.EventCountLimit);
        Assert.Equal(value, options.LinkCountLimit);
        Assert.Equal(value, options.AttributePerEventCountLimit);
        Assert.Equal(value, options.AttributePerLinkCountLimit);
    }

    [Fact]
    public void AttributeValueLengthLimit_NegativeValue_ThrowsWithoutChangingLimit()
    {
        var options = new SpanLimitOptions { AttributeValueLengthLimit = 5 };

        Assert.Throws<ArgumentOutOfRangeException>("value", () => options.AttributeValueLengthLimit = -1);
        Assert.Equal(5, options.AttributeValueLengthLimit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    public void AttributeValueLengthLimit_ValidValue_AcceptsValue(int? value)
    {
        var options = new SpanLimitOptions { AttributeValueLengthLimit = 5 };
        options.AttributeValueLengthLimit = value;

        Assert.Equal(value, options.AttributeValueLengthLimit);
    }
}
