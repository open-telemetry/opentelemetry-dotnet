// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class YamlPathTests
{
    [Fact]
    public void Child_JoinsParentAndKeyWithDot() =>
        Assert.Equal("tracer_provider.processors", YamlPath.Child(YamlKeys.TracerProvider, YamlKeys.Processors));

    [Fact]
    public void Index_AppendsZeroBasedBracketIndex() =>
        Assert.Equal("tracer_provider.processors[0]", YamlPath.Index(YamlPath.Child(YamlKeys.TracerProvider, YamlKeys.Processors), 0));

    [Fact]
    public void Child_AtMaximumLength_IsNotTruncated()
    {
        var parent = new string('p', YamlPath.MaximumLength - 2);

        Assert.Equal(parent + ".k", YamlPath.Child(parent, "k"));
    }

    [Fact]
    public void Child_AboveMaximumLength_IsTruncatedWithEllipsis()
    {
        var path = YamlPath.Child("parent", new string('k', 1_000_000));

        Assert.Equal(YamlPath.MaximumLength, path.Length);
        Assert.StartsWith("parent.kkk", path, StringComparison.Ordinal);
        Assert.EndsWith(YamlPath.Ellipsis, path, StringComparison.Ordinal);
    }

    [Fact]
    public void ChildAndIndex_OfTruncatedPath_StayTruncated()
    {
        var truncated = YamlPath.Child("parent", new string('k', YamlPath.MaximumLength));

        Assert.Equal(truncated, YamlPath.Child(truncated, "child"));
        Assert.Equal(truncated, YamlPath.Index(truncated, 12));
    }

    [Fact]
    public void Index_AboveMaximumLength_IsTruncatedWithEllipsis()
    {
        var parent = new string('p', YamlPath.MaximumLength - 1);

        Assert.Equal(new string('p', YamlPath.MaximumLength - 3) + YamlPath.Ellipsis, YamlPath.Index(parent, 0));
    }

    [Fact]
    public void Describe_WithPosition_IncludesLineAndColumn() =>
        Assert.Equal(
            "'tracer_provider.processors' (line 3, column 5)",
            YamlPath.Describe("tracer_provider.processors", new ConfigValuePosition(3, 5)));

    [Fact]
    public void Describe_WithoutPosition_QuotesPathOnly() =>
        Assert.Equal("'tracer_provider.processors'", YamlPath.Describe("tracer_provider.processors", ConfigValuePosition.Unknown));
}
