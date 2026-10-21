// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.Core;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class YamlDocumentTrackingParserTests
{
    [Theory]
    [InlineData(
        "file_format: \"1.2\"\nresource:\n  *key : 1\n  bar: 2\nvendor_key: &key foo\n",
        3,
        3)]
    [InlineData(
        "*key : 1\nfile_format: \"1.2\"\nvendor_key: &key foo\n",
        1,
        1)]
    [InlineData(
        "file_format: \"1.2\"\nresource: {*key : 1, bar: 2}\nvendor_key: &key foo\n",
        2,
        12)]
    [InlineData(
        "file_format: \"1.2\"\nvendor:\n  - *key : 1\nvendor_key: &key foo\n",
        3,
        5)]
    [InlineData(
        "file_format: \"1.2\"\nresource:\n  *key : 1\n  *key : 2\n",
        3,
        3)]
    [InlineData(
        "file_format: \"1.2\"\nvendor_key: &key foo\n---\nfile_format: \"1.2\"\nresource:\n  *key : 1\n",
        6,
        3)]
    public void Read_UnresolvedAliasKey_ThrowsYamlExceptionAtItsOccurrence(string yaml, int line, int column)
    {
        var exception = Assert.Throws<YamlException>(() => Read(yaml));

        Assert.Equal(
            "YAML mapping key alias '*key' must refer to an anchor defined earlier in the same document.",
            exception.Message);
        Assert.Equal(line, exception.Start.Line);
        Assert.Equal(column, exception.Start.Column);
    }

    [Fact]
    public void Read_ForwardAliasValue_Loads()
    {
        var document = Read("""
            file_format: "1.2"
            vendor:
              alias: *value
              definition: &value {name: example}
            """);

        Assert.True(document.Properties.GetMapping("vendor").TryGetValue(out var vendor), "Failed to get 'vendor' mapping from document properties.");
        Assert.True(vendor.GetMapping("alias").TryGetValue(out var alias), "Failed to get 'alias' mapping from 'vendor'.");
        Assert.True(alias.GetString("name").TryGetValue(out var name), "Failed to get 'name' string from 'alias' mapping.");
        Assert.Equal("example", name);
    }

    [Fact]
    public void Read_AnchorReusedForAliasKeys_PreservesEachOccurrencePosition()
    {
        var document = Read("""
            file_format: "1.2"
            vendor:
              first: &key first_name
              *key : 1
              second: &key second_name
              *key : 2
            """);

        Assert.True(document.Properties.GetMapping("vendor").TryGetValue(out var vendor), "Failed to get 'vendor' mapping from document properties.");
        Assert.Equal(new ConfigValuePosition(4, 3), vendor.GetKeyPosition("first_name"));
        Assert.Equal(new ConfigValuePosition(6, 3), vendor.GetKeyPosition("second_name"));
        Assert.True(vendor.GetLong("first_name").TryGetValue(out var first), "Failed to get 'first_name' long from 'vendor'.");
        Assert.True(vendor.GetLong("second_name").TryGetValue(out var second), "Failed to get 'second_name' long from 'vendor'.");
        Assert.Equal(1L, first);
        Assert.Equal(2L, second);
    }

    [Fact]
    public void Read_AnchorDefinedInsideSequence_CanBeUsedAsALaterKey()
    {
        var document = Read("""
            file_format: "1.2"
            vendor:
              definitions: [&key nested_key]
              values:
                *key : 1
            """);

        Assert.True(document.Properties.GetMapping("vendor").TryGetValue(out var vendor), "Failed to get 'vendor' mapping from document properties.");
        Assert.True(vendor.GetMapping("values").TryGetValue(out var values), "Failed to get 'values' mapping from 'vendor'.");
        Assert.Equal(new ConfigValuePosition(5, 5), values.GetKeyPosition("nested_key"));
    }

    [Fact]
    public void Read_MultipleDocuments_PreservesFirstDocumentAliasKeyPositions()
    {
        var document = Read("""
            file_format: "1.2"
            vendor:
              name: &key first
              *key : 1
            ---
            file_format: "1.2"
            vendor:
              name: &key second
              *key : 2
            """);

        Assert.True(document.Properties.GetMapping("vendor").TryGetValue(out var vendor), "Failed to get 'vendor' mapping from document properties.");
        Assert.Equal(new ConfigValuePosition(4, 3), vendor.GetKeyPosition("first"));
        Assert.DoesNotContain("second", vendor.Keys);
    }

    private static DeclarativeConfigurationDocument Read(string yaml)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        return DeclarativeConfigurationReader.Read(new FilePath(factory.CreateYamlFile(yaml)));
    }
}
