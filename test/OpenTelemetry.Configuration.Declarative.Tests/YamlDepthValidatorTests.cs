// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class YamlDepthValidatorTests
{
    [Theory]
    [InlineData("mapping")]
    [InlineData("sequence")]
    [InlineData("mixed")]
    public void Read_AtMaximumDepth_Loads(string kind)
    {
        var document = Read(NestedDocument(YamlDepthValidator.MaximumDepth, kind));

        Assert.Contains("vendor", document.Properties.Keys);
    }

    [Theory]
    [InlineData("mapping", 1033)]
    [InlineData("sequence", 137)]
    [InlineData("mixed", 585)]
    public void Read_AboveMaximumDepth_Throws(string kind, int column)
    {
        var exception = AssertDepthExceeded(() =>
            Read(NestedDocument(YamlDepthValidator.MaximumDepth + 1, kind)));

        Assert.Equal(
            $"YAML collection nesting exceeds the maximum depth of 128 (line 2, " +
            $"column {column.ToString(CultureInfo.InvariantCulture)}). The document root has depth 0.",
            exception.Message);
    }

    [Theory]
    [InlineData("mapping")]
    [InlineData("sequence")]
    public void Read_BlockCollectionsAtMaximumDepth_Loads(string kind)
    {
        var document = Read(NestedBlockDocument(YamlDepthValidator.MaximumDepth, kind));

        Assert.Contains("vendor", document.Properties.Keys);
    }

    [Theory]
    [InlineData("mapping")]
    [InlineData("sequence")]
    public void Read_BlockCollectionsAboveMaximumDepth_Throws(string kind)
    {
        var exception = AssertDepthExceeded(() =>
            Read(NestedBlockDocument(YamlDepthValidator.MaximumDepth + 1, kind)));

        Assert.Equal(
            "YAML collection nesting exceeds the maximum depth of 128 (line 131, " +
            "column 259). The document root has depth 0.",
            exception.Message);
    }

    [Fact]
    public void Load_ExtremeSyntacticDepth_ThrowsBeforeAdvancingPastTheExcessiveCollection()
    {
        using var reader = new StringReader(NestedDocument(10_000, "sequence"));
        var innerParser = new CollectionStartLimitedParser(
            new Parser(reader),
            YamlDepthValidator.MaximumDepth + 2);
        var parser = new YamlDocumentTrackingParser(innerParser);
        var stream = new YamlStream();

        AssertDepthExceeded(() => stream.Load(parser));

        Assert.Equal(YamlDepthValidator.MaximumDepth + 2, innerParser.CollectionStarts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_AliasChainAtMaximumDepth_Loads(bool reverse)
    {
        var document = Read(AliasedDocument(YamlDepthValidator.MaximumDepth, reverse));

        Assert.Contains("node0", document.Properties.Keys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_AliasChainAboveMaximumDepth_Throws(bool reverse)
    {
        AssertDepthExceeded(() =>
            Read(AliasedDocument(YamlDepthValidator.MaximumDepth + 1, reverse)));
    }

    [Fact]
    public void Read_SharedSubtreeAtDeeperAliasWithinLimit_Loads()
    {
        var nested = NestedDocument(YamlDepthValidator.MaximumDepth - 1, "sequence", anchored: true);

        var document = Read(nested + "\nother: [*shared]\n");

        Assert.Contains("other", document.Properties.Keys);
    }

    [Fact]
    public void Read_SharedSubtreeAtDeeperAlias_Throws()
    {
        var nested = NestedDocument(YamlDepthValidator.MaximumDepth, "sequence", anchored: true);
        var exception = AssertDepthExceeded(() =>
            Read(nested + "\nother: [*shared]\n"));

        Assert.Equal(
            "YAML collection nesting exceeds the maximum depth of 128 (line 2, " +
            "column 9). The document root has depth 0.",
            exception.Message);
    }

    [Theory]
    [InlineData(63, true)]
    [InlineData(64, false)]
    public void Read_RecursiveSampler_RespectsTheDepthLimit(int wrappers, bool accepted)
    {
        var yaml = new StringBuilder("file_format: \"1.2\"\ntracer_provider: {sampler: ");
        for (var i = 0; i < wrappers; i++)
        {
            yaml.Append("{parent_based: {root: ");
        }

        yaml.Append("{always_on: null}");
        for (var i = 0; i < wrappers; i++)
        {
            yaml.Append("}}");
        }

        yaml.Append('}');
        if (accepted)
        {
            Assert.Contains("tracer_provider", Read(yaml.ToString()).Properties.Keys);
        }
        else
        {
            AssertDepthExceeded(() => Read(yaml.ToString()));
        }
    }

    [Fact]
    public void Read_MultipleDocuments_ResetsTheSyntacticDepth()
    {
        var yaml = NestedDocument(YamlDepthValidator.MaximumDepth, "sequence");

        var document = Read(yaml + "\n---\n" + yaml);

        Assert.Contains("vendor", document.Properties.Keys);
    }

    [Fact]
    public void ThrowIfExceeded_ExtremeResolvedDepth_ThrowsWithoutExhaustingTheCallStack()
    {
        var root = CreateSequenceChain(10_001, out _);

        AssertDepthExceeded(() =>
            YamlDepthValidator.ThrowIfExceeded(root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThrowIfExceeded_SharedSubtreesAtMaximumDepth_AreNotCycles(bool reverse)
    {
        var shared = CreateSequenceChain(YamlDepthValidator.MaximumDepth - 2, out _);

        var first = new YamlSequenceNode(shared, shared);
        var second = new YamlSequenceNode(first, shared);
        var root = reverse
            ? new YamlSequenceNode(second, first, shared)
            : new YamlSequenceNode(shared, first, second);

        YamlDepthValidator.ThrowIfExceeded(root);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThrowIfExceeded_CollectionKeysAndValues_RespectTheDepthLimit(bool exceedKeyDepth)
    {
        var key = CreateSequenceChain(YamlDepthValidator.MaximumDepth, out var deepestKey);
        var value = CreateSequenceChain(YamlDepthValidator.MaximumDepth, out var deepestValue);

        var root = new YamlMappingNode { { key, value } };
        YamlDepthValidator.ThrowIfExceeded(root);

        (exceedKeyDepth ? deepestKey : deepestValue).Add(new YamlSequenceNode());

        AssertDepthExceeded(() =>
            YamlDepthValidator.ThrowIfExceeded(root));
    }

    [Fact]
    public void ThrowIfExceeded_ScalarRoot_DoesNotThrow() =>
        YamlDepthValidator.ThrowIfExceeded(new YamlScalarNode("value"));

    [Fact]
    public void ThrowIfExceeded_SelfReferentialMapping_ThrowsCycleDiagnostic()
    {
        var root = new YamlMappingNode();
        root.Add("self", root);

        var exception = Assert.Throws<DeclarativeConfigurationException>(() =>
            YamlDepthValidator.ThrowIfExceeded(root));

        Assert.Equal(
            "YAML alias at '<root>.self' refers to a node that contains it. A declarative " +
            "configuration document cannot contain a cycle.",
            exception.Message);
    }

    [Fact]
    public void ThrowIfExceeded_MappingSequenceCycle_ThrowsCycleDiagnostic()
    {
        var root = new YamlMappingNode();
        var items = new YamlSequenceNode(root);
        root.Add("items", items);

        var exception = Assert.Throws<DeclarativeConfigurationException>(() =>
            YamlDepthValidator.ThrowIfExceeded(root));

        Assert.Equal(
            "YAML alias at '<root>.items[0]' refers to a node that contains it. A declarative " +
            "configuration document cannot contain a cycle.",
            exception.Message);
    }

    private static DeclarativeConfigurationException AssertDepthExceeded(Action action)
    {
        var exception = Assert.Throws<DeclarativeConfigurationException>(action);
        Assert.Contains("maximum depth of 128", exception.Message, StringComparison.Ordinal);
        return exception;
    }

    private static YamlSequenceNode CreateSequenceChain(int collectionCount, out YamlSequenceNode leaf)
    {
        Assert.InRange(collectionCount, 1, int.MaxValue);

        leaf = new YamlSequenceNode();
        var root = leaf;
        for (var i = 1; i < collectionCount; i++)
        {
            root = new YamlSequenceNode { root };
        }

        return root;
    }

    private static string NestedDocument(int depth, string kind, bool anchored = false)
    {
        if (kind is not ("mapping" or "sequence" or "mixed"))
        {
            throw new ArgumentException("Unknown collection kind.", nameof(kind));
        }

        var yaml = new StringBuilder(anchored
            ? "file_format: \"1.2\"\nvendor: &shared "
            : "file_format: \"1.2\"\nvendor: ");
        var endings = new Stack<char>();
        for (var i = 0; i < depth; i++)
        {
            var mapping = kind == "mapping" || (kind == "mixed" && i % 2 == 0);
            yaml.Append(mapping ? "{child: " : "[");
            endings.Push(mapping ? '}' : ']');
        }

        yaml.Append('1');
        while (endings.Count > 0)
        {
            yaml.Append(endings.Pop());
        }

        return yaml.ToString();
    }

    private static string NestedBlockDocument(int depth, string kind)
    {
        var entry = kind switch
        {
            "mapping" => "child:",
            "sequence" => "-",
            _ => throw new ArgumentException("Unknown collection kind.", nameof(kind)),
        };

        var yaml = new StringBuilder("file_format: \"1.2\"\nvendor:\n");
        for (var i = 1; i <= depth; i++)
        {
            yaml.Append(' ', i * 2).Append(entry);
            if (i == depth)
            {
                yaml.Append(" 1");
            }

            yaml.Append('\n');
        }

        return yaml.ToString();
    }

    private static string AliasedDocument(int depth, bool reverse)
    {
        var yaml = new StringBuilder("file_format: \"1.2\"\n");
        for (var i = 0; i < depth; i++)
        {
            var index = reverse ? depth - i - 1 : i;
            var name = "node" + index.ToString(CultureInfo.InvariantCulture);
            yaml.Append(name).Append(": &").Append(name).Append(" {");
            if (index > 0)
            {
                yaml.Append("child: *node").Append((index - 1).ToString(CultureInfo.InvariantCulture));
            }

            yaml.AppendLine("}");
        }

        return yaml.ToString();
    }

    private static DeclarativeConfigurationDocument Read(string yaml)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        return DeclarativeConfigurationReader.Read(new FilePath(factory.CreateYamlFile(yaml)));
    }

    private sealed class CollectionStartLimitedParser(IParser innerParser, int maximumCollectionStarts) : IParser
    {
        public ParsingEvent? Current => innerParser.Current;

        public int CollectionStarts { get; private set; }

        public bool MoveNext()
        {
            Assert.True(
                this.CollectionStarts < maximumCollectionStarts,
                "The parser advanced past the first collection exceeding the depth limit.");

            var moved = innerParser.MoveNext();
            if (moved && this.Current is MappingStart or SequenceStart)
            {
                this.CollectionStarts++;
            }

            return moved;
        }
    }
}
