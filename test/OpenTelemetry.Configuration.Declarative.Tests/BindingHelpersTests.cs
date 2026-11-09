// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class BindingHelpersTests
{
    [Fact]
    public void PositionOfValueOrKey_PrefersValuePosition()
    {
        var parent = new ConfigPropertiesBuilder()
            .Add("enabled", ConfigValue.Boolean(true), new ConfigValuePosition(1, 1))
            .Build();

        _ = parent.TryGetValue("enabled", out var value);

        var positioned = value.WithPosition(new ConfigValuePosition(2, 4));

        Assert.Equal(new ConfigValuePosition(2, 4), BindingHelpers.PositionOfValueOrKey(parent, "enabled", positioned));
    }

    [Fact]
    public void PositionOfValueOrKey_FallsBackToKeyWhenValueHasNoPosition()
    {
        var parent = new ConfigPropertiesBuilder()
            .Add("enabled", ConfigValue.Boolean(true), new ConfigValuePosition(4, 7))
            .Build();

        _ = parent.TryGetValue("enabled", out var value);

        Assert.False(value.Position.HasPosition, "Expected value to have no position.");
        Assert.Equal(new ConfigValuePosition(4, 7), BindingHelpers.PositionOfValueOrKey(parent, "enabled", value));
    }

    [Fact]
    public void AsRequiredMapping_NonMapping_ThrowsWithKeyPositionWhenValueHasNone()
    {
        var parent = new ConfigPropertiesBuilder()
            .Add("tracer_provider", ConfigValue.String("nope"), new ConfigValuePosition(2, 1))
            .Build();

        _ = parent.TryGetValue("tracer_provider", out var value);

        var exception = Assert.Throws<DeclarativeConfigurationException>(
            () => BindingHelpers.AsRequiredMapping(parent, "tracer_provider", value, "tracer_provider"));

        Assert.Equal("'tracer_provider' (line 2, column 1) must be a mapping.", exception.Message);
    }

    [Fact]
    public void BindComponent_NullValue_UsesEmptyProperties()
    {
        var component = BindingHelpers.BindComponent(
            ConfigValue.Mapping(new ConfigPropertiesBuilder().AddNull("console").Build()),
            "exporter",
            "span exporter");

        Assert.Equal("console", component.Name);
        Assert.Same(ConfigProperties.Empty, component.Properties);
    }

    [Fact]
    public void BindComponent_UnpositionedMapping_ComputesPositionFromKeyFallback()
    {
        var value = ConfigValue.Mapping(new ConfigPropertiesBuilder().AddNull("console").Build());
        Assert.False(value.Position.HasPosition, "Expected value to have no position.");

        var component = BindingHelpers.BindComponent(
            value,
            "exporter",
            "span exporter",
            new ConfigValuePosition(8, 3));

        Assert.Equal("console", component.Name);
        Assert.Equal("exporter.console", component.Path);
    }

    [Fact]
    public void BindComponent_UnpositionedInvalidValue_ReportsKeyPosition()
    {
        var exception = Assert.Throws<DeclarativeConfigurationException>(
            () => BindingHelpers.BindComponent(
                ConfigValue.String("nope"),
                "exporter",
                "span exporter",
                new ConfigValuePosition(8, 3)));

        Assert.Equal("'exporter' (line 8, column 3) must name exactly one span exporter.", exception.Message);
    }

    [Fact]
    public void BindComponent_NonMappingOrNullProperties_Throws()
    {
        var value = ConfigValue.Mapping(new ConfigPropertiesBuilder().Add("console", "not-a-mapping").Build());

        var exception = Assert.Throws<DeclarativeConfigurationException>(
            () => BindingHelpers.BindComponent(value, "exporter", "span exporter"));

        Assert.Contains("must be a mapping or null.", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'exporter.console'", exception.Message, StringComparison.Ordinal);
    }
}
