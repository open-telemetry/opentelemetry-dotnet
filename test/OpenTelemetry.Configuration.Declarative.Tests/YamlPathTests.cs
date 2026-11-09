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
    public void Describe_WithPosition_IncludesLineAndColumn() =>
        Assert.Equal(
            "'tracer_provider.processors' (line 3, column 5)",
            YamlPath.Describe("tracer_provider.processors", new ConfigValuePosition(3, 5)));

    [Fact]
    public void Describe_WithoutPosition_QuotesPathOnly() =>
        Assert.Equal("'tracer_provider.processors'", YamlPath.Describe("tracer_provider.processors", ConfigValuePosition.Unknown));
}
