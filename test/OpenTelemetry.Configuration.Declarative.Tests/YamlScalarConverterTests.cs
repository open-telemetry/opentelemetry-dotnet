// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class YamlScalarConverterTests
{
    public static TheoryData<string, long> DecimalFloatRoundingCases => new()
    {
        { "0.1", 4591870180066957722L },
        { "0.2", 4596373779694328218L },
        { "0.3", 4599075939470750515L },
        { "1.1", 4607632778762754458L },
        { "3.14", 4614253070214989087L },
        { "1e-10", 4457293557087583675L },
        { "1.23456789", 4608238818662014491L },
        { "-0.0", -9223372036854775808L },
    };

    public static TheoryData<string, double> FloatIntegerOverflowBoundaries => new()
    {
        { "0x" + new string('f', 13) + "8" + new string('0', 242), double.MaxValue },
        { "0x" + new string('f', 13) + "b" + new string('f', 242), double.MaxValue },
        { "0x" + new string('f', 13) + "c" + new string('0', 242), double.PositiveInfinity },
        { "0x1" + new string('0', 256), double.PositiveInfinity },
        { "0o1" + new string('7', 17) + "4" + new string('0', 323), double.MaxValue },
        { "0o1" + new string('7', 17) + "5" + new string('7', 323), double.MaxValue },
        { "0o1" + new string('7', 17) + "6" + new string('0', 323), double.PositiveInfinity },
        { "0o2" + new string('0', 341), double.PositiveInfinity },
    };

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
    [InlineData("1.5", 1.5)]
    [InlineData("3.14", 3.14)]
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
    [MemberData(nameof(DecimalFloatRoundingCases))]
    public void Convert_Float_Decimal_IsExact(string value, long expectedBits)
    {
        var result = YamlScalarConverter.Convert(new(value, YamlScalarKind.Float));

        Assert.Equal(ConfigValueKind.Double, result.Kind);
        Assert.Equal(expectedBits, BitConverter.DoubleToInt64Bits(result.AsDouble()));
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

    [Theory]
    [InlineData("0x0", 0.0)]
    [InlineData("0o0", 0.0)]
    [InlineData("0x000200000000000011", 144115188075855904.0)]
    [InlineData("0o00010000000000000000021", 144115188075855904.0)]
    [InlineData("0x1fffffffffffff", 9007199254740991.0)]
    [InlineData("0o377777777777777777", 9007199254740991.0)]
    [InlineData("0x20000000000000", 9007199254740992.0)]
    [InlineData("0o400000000000000000", 9007199254740992.0)]
    [InlineData("0x20000000000001", 9007199254740992.0)]
    [InlineData("0o400000000000000001", 9007199254740992.0)]
    [InlineData("0x20000000000003", 9007199254740996.0)]
    [InlineData("0o400000000000000003", 9007199254740996.0)]
    [InlineData("0x200000000000011", 144115188075855904.0)]
    [InlineData("0o10000000000000000021", 144115188075855904.0)]
    [InlineData("0x20000000000002f", 144115188075855904.0)]
    [InlineData("0o10000000000000000057", 144115188075855904.0)]
    [InlineData("0x20000000000001100", 36893488147419111424.0)]
    [InlineData("0o4000000000000000010400", 36893488147419111424.0)]
    [InlineData("0x3fffffffffffff", 18014398509481984.0)]
    [InlineData("0o777777777777777777", 18014398509481984.0)]
    public void Convert_Float_IntegerNotation_RoundsToNearestEven(string value, double expected)
    {
        var result = YamlScalarConverter.Convert(new(value, YamlScalarKind.Float));

        Assert.Equal(expected, result.AsDouble());
    }

    [Theory]
    [InlineData("0x20000000000001", 4)]
    [InlineData("0o400000000000000001", 3)]
    public void Convert_Float_IntegerNotation_DistantNonzeroDigitRoundsAboveTie(string prefix, int bitsPerDigit)
    {
        var value = prefix + new string('0', 100) + "1";
        var expected = 9007199254740994.0 * Math.Pow(2, 101 * bitsPerDigit);

        var result = YamlScalarConverter.Convert(new(value, YamlScalarKind.Float));

        Assert.Equal(expected, result.AsDouble());
    }

    [Theory]
    [MemberData(nameof(FloatIntegerOverflowBoundaries))]
    public void Convert_Float_IntegerNotation_OverflowBoundaries(string value, double expected)
    {
        var result = YamlScalarConverter.Convert(new(value, YamlScalarKind.Float));

        Assert.Equal(expected, result.AsDouble());
    }

    // !!float 0xFFFFFFFFFFFFFFFFFF exceeds long.MaxValue and must produce ~4.72e21, not UnrepresentableInteger.
    [Fact]
    public void Convert_Float_HexBeyondLongRange_SaturatesToDouble()
    {
        var result = YamlScalarConverter.Convert(new("0xFFFFFFFFFFFFFFFFFF", YamlScalarKind.Float));

        Assert.Equal(ConfigValueKind.Double, result.Kind);
        var d = result.AsDouble();
        Assert.True(d is > 4e21 and < 5e21, $"Expected ~4.72e21 but got {d}.");
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
