// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class DeclarativeConfigurationSchemaTests
{
    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    public void ResourceAttributes_NullOrEmptySequence_ThrowsSchemaError(string attributes)
    {
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes: {attributes}
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Theory]
    [InlineData("{ name: true, value: text }")]
    [InlineData("{ name: 42, value: text }")]
    [InlineData("{ name: [], value: text }")]
    [InlineData("{ name: {}, value: text }")]
    [InlineData("{ name: key, type: true, value: text }")]
    [InlineData("{ name: key, type: 42, value: text }")]
    [InlineData("{ name: key, type: [], value: text }")]
    [InlineData("{ name: key, type: {}, value: text }")]
    public void ResourceAttribute_StringFieldsWithNonStringValues_Throw(string fields)
    {
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - {fields}
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Theory]
    [InlineData("string_array", "[one, two]")]
    [InlineData("bool_array", "[true, False, TRUE]")]
    [InlineData("int_array", "[0, -42, 0o7, 0x3A]")]
    [InlineData("double_array", "[0, -42, 1.5, 2e3, .inf]")]
    public void ResourceAttribute_ValidHomogeneousArray_IsAcceptedButNotProjected(
        string type, string value)
    {
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: retained
                  value: text
                - name: array
                  type: {type}
                  value: {value}
            """;

        var configuration = ReadConfiguration(yaml);

        Assert.Equal("retained=text", configuration[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Theory]
    [InlineData("string_array", "[]")]
    [InlineData("string_array", "[one, true]")]
    [InlineData("bool_array", "[true, yes]")]
    [InlineData("int_array", "[1, 1.5]")]
    [InlineData("double_array", "[1, null]")]
    [InlineData("double_array", "[1, {}]")]
    public void ResourceAttribute_InvalidArray_Throws(string type, string value)
    {
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: array
                  type: {type}
                  value: {value}
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Theory]
    [InlineData("value: 1.0")]
    [InlineData("value: true")]
    [InlineData("value: 0x3A")]
    public void ResourceAttribute_OmittedTypeDefaultsToStringAndNonStringThrows(string value)
    {
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: attribute
                  {value}
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void ResourceAttribute_ValidArray_IsPreservedInTypedModel()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: array
                  type: int_array
                  value: [0, 0o7, 0x3A]
            """;

        var configuration = ParseConfiguration(yaml);

        Assert.True(configuration.Resource.TryGetValue(out var resource));
        Assert.True(resource.Attributes.TryGetValue(out var attributes));
        var attribute = Assert.Single(attributes);
        Assert.True(attribute.TryGetSequenceValues(out var values));
        Assert.Collection(
            values,
            value => Assert.Equal(new ResolvedYamlScalar("0", YamlScalarKind.Integer), value),
            value => Assert.Equal(new ResolvedYamlScalar("0o7", YamlScalarKind.Integer), value),
            value => Assert.Equal(new ResolvedYamlScalar("0x3A", YamlScalarKind.Integer), value));
    }

    [Theory]
    [InlineData("\"1.0\"", "1.0")]
    [InlineData("\"true\"", "true")]
    [InlineData("!!str 0x3A", "0x3A")]
    public void ResourceAttribute_ExplicitStringValue_IsProjected(string value, string expected)
    {
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: attribute
                  value: {value}
            """;

        var configuration = ReadConfiguration(yaml);

        Assert.Equal($"attribute={expected}", configuration[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Fact]
    public void ResourceAttribute_NullType_Throws()
    {
        // AttributeType's enum excludes null, so null cannot select the default string type.
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: attribute
                  type: null
                  value: text
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void BareNonSpecificTag_ForcesStringAtIntegrationBoundary()
    {
        const string yaml = """
            file_format: ! 1.0
            """;

        Assert.Empty(ReadConfiguration(yaml));
    }

    [Fact]
    public void BareNonSpecificTag_CannotSatisfyBooleanField()
    {
        const string yaml = """
            file_format: "1.0"
            disabled: ! true
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Theory]
    [InlineData("!!bool yes")]
    [InlineData("!!int 1.5")]
    [InlineData("!!float number")]
    [InlineData("!custom value")]
    public void InvalidOrUnsupportedExplicitTag_Throws(string taggedValue)
    {
        var yaml = $"""
            file_format: "1.0"
            resource:
              attributes:
                - name: attribute
                  value: {taggedValue}
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Theory]
    [InlineData("!!str { file_format: \"1.0\" }")]
    [InlineData("file_format: \"1.0\"\nresource: !!str {}")]
    [InlineData("file_format: \"1.0\"\nresource: { attributes: !!str [] }")]
    [InlineData("file_format: \"1.0\"\nresource: { attributes: [!!str { name: attribute, value: value }] }")]
    [InlineData("file_format: \"1.0\"\nresource: { attributes: [{ name: attribute, type: string_array, value: !!str [value] }] }")]
    public void CollectionWithIncompatibleExplicitTag_Throws(string yaml) =>
        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));

    [Fact]
    public void CollectionWithMatchingCoreTag_IsAccepted()
    {
        const string yaml = """
            file_format: "1.0"
            resource: !!map
              attributes: !!seq
                - !!map { name: attribute, value: value }
            """;

        var configuration = ReadConfiguration(yaml);

        Assert.Equal("attribute=value", configuration[DeclarativeConfigurationConverter.ResourceAttributesKey]);
    }

    [Theory]
    [InlineData("name", "true")]
    [InlineData("type", "true")]
    public void EnvironmentSubstitution_ResolvesAttributeMetadataTypeAfterSubstitution(
        string field, string environmentValue)
    {
        const string environmentVariable = "OTEL_DECLARATIVE_TEST_ATTRIBUTE_METADATA";
        var yaml = string.Equals(field, "name", StringComparison.Ordinal)
            ? """
                file_format: "1.0"
                resource:
                  attributes:
                    - name: ${OTEL_DECLARATIVE_TEST_ATTRIBUTE_METADATA}
                      value: text
                """
            : """
                file_format: "1.0"
                resource:
                  attributes:
                    - name: attribute
                      type: ${OTEL_DECLARATIVE_TEST_ATTRIBUTE_METADATA}
                      value: text
                """;

        using var environment = EnvironmentVariableScope.Create(environmentVariable, environmentValue);

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void EnvironmentSubstitution_ResolvesAttributeValueTypeAfterSubstitution()
    {
        const string environmentVariable = "OTEL_DECLARATIVE_TEST_ATTRIBUTE_VALUE";
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: attribute
                  type: bool
                  value: ${OTEL_DECLARATIVE_TEST_ATTRIBUTE_VALUE}
            """;

        using var environment = EnvironmentVariableScope.Create(environmentVariable, "TRUE");

        var configuration = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.ResourceAttributesKey, configuration.Keys);
    }

    [Fact]
    public void EnvironmentSubstitution_DoesNotApplyToMappingKeys()
    {
        const string environmentVariable = "OTEL_DECLARATIVE_TEST_MAPPING_KEY";
        const string yaml = """
            file_format: "1.0"
            ${OTEL_DECLARATIVE_TEST_MAPPING_KEY}: true
            """;

        using var environment = EnvironmentVariableScope.Create(environmentVariable, YamlKeys.Disabled);

        var configuration = ReadConfiguration(yaml);

        Assert.DoesNotContain(DeclarativeConfigurationConverter.DisabledKey, configuration.Keys);
    }

    [Fact]
    public void EnvironmentSubstitution_CannotInjectYamlMappingStructure()
    {
        const string environmentVariable = "OTEL_DECLARATIVE_TEST_MAPPING_INJECTION";
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: attribute
                  value: ${OTEL_DECLARATIVE_TEST_MAPPING_INJECTION}
            """;

        using var environment = EnvironmentVariableScope.Create(environmentVariable, "value\ninjected: true");

        var configuration = ReadConfiguration(yaml);

        Assert.Equal(
            "attribute=value\ninjected: true",
            configuration[DeclarativeConfigurationConverter.ResourceAttributesKey]);
        Assert.DoesNotContain(DeclarativeConfigurationConverter.DisabledKey, configuration.Keys);
    }

    [Fact]
    public void EnvironmentSubstitution_InvalidReferenceInIgnoredTopLevelSection_Throws()
    {
        const string yaml = """
            file_format: "1.1"
            distribution:
              vendor:
                setting: ${VALUE:?error}
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void EnvironmentSubstitution_InvalidReferenceInIgnoredTopLevelSequence_Throws()
    {
        const string yaml = """
            file_format: "1.1"
            extension:
              - ${VALUE:?error}
            """;

        Assert.Throws<DeclarativeConfigurationException>(() => ReadConfiguration(yaml));
    }

    [Fact]
    public void EnvironmentSubstitution_ValidReferenceInIgnoredTopLevelSection_DoesNotContributeConfiguration()
    {
        const string environmentVariable = "OTEL_DECLARATIVE_TEST_IGNORED_SECTION_UNSET";
        const string yaml = """
            file_format: "1.1"
            distribution:
              vendor:
                setting: ${OTEL_DECLARATIVE_TEST_IGNORED_SECTION_UNSET}
            """;

        // Leave the variable unset: ignored sections must still accept valid references without
        // requiring the variable to be present for this package to load the document.
        using var environment = EnvironmentVariableScope.Create(environmentVariable, null);

        var configuration = ReadConfiguration(yaml);

        Assert.Empty(configuration);
    }

    private static ReadOnlyDictionary<string, string?> ReadConfiguration(string yaml)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        return DeclarativeConfigurationReader.Read(new FilePath(factory.CreateYamlFile(yaml))).FlatKeys;
    }

    private static DeclarativeConfiguration ParseConfiguration(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        var root = Assert.IsType<YamlMappingNode>(stream.Documents[0].RootNode);
        _ = root.EnsureUniqueStringKeys("<root>");
        return new DeclarativeConfigurationParser(new YamlParseContext(Environment.GetEnvironmentVariable)).Parse(root, "1.0");
    }
}
