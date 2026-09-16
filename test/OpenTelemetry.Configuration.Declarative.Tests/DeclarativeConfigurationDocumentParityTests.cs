// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class DeclarativeConfigurationDocumentParityTests
{
    [Theory]
    [MemberData(nameof(YamlScalarResolverTests.CoreSchemaExamples), MemberType = typeof(YamlScalarResolverTests))]
    public void DocumentValue_ReadsAsTheKindTheResolverAssigned(string value, string expectedKind)
    {
        var vendor = ReadProbe(value);

        switch (expectedKind)
        {
            case "Boolean":
                Assert.Equal(ConfigValueOutcome.Present, vendor.GetBoolean("probe").Outcome);
                Assert.Equal(ConfigValueOutcome.TypeMismatch, vendor.GetString("probe").Outcome);
                break;

            case "Float":
                Assert.Equal(ConfigValueOutcome.Present, vendor.GetDouble("probe").Outcome);
                Assert.Equal(ConfigValueOutcome.TypeMismatch, vendor.GetString("probe").Outcome);
                break;

            case "Integer":
                Assert.Equal(ConfigValueOutcome.Present, vendor.GetLong("probe").Outcome);

                // An integer is readable as a double; the number domain is shared with the schema.
                Assert.Equal(ConfigValueOutcome.Present, vendor.GetDouble("probe").Outcome);
                Assert.Equal(ConfigValueOutcome.TypeMismatch, vendor.GetString("probe").Outcome);
                break;

            case "Null":
                Assert.Equal(ConfigValueOutcome.PresentNull, vendor.GetString("probe").Outcome);
                Assert.Equal(ConfigValueOutcome.PresentNull, vendor.GetDouble("probe").Outcome);
                break;

            case "String":
                Assert.Equal(value, AssertPresent(vendor.GetString("probe")));
                Assert.Equal(ConfigValueOutcome.TypeMismatch, vendor.GetLong("probe").Outcome);
                break;

            default:
                Assert.Fail($"Unhandled YAML scalar kind '{expectedKind}'.");
                break;
        }
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("+42", 42L)]
    [InlineData("-19", -19L)]
    [InlineData("007", 7L)]
    [InlineData("0o7", 7L)]
    [InlineData("0x3A", 58L)]
    [InlineData("0xdeadbeef", 3735928559L)]
    public void DocumentValue_Integer_ReadsAsTheConvertedValueNotTheLexeme(string value, long expected) =>
        Assert.Equal(expected, AssertPresent(ReadProbe(value).GetLong("probe")));

    [Fact]
    public void DocumentValue_SchemaValidWidening_IsReadableInBothDirections()
    {
        Assert.Equal(1.0, AssertPresent(ReadProbe("1").GetDouble("probe")));
        Assert.Equal(5L, AssertPresent(ReadProbe("5.0").GetLong("probe")));
        Assert.Equal(5, AssertPresent(ReadProbe("5.0").GetInt("probe")));
    }

    [Theory]
    [InlineData("1e999", double.PositiveInfinity)]
    [InlineData("-1e999", double.NegativeInfinity)]
    [InlineData("1e-999", 0.0)]
    public void DocumentValue_FloatOutOfRange_NormalisesConsistentlyAcrossRuntimes(string value, double expected) =>
        Assert.Equal(expected, AssertPresent(ReadProbe(value).GetDouble("probe")));

    [Fact]
    public void DocumentValue_FloatUnderflow_PreservesTheSign()
    {
        var value = AssertPresent(ReadProbe("-1e-999").GetDouble("probe"));

        Assert.Equal(0.0, value);
        Assert.Equal(double.NegativeInfinity, 1.0 / value);
    }

    private static ConfigProperties ReadProbe(string value)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        var path = factory.CreateYamlFile("file_format: \"1.0\"\nvendor:\n  probe: " + value + "\n");

        // The injectable overload keeps the probe hermetic: a core-schema example is embedded raw,
        // so binding the real environment would make these assertions machine-dependent.
        var properties = DeclarativeConfigurationReader.Read(new FilePath(path), _ => null).Properties;

        return AssertPresent(properties.GetProperties("vendor"));
    }

    private static T AssertPresent<T>(ConfigValueResult<T> result)
    {
        Assert.Equal(ConfigValueOutcome.Present, result.Outcome);
        return result.Value!;
    }
}
