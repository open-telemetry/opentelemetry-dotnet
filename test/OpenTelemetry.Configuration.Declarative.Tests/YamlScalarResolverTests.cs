// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class YamlScalarResolverTests
{
    public static TheoryData<string, string> CoreSchemaExamples => new()
    {
        { string.Empty, "Null" },
        { "~", "Null" },
        { "null", "Null" },
        { "Null", "Null" },
        { "NULL", "Null" },
        { "true", "Boolean" },
        { "True", "Boolean" },
        { "TRUE", "Boolean" },
        { "false", "Boolean" },
        { "False", "Boolean" },
        { "FALSE", "Boolean" },
        { "0", "Integer" },
        { "+42", "Integer" },
        { "-19", "Integer" },
        { "007", "Integer" },
        { "0o7", "Integer" },
        { "0x3A", "Integer" },
        { "0xdeadbeef", "Integer" },
        { "-0o7", "String" },
        { "+0x3A", "String" },
        { "+0o7", "String" },
        { "-0x3A", "String" },
        { "0.", "Float" },
        { "-0.0", "Float" },
        { ".5", "Float" },
        { "+12e03", "Float" },
        { "-2E+05", "Float" },
        { ".inf", "Float" },
        { "-.Inf", "Float" },
        { "+.INF", "Float" },
        { ".nan", "Float" },
        { ".NaN", "Float" },
        { ".NAN", "Float" },
    };

    public static TheoryData<string> CoreSchemaStrings =>
    [
        "yes",
        "no",
        "on",
        "off",
        "tRue",
        "FaLsE",
        "1_000",
        "0O7",
        "0X3A",
        "0b1010",
        "0xhello",
        "1e",
        "+.",
        ".",
        "-",
        "+.NAN",
        "Infinity",
        "1.2.3",
        " 1.0 ",
        "\ttrue\t",
        "value\nkey:value",
    ];

    [Theory]
    [MemberData(nameof(CoreSchemaExamples))]
    public void Resolve_ImplicitPlainScalar_MatchesYaml12CoreSchema(string value, string expected)
    {
        var resolved = YamlScalarResolver.Resolve(Scalar(value, ScalarStyle.Plain), value);

        Assert.Equal(expected, resolved.Kind.ToString());
        Assert.Equal(value, resolved.Value);
    }

    [Theory]
    [MemberData(nameof(CoreSchemaStrings))]
    public void Resolve_ImplicitPlainString_MatchesYaml12CoreSchemaFallback(string value) =>
        Assert.Equal(YamlScalarKind.String, YamlScalarResolver.Resolve(Scalar(value), value).Kind);

    [Theory]
    [InlineData(ScalarStyle.SingleQuoted)]
    [InlineData(ScalarStyle.DoubleQuoted)]
    [InlineData(ScalarStyle.Literal)]
    [InlineData(ScalarStyle.Folded)]
    public void Resolve_NonPlainScalar_IsAlwaysString(ScalarStyle style)
    {
        foreach (var value in new[] { "null", "true", "42", "1.5", "value" })
        {
            Assert.Equal(YamlScalarKind.String, YamlScalarResolver.Resolve(Scalar(value, style), value).Kind);
        }
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("FALSE", false)]
    public void TryGetBoolean_CoreSchemaRepresentation_ReturnsValue(string value, bool expected)
    {
        Assert.True(YamlScalarResolver.TryGetBoolean(value, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(".inf", true)]
    [InlineData("+.INF", true)]
    [InlineData("-.Inf", true)]
    [InlineData(".nan", false)]
    [InlineData("Infinity", false)]
    [InlineData("1.0", false)]
    public void IsInfinity_CoreSchemaForms_ReturnsExpected(string value, bool expected) =>
        Assert.Equal(expected, YamlScalarResolver.IsInfinity(value));

    [Theory]
    [InlineData(".nan", true)]
    [InlineData(".NaN", true)]
    [InlineData(".NAN", true)]
    [InlineData("+.nan", false)]
    [InlineData("-.NAN", false)]
    [InlineData("NaN", false)]
    [InlineData(".inf", false)]
    public void IsNaN_CoreSchemaForms_ReturnsExpected(string value, bool expected) =>
        Assert.Equal(expected, YamlScalarResolver.IsNaN(value));

    [Theory]
    [InlineData(YamlScalarResolver.StringTag, "1.0", "String")]
    [InlineData(YamlScalarResolver.NullTag, "null", "Null")]
    [InlineData(YamlScalarResolver.BooleanTag, "true", "Boolean")]
    [InlineData(YamlScalarResolver.IntegerTag, "0x3A", "Integer")]
    [InlineData(YamlScalarResolver.FloatTag, "1", "Float")]
    [InlineData(YamlScalarResolver.FloatTag, "1.5", "Float")]
    public void Resolve_ValidExplicitCoreTag_OverridesStyle(string tag, string value, string expected)
    {
        var resolved = YamlScalarResolver.Resolve(Tagged(value, tag, ScalarStyle.DoubleQuoted), value);

        Assert.Equal(expected, resolved.Kind.ToString());
    }

    [Theory]
    [InlineData(YamlScalarResolver.NullTag, "nil")]
    [InlineData(YamlScalarResolver.BooleanTag, "yes")]
    [InlineData(YamlScalarResolver.IntegerTag, "1.5")]
    [InlineData(YamlScalarResolver.FloatTag, "number")]
    public void Resolve_InvalidExplicitCoreTagRepresentation_Throws(string tag, string value) =>
        Assert.Throws<DeclarativeConfigurationException>(() =>
            YamlScalarResolver.Resolve(Tagged(value, tag), value));

    [Fact]
    public void Resolve_UnsupportedExplicitTag_Throws() =>
        Assert.Throws<DeclarativeConfigurationException>(() =>
            YamlScalarResolver.Resolve(Tagged("value", "!custom"), "value"));

    [Theory]
    [InlineData("1.0")]
    [InlineData("true")]
    [InlineData("null")]
    public void Resolve_BareNonSpecificTag_ForcesString(string value)
    {
        var scalar = Tagged(value, "!");

        Assert.Equal(YamlScalarKind.String, YamlScalarResolver.Resolve(scalar, value).Kind);
    }

    [Theory]
    [InlineData("1.0", "Float")]
    [InlineData("true", "Boolean")]
    [InlineData("null", "Null")]
    public void Resolve_QuestionNonSpecificTag_UsesCoreResolution(string value, string expected)
    {
        var scalar = Tagged(value, "?");

        Assert.Equal(expected, YamlScalarResolver.Resolve(scalar, value).Kind.ToString());
    }

    private static YamlScalarNode Scalar(string value, ScalarStyle style = ScalarStyle.Plain) =>
        new(value) { Style = style };

    private static YamlScalarNode Tagged(string value, string tag, ScalarStyle style = ScalarStyle.Plain) =>
        new(value) { Style = style, Tag = new TagName(tag) };
}
