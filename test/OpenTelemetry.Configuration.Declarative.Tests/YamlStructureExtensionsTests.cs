// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class YamlStructureExtensionsTests
{
    [Theory]
    [InlineData("!!str key")]
    [InlineData("! key")]
    public void EnsureUniqueStringKeys_EquivalentTaggedKey_Throws(string duplicateKey)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader($"key: first\n{duplicateKey}: second"));
        var mapping = Assert.IsType<YamlMappingNode>(stream.Documents[0].RootNode);

        Assert.Throws<DeclarativeConfigurationException>(() =>
            mapping.EnsureUniqueStringKeys("<root>"));
    }

    [Theory]
    [InlineData("~")]
    [InlineData("true")]
    [InlineData("42")]
    [InlineData("3.14")]
    public void EnsureUniqueStringKeys_NonStringKey_Throws(string key)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader($"{key}: value"));
        var mapping = Assert.IsType<YamlMappingNode>(stream.Documents[0].RootNode);

        Assert.Throws<DeclarativeConfigurationException>(() =>
            mapping.EnsureUniqueStringKeys("<root>"));
    }

    // The resolved spelling is returned, not the authored one, so a caller keying a dictionary by
    // these strings agrees with the duplicate detection performed here. The value node is returned
    // alongside it so a caller never has to re-zip the result against the mapping's children.
    [Fact]
    public void EnsureUniqueStringKeys_ReturnsResolvedKeysWithValuesInDocumentOrder()
    {
        var stream = new YamlStream();
        stream.Load(new StringReader("beta: 1\n!!str alpha: 2\n\"gamma\": 3"));
        var mapping = Assert.IsType<YamlMappingNode>(stream.Documents[0].RootNode);

        var entries = mapping.EnsureUniqueStringKeys("<root>");

        Assert.Equal(["beta", "alpha", "gamma"], entries.Select(e => e.Key));
        Assert.Equal(["1", "2", "3"], entries.Select(e => ((YamlScalarNode)e.Value).Value));
    }

    [Fact]
    public void EnsureCoreCollectionTag_ScalarNode_ThrowsArgumentException()
    {
        var scalar = new YamlScalarNode("value");
        Assert.Throws<ArgumentException>(() => scalar.EnsureCoreCollectionTag("context"));
    }

    [Fact]
    public void EnsureNoUnrecognizedProperties_NonScalarKey_Throws()
    {
        var mapping = new YamlMappingNode
        {
            { new YamlSequenceNode("a", "b"), new YamlScalarNode("value") },
        };

        Assert.Throws<DeclarativeConfigurationException>(() =>
            mapping.EnsureNoUnrecognizedProperties("root", []));
    }
}
