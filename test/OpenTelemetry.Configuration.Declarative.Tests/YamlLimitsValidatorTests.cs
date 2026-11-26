// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class YamlLimitsValidatorTests
{
    [Theory]
    [InlineData("mapping")]
    [InlineData("sequence")]
    [InlineData("mixed")]
    public void Read_AtMaximumDepth_Loads(string kind)
    {
        var document = Read(NestedDocument(YamlLimitsValidator.MaximumDepth, kind));

        Assert.Contains("vendor", document.Properties.Keys);
    }

    [Theory]
    [InlineData("mapping", 1033)]
    [InlineData("sequence", 137)]
    [InlineData("mixed", 585)]
    public void Read_AboveMaximumDepth_Throws(string kind, int column)
    {
        var exception = AssertDepthExceeded(() =>
            Read(NestedDocument(YamlLimitsValidator.MaximumDepth + 1, kind)));

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
        var document = Read(NestedBlockDocument(YamlLimitsValidator.MaximumDepth, kind));

        Assert.Contains("vendor", document.Properties.Keys);
    }

    [Theory]
    [InlineData("mapping")]
    [InlineData("sequence")]
    public void Read_BlockCollectionsAboveMaximumDepth_Throws(string kind)
    {
        var exception = AssertDepthExceeded(() =>
            Read(NestedBlockDocument(YamlLimitsValidator.MaximumDepth + 1, kind)));

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
            YamlLimitsValidator.MaximumDepth + 2);
        var parser = new YamlDocumentTrackingParser(innerParser);
        var stream = new YamlStream();

        AssertDepthExceeded(() => stream.Load(parser));

        Assert.Equal(YamlLimitsValidator.MaximumDepth + 2, innerParser.CollectionStarts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_AliasChainAtMaximumDepth_Loads(bool reverse)
    {
        var document = Read(AliasedDocument(YamlLimitsValidator.MaximumDepth, reverse));

        Assert.Contains("node0", document.Properties.Keys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_AliasChainAboveMaximumDepth_Throws(bool reverse)
    {
        AssertDepthExceeded(() =>
            Read(AliasedDocument(YamlLimitsValidator.MaximumDepth + 1, reverse)));
    }

    [Fact]
    public void Read_SharedSubtreeAtDeeperAliasWithinLimit_Loads()
    {
        var nested = NestedDocument(YamlLimitsValidator.MaximumDepth - 1, "sequence", anchored: true);

        var document = Read(nested + "\nother: [*shared]\n");

        Assert.Contains("other", document.Properties.Keys);
    }

    [Fact]
    public void Read_SharedSubtreeAtDeeperAlias_Throws()
    {
        var nested = NestedDocument(YamlLimitsValidator.MaximumDepth, "sequence", anchored: true);
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
    public void Read_AliasedParentBasedSamplers_ThrowsBeforeExpandingThem()
    {
        // Twelve levels, each referring to the one below five times, describe 5^12 leaf samplers in under 3 KB.
        var yaml = new StringBuilder("file_format: \"1.2\"\nx_l0: &l0\n  always_off:\n");
        for (var level = 1; level <= 12; level++)
        {
            var name = "l" + level.ToString(CultureInfo.InvariantCulture);
            var below = "*l" + (level - 1).ToString(CultureInfo.InvariantCulture);
            yaml.Append("x_").Append(name).Append(": &").Append(name).Append("\n  parent_based:\n");
            foreach (var key in new[] { "root", "remote_parent_sampled", "remote_parent_not_sampled", "local_parent_sampled", "local_parent_not_sampled" })
            {
                yaml.Append("    ").Append(key).Append(": ").Append(below).Append('\n');
            }
        }

        yaml.Append(
            """
            tracer_provider:
              processors:
                - simple:
                    exporter:
                      console:
              sampler: *l12

            """);

        var exception = AssertNodeCountExceeded(() => Read(yaml.ToString()));

        // Level 5 is the first to exceed the limit, while its parent_based mapping counts its children.
        Assert.Equal(
            "YAML document exceeds the maximum of 5000 nodes (line 34, column 5). " +
            "Each alias counts every node it refers to.",
            exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThrowIfExceeded_Scalars_RespectTheNodeCountLimit(bool exceed)
    {
        // The sequence counts as one node.
        var root = new YamlSequenceNode();
        for (var i = 1; i < YamlLimitsValidator.MaximumNodeCount; i++)
        {
            root.Add(new YamlScalarNode("1"));
        }

        YamlLimitsValidator.ThrowIfExceeded(root);

        if (exceed)
        {
            root.Add(new YamlScalarNode("1"));
            AssertNodeCountExceeded(() => YamlLimitsValidator.ThrowIfExceeded(root));
        }
    }

    [Theory]
    [InlineData(49, true)]
    [InlineData(50, false)]
    public void ThrowIfExceeded_SharedSubtree_CountsEachReference(int references, bool accepted)
    {
        // 1 + 99 scalars = 100 nodes for each reference, plus the root.
        var shared = new YamlSequenceNode();
        for (var i = 0; i < 99; i++)
        {
            shared.Add(new YamlScalarNode("1"));
        }

        var root = new YamlSequenceNode();
        for (var i = 0; i < references; i++)
        {
            root.Add(shared);
        }

        if (accepted)
        {
            YamlLimitsValidator.ThrowIfExceeded(root);
        }
        else
        {
            AssertNodeCountExceeded(() => YamlLimitsValidator.ThrowIfExceeded(root));
        }
    }

    [Fact]
    public void ThrowIfExceeded_CountBeyondInt32_ThrowsWithoutOverflow()
    {
        // 64 levels, each referring to the one below twice, describe 2^64 nodes.
        YamlNode node = new YamlScalarNode("1");
        for (var i = 0; i < 64; i++)
        {
            node = new YamlSequenceNode(node, node);
        }

        AssertNodeCountExceeded(() => YamlLimitsValidator.ThrowIfExceeded(node));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Load_SyntacticNodeCount_IsCheckedWhileParsing(bool exceed)
    {
        // The sequence and its scalars are the document's only nodes.
        var scalars = YamlLimitsValidator.MaximumNodeCount - 1 + (exceed ? 1 : 0);
        var yaml = new StringBuilder("[1");
        yaml.Insert(yaml.Length, ", 1", scalars - 1).Append(']');

        using var reader = new StringReader(yaml.ToString());
        var innerParser = new NodeEventCountingParser(new Parser(reader));
        var stream = new YamlStream();

        if (exceed)
        {
            var exception = AssertNodeCountExceeded(() => stream.Load(new YamlDocumentTrackingParser(innerParser)));

            // The parser stopped at the first node above the limit, before building the node tree.
            Assert.Equal(YamlLimitsValidator.MaximumNodeCount + 1, innerParser.NodeEvents);
            Assert.Empty(stream.Documents);

            // No aliases are expanded while parsing, so the message does not mention them.
            Assert.Equal("YAML document exceeds the maximum of 5000 nodes (line 1, column 14999).", exception.Message);
        }
        else
        {
            stream.Load(new YamlDocumentTrackingParser(innerParser));
            Assert.Equal(YamlLimitsValidator.MaximumNodeCount, innerParser.NodeEvents);
        }
    }

    [Fact]
    public void Read_LongKeyAliasedAtEveryLevel_KeepsPathsShort()
    {
        // One 50,000-character key, used through an alias at each of 120 levels. Building every
        // path in full allocated about 2 GB.
        var yaml = new StringBuilder("file_format: \"1.2\"\nk: &k ")
            .Append('a', 50_000)
            .Append("\nx: ");
        for (var i = 0; i < 120; i++)
        {
            yaml.Append("{*k : ");
        }

        yaml.Append('1').Append('}', 120).Append('\n');

#if NET
        var before = GC.GetAllocatedBytesForCurrentThread();
#endif
        var document = Read(yaml.ToString());
#if NET
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 0, 64 * 1024 * 1024);
#endif

        Assert.Contains("x", document.Properties.Keys);
    }

    [Fact]
    public void Read_MultipleDocuments_ResetsTheSyntacticDepth()
    {
        var yaml = NestedDocument(YamlLimitsValidator.MaximumDepth, "sequence");

        var document = Read(yaml + "\n---\n" + yaml);

        Assert.Contains("vendor", document.Properties.Keys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_MultipleDocuments_AppliesTheNodeCountLimitToEachDocument(bool exceedSecondDocument)
    {
        // Each document has five nodes outside the sequence's scalar items.
        var yaml = new StringBuilder("file_format: \"1.2\"\nvendor: [1");
        yaml.Insert(yaml.Length, ", 1", YamlLimitsValidator.MaximumNodeCount - 6)
            .Append("]\n---\nfile_format: \"1.2\"\nvendor: [1");
        yaml.Insert(yaml.Length, ", 1", YamlLimitsValidator.MaximumNodeCount - 6 + (exceedSecondDocument ? 1 : 0))
            .Append(']');

        if (exceedSecondDocument)
        {
            var exception = AssertNodeCountExceeded(() => Read(yaml.ToString()));

            Assert.Contains("(line 5,", exception.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("vendor", Read(yaml.ToString()).Properties.Keys);
        }
    }

    [Fact]
    public void ThrowIfExceeded_ExtremeResolvedDepth_ThrowsWithoutExhaustingTheCallStack()
    {
        var root = CreateSequenceChain(10_001, out _);

        AssertDepthExceeded(() =>
            YamlLimitsValidator.ThrowIfExceeded(root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThrowIfExceeded_SharedSubtreesAtMaximumDepth_AreNotCycles(bool reverse)
    {
        var shared = CreateSequenceChain(YamlLimitsValidator.MaximumDepth - 2, out _);

        var first = new YamlSequenceNode(shared, shared);
        var second = new YamlSequenceNode(first, shared);
        var root = reverse
            ? new YamlSequenceNode(second, first, shared)
            : new YamlSequenceNode(shared, first, second);

        YamlLimitsValidator.ThrowIfExceeded(root);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThrowIfExceeded_CollectionKeysAndValues_RespectTheDepthLimit(bool exceedKeyDepth)
    {
        var key = CreateSequenceChain(YamlLimitsValidator.MaximumDepth, out var deepestKey);
        var value = CreateSequenceChain(YamlLimitsValidator.MaximumDepth, out var deepestValue);

        var root = new YamlMappingNode { { key, value } };
        YamlLimitsValidator.ThrowIfExceeded(root);

        (exceedKeyDepth ? deepestKey : deepestValue).Add(new YamlSequenceNode());

        AssertDepthExceeded(() =>
            YamlLimitsValidator.ThrowIfExceeded(root));
    }

    [Fact]
    public void ThrowIfExceeded_ScalarRoot_DoesNotThrow() =>
        YamlLimitsValidator.ThrowIfExceeded(new YamlScalarNode("value"));

    [Fact]
    public void ThrowIfExceeded_SelfReferentialMapping_ThrowsCycleDiagnostic()
    {
        var root = new YamlMappingNode();
        root.Add("self", root);

        var exception = Assert.Throws<DeclarativeConfigurationException>(() =>
            YamlLimitsValidator.ThrowIfExceeded(root));

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
            YamlLimitsValidator.ThrowIfExceeded(root));

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

    private static DeclarativeConfigurationException AssertNodeCountExceeded(Action action)
    {
        var exception = Assert.Throws<DeclarativeConfigurationException>(action);
        Assert.Contains("maximum of 5000 nodes", exception.Message, StringComparison.Ordinal);
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

    // Chains a few anchored blocks of nested mappings, so the resolved depth comes through aliases
    // while the resolved node count stays well within the limit. Every anchor is a root property,
    // so the root counts each block and everything it refers to.
    private static string AliasedDocument(int depth, bool reverse)
    {
        const int Blocks = 8;
        var yaml = new StringBuilder("file_format: \"1.2\"\n");
        for (var i = 0; i < Blocks; i++)
        {
            var index = reverse ? Blocks - i - 1 : i;
            var levels = (depth / Blocks) + (index == Blocks - 1 ? depth % Blocks : 0);
            var name = "node" + index.ToString(CultureInfo.InvariantCulture);
            yaml.Append(name).Append(": &").Append(name).Append(' ');

            if (index == 0)
            {
                yaml.Insert(yaml.Length, "{child: ", levels - 1).Append("{}").Append('}', levels - 1);
            }
            else
            {
                yaml.Insert(yaml.Length, "{child: ", levels)
                    .Append("*node").Append((index - 1).ToString(CultureInfo.InvariantCulture))
                    .Append('}', levels);
            }

            yaml.Append('\n');
        }

        return yaml.ToString();
    }

    private static DeclarativeConfigurationDocument Read(string yaml)
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        return DeclarativeConfigurationReader.Read(new FilePath(factory.CreateYamlFile(yaml)));
    }

    private sealed class NodeEventCountingParser(IParser innerParser) : IParser
    {
        public ParsingEvent? Current => innerParser.Current;

        public int NodeEvents { get; private set; }

        public bool MoveNext()
        {
            var moved = innerParser.MoveNext();
            if (moved && this.Current is MappingStart or SequenceStart or Scalar or AnchorAlias)
            {
                this.NodeEvents++;
            }

            return moved;
        }
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
