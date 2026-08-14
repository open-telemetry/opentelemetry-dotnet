// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class YamlNodeReaderTests
{
    [Fact]
    public void GetScalarString_NullScalarValue_ReturnsNull()
    {
        // Style is set explicitly because that is what the parser produces. An empty or absent
        // value in the document arrives as ScalarStyle.Plain, which is what makes it YAML null.
        var scalar = new YamlScalarNode(null) { Style = ScalarStyle.Plain };
        Assert.Null(scalar.GetScalarString());
    }

    [Fact]
    public void GetScalarString_DefaultStyleScalar_IsNotTypeResolved()
    {
        // ScalarStyle.Any is the enum default, not a style the parser ever assigns, so a node that
        // never declared itself plain must not be implicitly type-resolved. Defaulting to string is
        // the safe direction: it cannot turn "true" or "1.0" into a boolean or a float.
        var scalar = new YamlScalarNode(null);

        Assert.Equal(ScalarStyle.Any, scalar.Style);
        Assert.Equal(string.Empty, scalar.GetScalarString());
    }

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
    public void EnsureUniqueStringKeys_NonStringKey_Throws(string key)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader($"{key}: value"));
        var mapping = Assert.IsType<YamlMappingNode>(stream.Documents[0].RootNode);

        Assert.Throws<DeclarativeConfigurationException>(() =>
            mapping.EnsureUniqueStringKeys("<root>"));
    }
}
