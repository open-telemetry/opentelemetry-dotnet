// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class ModelPropertyTests
{
    [Fact]
    public void IsAbsent_WhenAbsent_ReturnsTrue() =>
        Assert.True(ModelProperty<string>.Absent.IsAbsent);

    [Fact]
    public void IsAbsent_WhenNull_ReturnsFalse() =>
        Assert.False(ModelProperty<string>.Null.IsAbsent);

    [Fact]
    public void IsAbsent_WhenPresent_ReturnsFalse() =>
        Assert.False(ModelProperty<string>.Create("x").IsAbsent);

    [Fact]
    public void IsNull_WhenNull_ReturnsTrue() =>
        Assert.True(ModelProperty<string>.Null.IsNull);

    [Fact]
    public void IsNull_WhenAbsent_ReturnsFalse() =>
        Assert.False(ModelProperty<string>.Absent.IsNull);

    [Fact]
    public void IsNull_WhenPresent_ReturnsFalse() =>
        Assert.False(ModelProperty<string>.Create("x").IsNull);

    [Fact]
    public void Value_WhenPresent_ReturnsValue() =>
        Assert.Equal("hello", ModelProperty<string>.Create("hello").Value);

    [Fact]
    public void Value_WhenAbsent_ThrowsInvalidOperationException() =>
        Assert.Throws<InvalidOperationException>(() => ModelProperty<string>.Absent.Value);

    [Fact]
    public void Value_WhenNull_ThrowsInvalidOperationException() =>
        Assert.Throws<InvalidOperationException>(() => ModelProperty<string>.Null.Value);

    [Fact]
    public void TryGetValue_WhenPresent_ReturnsTrueAndValue()
    {
        var result = ModelProperty<string>.Create("hello");

        Assert.Equal(ModelPropertyState.Present, result.State);
        Assert.True(result.TryGetValue(out var value));
        Assert.Equal("hello", value);
    }

    [Fact]
    public void TryGetValue_WhenAbsent_ReturnsFalse()
    {
        var result = ModelProperty<string>.Absent;

        Assert.Equal(ModelPropertyState.Absent, result.State);
        Assert.False(result.TryGetValue(out var value));
        Assert.Null(value);
    }

    [Fact]
    public void TryGetValue_WhenNull_ReturnsFalse()
    {
        var result = ModelProperty<string>.Null;

        Assert.Equal(ModelPropertyState.Null, result.State);
        Assert.False(result.TryGetValue(out var value));
        Assert.Null(value);
    }
}
