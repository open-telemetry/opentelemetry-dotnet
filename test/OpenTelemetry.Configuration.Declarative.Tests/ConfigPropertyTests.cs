// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable OTEL1006

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class ConfigPropertyTests
{
    [Fact]
    public void IsAbsent_WhenAbsent_ReturnsTrue() =>
        Assert.True(ConfigProperty<string>.Absent.IsAbsent);

    [Fact]
    public void IsAbsent_WhenNull_ReturnsFalse() =>
        Assert.False(ConfigProperty<string>.Null.IsAbsent);

    [Fact]
    public void IsAbsent_WhenPresent_ReturnsFalse() =>
        Assert.False(ConfigProperty<string>.Create("x").IsAbsent);

    [Fact]
    public void IsNull_WhenNull_ReturnsTrue() =>
        Assert.True(ConfigProperty<string>.Null.IsNull);

    [Fact]
    public void IsNull_WhenAbsent_ReturnsFalse() =>
        Assert.False(ConfigProperty<string>.Absent.IsNull);

    [Fact]
    public void IsNull_WhenPresent_ReturnsFalse() =>
        Assert.False(ConfigProperty<string>.Create("x").IsNull);

    [Fact]
    public void Value_WhenPresent_ReturnsValue() =>
        Assert.Equal("hello", ConfigProperty<string>.Create("hello").Value);

    [Fact]
    public void Value_WhenAbsent_ThrowsInvalidOperationException() =>
        Assert.Throws<InvalidOperationException>(() => ConfigProperty<string>.Absent.Value);

    [Fact]
    public void Value_WhenNull_ThrowsInvalidOperationException() =>
        Assert.Throws<InvalidOperationException>(() => ConfigProperty<string>.Null.Value);

    [Fact]
    public void TryGetValue_WhenPresent_ReturnsTrueAndValue()
    {
        var result = ConfigProperty<string>.Create("hello");

        Assert.Equal(ConfigPropertyState.Present, result.State);
        Assert.True(result.TryGetValue(out var value));
        Assert.Equal("hello", value);
    }

    [Fact]
    public void TryGetValue_WhenAbsent_ReturnsFalse()
    {
        var result = ConfigProperty<string>.Absent;

        Assert.Equal(ConfigPropertyState.Absent, result.State);
        Assert.False(result.TryGetValue(out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryGetValue_WhenNull_ReturnsFalse()
    {
        var result = ConfigProperty<string>.Null;

        Assert.Equal(ConfigPropertyState.Null, result.State);
        Assert.False(result.TryGetValue(out var value));
        Assert.Null(value);
    }
}
