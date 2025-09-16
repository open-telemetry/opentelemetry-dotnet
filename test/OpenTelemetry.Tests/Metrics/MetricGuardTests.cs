// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Tests;
using Xunit;

namespace OpenTelemetry.Tests.Metrics;

public class MetricGuardTests
{
    [Theory]
    [MemberData(nameof(MetricTestData.InvalidInstrumentNames), MemberType = typeof(MetricTestData))]
    [InlineData(null)]
    public void IsValidInstrumentName_ReturnsFalse_ForInvalidNames(string? instrumentName)
    {
        Assert.False(MetricGuard.IsValidInstrumentName(instrumentName));
    }

    [Theory]
    [MemberData(nameof(MetricTestData.ValidInstrumentNames), MemberType = typeof(MetricTestData))]
    public void IsValidInstrumentName_ReturnsTrue_ForValidNames(string instrumentName)
    {
        Assert.True(MetricGuard.IsValidInstrumentName(instrumentName));
    }

    [Theory]
    [MemberData(nameof(MetricTestData.InvalidInstrumentNames), MemberType = typeof(MetricTestData))]
    public void ThrowIfInvalidViewName_ThrowsOnInvalid(string? viewName)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            MetricGuard.ThrowIfInvalidViewName(viewName));

        Assert.Contains($"View name {viewName} is invalid.", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(MetricTestData.ValidInstrumentNames), MemberType = typeof(MetricTestData))]
    [InlineData(null)] // null is valid because the instrument name will be used as the view name.
    public void ThrowIfInvalidViewName_DoesNotThrowForValid(string? viewName)
    {
        var ex = Record.Exception(() =>
            MetricGuard.ThrowIfInvalidViewName(viewName));

        Assert.Null(ex);
    }

    [Theory]
    [MemberData(nameof(MetricTestData.InvalidInstrumentNames), MemberType = typeof(MetricTestData))]
    [InlineData(null)] // null is invalid for custom view names.
    public void ThrowIfInvalidCustomViewName_ThrowsOnInvalid(string? customViewName)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            MetricGuard.ThrowIfInvalidCustomViewName(customViewName));

        Assert.Contains($"Custom view name {customViewName} is invalid.", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(MetricTestData.ValidInstrumentNames), MemberType = typeof(MetricTestData))]
    public void ThrowIfInvalidCustomViewName_DoesNotThrowForValid(string? customViewName)
    {
        var ex = Record.Exception(() =>
            MetricGuard.ThrowIfInvalidCustomViewName(customViewName));

        Assert.Null(ex);
    }
}
