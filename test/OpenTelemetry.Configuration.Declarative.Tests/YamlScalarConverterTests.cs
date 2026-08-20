// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class YamlScalarConverterTests
{
    [Theory]
    [InlineData("")]
    [InlineData("~")]
    [InlineData("null")]
    [InlineData("Null")]
    [InlineData("NULL")]
    public void Convert_Null_ReturnsNull(string value)
    {
        var result = YamlScalarConverter.Convert(new(value, YamlScalarKind.Null));

        Assert.Equal(ConfigValueKind.Null, result.Kind);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("FALSE", false)]
    public void Convert_Boolean_ReturnsExpectedValue(string value, bool expected)
    {
        var result = YamlScalarConverter.Convert(new(value, YamlScalarKind.Boolean));

        Assert.Equal(ConfigValueKind.Boolean, result.Kind);
        Assert.Equal(expected, result.AsBoolean());
    }

    [Fact]
    public void Convert_Boolean_InvalidValue_Throws() =>
        Assert.Throws<InvalidOperationException>(() =>
            YamlScalarConverter.Convert(new("maybe", YamlScalarKind.Boolean)));

    [Theory]
    [InlineData("hello")]
    [InlineData("-0o7")]
    [InlineData("+0x3A")]
    [InlineData("0O17")]
    public void Convert_String_ReturnsValueVerbatim(string value)
    {
        var result = YamlScalarConverter.Convert(new(value, YamlScalarKind.String));

        Assert.Equal(ConfigValueKind.String, result.Kind);
        Assert.Equal(value, result.AsString());
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("+42", 42L)]
    [InlineData("-19", -19L)]
    [InlineData("007", 7L)]
    [InlineData("0000000000000000000000042", 42L)]
    [InlineData("9223372036854775807", long.MaxValue)]
    [InlineData("-9223372036854775808", long.MinValue)]
    [InlineData("0x3A", 58L)]
    [InlineData("0xdeadbeef", 3735928559L)]
    [InlineData("0x7FFFFFFFFFFFFFFF", long.MaxValue)]
    [InlineData("0x000000000000000000001F", 31L)]
    [InlineData("0o7", 7L)]
    [InlineData("0o17", 15L)]
    [InlineData("0o777777777777777777777", long.MaxValue)]
    public void Convert_Integer_RepresentableRange_ReturnsExpectedLong(string value, long expected)
    {
        var result = YamlScalarConverter.Convert(new(value, YamlScalarKind.Integer));

        Assert.Equal(ConfigValueKind.Integer, result.Kind);
        Assert.False(result.IsUnrepresentable);
        Assert.Equal(expected, result.AsLong());
    }

    [Theory]
    [InlineData("9223372036854775808")]
    [InlineData("-9223372036854775809")]
    [InlineData("123456789012345678901234567890")]
    [InlineData("0x8000000000000000")]
    [InlineData("0xFFFFFFFFFFFFFFFFFF")]
    [InlineData("0o1000000000000000000000")]
    public void Convert_Integer_OutOfRange_ReturnsUnrepresentable(string value)
    {
        var result = YamlScalarConverter.Convert(new(value, YamlScalarKind.Integer));

        Assert.Equal(ConfigValueKind.Integer, result.Kind);
        Assert.True(result.IsUnrepresentable);
    }

    [Theory]
    [InlineData("0o8")]
    [InlineData("0o19")]
    [InlineData("0xG")]
    public void Convert_Integer_InvalidDigit_Throws(string value) =>
        Assert.Throws<InvalidOperationException>(() =>
            YamlScalarConverter.Convert(new(value, YamlScalarKind.Integer)));

    [Theory]
    [InlineData("0o8")]
    [InlineData("0o19")]
    [InlineData("0xG")]
    public void Convert_Float_InvalidDigit_Throws(string value) =>
        Assert.Throws<InvalidOperationException>(() =>
            YamlScalarConverter.Convert(new(value, YamlScalarKind.Float)));

    [Theory]
    [InlineData(".nan")]
    [InlineData(".NaN")]
    [InlineData(".NAN")]
    public void Convert_Float_NanForms_ReturnsNaN(string value)
    {
        var result = YamlScalarConverter.Convert(new(value, YamlScalarKind.Float));

        Assert.Equal(ConfigValueKind.Double, result.Kind);
        Assert.True(double.IsNaN(result.AsDouble()));
    }

    [Fact]
    public void Convert_Float_NegativeZero_PreservesSign()
    {
        var result = YamlScalarConverter.Convert(new("-0.0", YamlScalarKind.Float));

        Assert.Equal(ConfigValueKind.Double, result.Kind);
        var d = result.AsDouble();
        Assert.Equal(0.0, d);

        // double.IsNegative is not available on net462; this is the idiomatic substitute.
        Assert.Equal(double.NegativeInfinity, 1.0 / d);
    }

    [Theory]
    [InlineData("0.", 0.0)]
    [InlineData(".5", 0.5)]
    [InlineData("+.5", 0.5)]
    [InlineData("+12e03", 12000.0)]
    [InlineData("-2E+05", -200000.0)]
    [InlineData(".inf", double.PositiveInfinity)]
    [InlineData("+.INF", double.PositiveInfinity)]
    [InlineData("-.Inf", double.NegativeInfinity)]
    [InlineData("1e999", double.PositiveInfinity)]
    [InlineData("-1e999", double.NegativeInfinity)]
    [InlineData("1e-999", 0.0)]
    public void Convert_Float_StandardForms_ReturnsExpectedDouble(string value, double expected)
    {
        var result = YamlScalarConverter.Convert(new(value, YamlScalarKind.Float));

        Assert.Equal(ConfigValueKind.Double, result.Kind);
        Assert.Equal(expected, result.AsDouble());
    }

    [Fact]
    public void Convert_Float_NegativeUnderflow_ReturnsNegativeZero()
    {
        var result = YamlScalarConverter.Convert(new("-1e-999", YamlScalarKind.Float));

        Assert.Equal(ConfigValueKind.Double, result.Kind);
        var d = result.AsDouble();
        Assert.Equal(0.0, d);
        Assert.Equal(double.NegativeInfinity, 1.0 / d);
    }

    // Empty value cannot come from the resolver as Float; overflow fallback must not index [0].
    [Fact]
    public void Convert_Float_EmptyValue_DoesNotThrow_ReturnsPositiveInfinity()
    {
        var result = YamlScalarConverter.Convert(new(string.Empty, YamlScalarKind.Float));

        Assert.Equal(ConfigValueKind.Double, result.Kind);
        Assert.Equal(double.PositiveInfinity, result.AsDouble());
    }

    [Theory]
    [InlineData("5", 5.0)]
    [InlineData("+5", 5.0)]
    [InlineData("0x1F", 31.0)]
    [InlineData("0o17", 15.0)]
    public void Convert_Float_IntegerNotationViaExplicitTag_ReturnsDouble(string value, double expected)
    {
        var result = YamlScalarConverter.Convert(new(value, YamlScalarKind.Float));

        Assert.Equal(ConfigValueKind.Double, result.Kind);
        Assert.Equal(expected, result.AsDouble());
    }

    // !!float 0xFFFFFFFFFFFFFFFFFF exceeds long.MaxValue and must produce ~4.72e21, not UnrepresentableInteger.
    [Fact]
    public void Convert_Float_HexBeyondLongRange_SaturatesToDouble()
    {
        var result = YamlScalarConverter.Convert(new("0xFFFFFFFFFFFFFFFFFF", YamlScalarKind.Float));

        Assert.Equal(ConfigValueKind.Double, result.Kind);
        var d = result.AsDouble();
        Assert.True(d > 4e21 && d < 5e21, $"Expected ~4.72e21 but got {d}.");
    }

    // Fails if a new YamlScalarKind member is added without a matching case in YamlScalarConverter.
    [Fact]
    public void Convert_AllCurrentKinds_DoNotThrow()
    {
        var cases = new (string Value, YamlScalarKind Kind)[]
        {
            ("null", YamlScalarKind.Null),
            ("hello", YamlScalarKind.String),
            ("true", YamlScalarKind.Boolean),
            ("0", YamlScalarKind.Integer),
            ("0.", YamlScalarKind.Float),
        };

        foreach (var (value, kind) in cases)
        {
            _ = YamlScalarConverter.Convert(new(value, kind));
        }

        // If YamlScalarKind gains a new member, this count check fails, prompting an update here
        // and a new case in YamlScalarConverter.
#if NET
        Assert.Equal(cases.Length, Enum.GetNames<YamlScalarKind>().Length);
#else
        Assert.Equal(cases.Length, Enum.GetNames(typeof(YamlScalarKind)).Length);
#endif
    }

    [Fact]
    public void Convert_UnknownKind_Throws() =>
        Assert.Throws<InvalidOperationException>(() =>
            YamlScalarConverter.Convert(new("x", (YamlScalarKind)42)));

    [Theory]
    [MemberData(nameof(YamlScalarResolverTests.CoreSchemaExamples), MemberType = typeof(YamlScalarResolverTests))]
    public void Convert_CoreSchemaExamples_KindMatchesResolver(string value, string expectedKindName)
    {
        var node = new YamlScalarNode(value) { Style = ScalarStyle.Plain };
        var resolved = YamlScalarResolver.Resolve(node, value);
        var result = YamlScalarConverter.Convert(resolved);

        var expectedConfigKind = expectedKindName switch
        {
            "Null" => ConfigValueKind.Null,
            "Boolean" => ConfigValueKind.Boolean,
            "Integer" => ConfigValueKind.Integer,
            "Float" => ConfigValueKind.Double, // YamlScalarKind.Float maps to ConfigValueKind.Double
            "String" => ConfigValueKind.String,
            _ => throw new InvalidOperationException($"Unexpected kind name: {expectedKindName}"),
        };

        Assert.Equal(expectedConfigKind, result.Kind);
    }
}
