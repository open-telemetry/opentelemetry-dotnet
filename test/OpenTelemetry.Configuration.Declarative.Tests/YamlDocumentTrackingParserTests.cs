// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class YamlDocumentTrackingParserTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Load_TagAtMaximumLength_IsAccepted(bool useDirective)
    {
        const string prefix = "tag:example.com,2026:";
        var tag = prefix + new string('a', 1024 - prefix.Length - 1) + "s";
        var yaml = useDirective
            ? $"%TAG !e! {tag.Substring(0, tag.Length - 1)}\n---\nvendor: !e!s 1\n"
            : $"vendor: !<{tag}> 1\n";
        using var reader = new StringReader(yaml);
        var stream = new YamlStream();

        stream.Load(new YamlDocumentTrackingParser(new Parser(reader)));

        Assert.Equal(tag, stream.Documents[0].RootNode[new YamlScalarNode("vendor")].Tag.Value);
    }

    [Theory]
    [InlineData("1", false)]
    [InlineData("[]", false)]
    [InlineData("{}", false)]
    [InlineData("1", true)]
    [InlineData("[]", true)]
    [InlineData("{}", true)]
    public void Load_TagAboveMaximumLength_IsRejectedBeforeAdvancingPastIt(string value, bool useDirective)
    {
        const string prefix = "tag:example.com,2026:";
        var expandedPrefix = prefix + new string('a', 1024 - prefix.Length);
        var header = useDirective ? $"%TAG !e! {expandedPrefix}\n---\n" : string.Empty;
        var tag = useDirective ? "!e!s" : $"!<{expandedPrefix}s>";
        var yaml = $"{header}vendor:\n  - {tag} {value}\n  - {tag} {value}\n";
        using var reader = new StringReader(yaml);
        var innerParser = new TaggedNodeLimitedParser(new Parser(reader));
        var stream = new YamlStream();

        var exception = Assert.Throws<DeclarativeConfigurationException>(() =>
            stream.Load(new YamlDocumentTrackingParser(innerParser)));

        Assert.Equal(
            FormattableString.Invariant(
                $"YAML tag exceeds the maximum length of 1024 characters (line {(useDirective ? 4 : 2)}, column 5)."),
            exception.Message);
        Assert.Equal(1, innerParser.TaggedNodes);
        Assert.Empty(stream.Documents);
    }

    [Fact]
    public void Read_CoreTagDirective_IsAccepted()
    {
        var document = Read("""
            %TAG !core! tag:yaml.org,2002:
            ---
            !core!map
            file_format: !core!str 1.2
            disabled: !core!bool true
            vendor: !core!seq [!core!map {name: !core!str example}]
            """);

        Assert.Equal("true", document.FlatKeys[DeclarativeConfigurationConverter.DisabledKey]);
        Assert.Contains("vendor", document.Properties.Keys);
    }

    [Fact]
    public void Read_TagAboveMaximumLengthInSecondDocument_IsRejected()
    {
        var tag = "!" + new string('a', 1024);

        var exception = Assert.Throws<DeclarativeConfigurationException>(() =>
            Read($"file_format: \"1.2\"\n---\nvendor: !<{tag}> 1\n"));

        Assert.Equal(
            "YAML tag exceeds the maximum length of 1024 characters (line 3, column 9).",
            exception.Message);
    }

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

    [Theory]
    [InlineData("vendor:\n  ? [a, b]\n  : 1\n", 2, 5)]
    [InlineData("vendor:\n  ? {a: b}\n  : 1\n", 2, 5)]
    [InlineData("vendor: &key [a, b]\n? *key\n: 1\n", 2, 3)]
    [InlineData("vendor: &key {a: b}\n? *key\n: 1\n", 2, 3)]
    [InlineData("? &key [*key]\n: 1\n", 1, 3)]
    [InlineData("first: &key name\nsecond: &key []\n? *key\n: 1\n", 3, 3)]
    [InlineData("first: &key name\nsecond: &key {}\n? *key\n: 1\n", 3, 3)]
    public void Load_CollectionKey_IsRejectedBeforeAdvancingPastIt(string yaml, int keyLine, int keyColumn)
    {
        using var reader = new StringReader(yaml);
        var innerParser = new KeyLimitedParser(new Parser(reader), keyLine, keyColumn);
        var stream = new YamlStream();

        var exception = Assert.Throws<DeclarativeConfigurationException>(() =>
            stream.Load(new YamlDocumentTrackingParser(innerParser)));

        Assert.Equal(
            FormattableString.Invariant($"YAML mapping keys must be string scalars (line {keyLine}, column {keyColumn})."),
            exception.Message);
        Assert.Empty(stream.Documents);
    }

    [Fact]
    public void Load_CollectionAliasKey_IsRejectedBeforeHashingItsExpandedSubtree()
    {
        const string yaml = """
            first: &first [1]
            second: &second [*first, *first]
            third: &third [*second, *second]
            ? *third
            : 1
            """;
        using var reader = new StringReader(yaml);
        var innerParser = new KeyLimitedParser(new Parser(reader), 4, 3);
        var stream = new YamlStream();

        var exception = Assert.Throws<DeclarativeConfigurationException>(() =>
            stream.Load(new YamlDocumentTrackingParser(innerParser)));

        Assert.Equal("YAML mapping keys must be string scalars (line 4, column 3).", exception.Message);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    public void Read_CollectionAnchorRedefinedAsScalar_CanBeUsedAsALaterKey(string collection)
    {
        var document = Read($$"""
            file_format: "1.2"
            vendor:
              collection: &key {{collection}}
              name: &key scalar_key
              *key : 1
            """);

        Assert.True(document.Properties.GetMapping("vendor").TryGetValue(out var vendor), "Failed to get 'vendor' mapping from document properties.");
        Assert.True(vendor.GetLong("scalar_key").TryGetValue(out var value), "Failed to get 'scalar_key' long from 'vendor'.");
        Assert.Equal(1L, value);
        Assert.Equal(new ConfigValuePosition(5, 3), vendor.GetKeyPosition("scalar_key"));
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

    private sealed class TaggedNodeLimitedParser(IParser innerParser) : IParser
    {
        public ParsingEvent? Current => innerParser.Current;

        public int TaggedNodes { get; private set; }

        public bool MoveNext()
        {
            Assert.True(this.TaggedNodes == 0, "The parser advanced past an overlong YAML tag.");

            var moved = innerParser.MoveNext();
            if (moved && this.Current is NodeEvent { Tag: { IsEmpty: false } })
            {
                this.TaggedNodes++;
            }

            return moved;
        }
    }

    private sealed class KeyLimitedParser(IParser innerParser, int keyLine, int keyColumn) : IParser
    {
        private bool keySeen;

        public ParsingEvent? Current => innerParser.Current;

        public bool MoveNext()
        {
            Assert.False(this.keySeen, "The parser advanced past an unsupported collection key.");

            var moved = innerParser.MoveNext();
            if (moved && this.Current is { } current)
            {
                this.keySeen = current.Start.Line == keyLine && current.Start.Column == keyColumn
                    && current is MappingStart or SequenceStart or AnchorAlias;
            }

            return moved;
        }
    }
}
