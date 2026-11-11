// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class BuiltInSamplerProviderTests
{
    [Theory]
    [InlineData("always_off")]
    [InlineData("always_on")]
    [InlineData("always_record")]
    [InlineData("parent_based")]
    [InlineData("trace_id_ratio_based")]
    public void Registry_ContainsTheBuiltInSamplers(string name)
    {
        using var services = BuildServiceProvider();

        Assert.True(services.GetRequiredService<PluginComponentProviderRegistry>().Contains<Sampler>(name), $"Expected the registry to contain the built-in sampler '{name}'.");
    }

    [Theory]
    [InlineData("always_off", typeof(AlwaysOffSampler))]
    [InlineData("always_on", typeof(AlwaysOnSampler))]
    [InlineData("trace_id_ratio_based", typeof(TraceIdRatioBasedSampler))]
    public void Create_ReturnsANewInstanceEachTime(string name, Type expectedType)
    {
        using var services = BuildServiceProvider();
        var registry = services.GetRequiredService<PluginComponentProviderRegistry>();

        var first = registry.Create<Sampler>(name, ConfigProperties.Empty);
        var second = registry.Create<Sampler>(name, ConfigProperties.Empty);

        Assert.IsType(expectedType, first);
        Assert.NotSame(first, second);
    }

    [Theory]
    [InlineData("always_off")]
    [InlineData("always_on")]
    [InlineData("trace_id_ratio_based")]
    public void Create_NullArguments_Throw(string name)
    {
        using var services = BuildServiceProvider();
        var registration = Assert.Single(
            services.GetServices<IPluginComponentProviderRegistration>(),
            registration => registration.ComponentType == typeof(Sampler) && registration.Name == name);

        Assert.Throws<ArgumentNullException>(() => registration.Create(null!, services));
        Assert.Throws<ArgumentNullException>(() => registration.Create(ConfigProperties.Empty, null!));
    }

    [Theory]
    [InlineData("{}", 1)]
    [InlineData("{ratio: }", 1)]
    [InlineData("{ratio: 0}", 0)]
    [InlineData("{ratio: 0.25}", 0.25)]
    [InlineData("{ratio: 1}", 1)]
    public void TraceIdRatioBased_ReadsTheRatioFromItsOwnProperties(string properties, double expected)
    {
        var sampler = CreateRatioSampler(properties);

        Assert.Equal(new TraceIdRatioBasedSampler(expected).Description, sampler.Description);
    }

    [Theory]
    [InlineData("{ratio: high}")]
    [InlineData("{ratio: true}")]
    [InlineData("{ratio: 1.5}")]
    [InlineData("{ratio: -1}")]
    [InlineData("{ratio: .nan}")]
    [InlineData("{ratio: .inf}")]
    public void TraceIdRatioBased_InvalidRatio_Throws(string properties)
    {
        var exception = Assert.Throws<DeclarativeConfigurationException>(() => CreateRatioSampler(properties));

        Assert.Contains("'trace_id_ratio_based.ratio' (line 2, column", exception.Message, StringComparison.Ordinal);
        Assert.Contains("must be a number from 0 to 1", exception.Message, StringComparison.Ordinal);
    }

    private static Sampler CreateRatioSampler(string properties)
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile($"""
            file_format: "1.2"
            x_extension: {properties}

            """);
        var document = DeclarativeConfigurationReader.Read(new FilePath(yamlFile.Path));
        var mapping = document.Properties.GetMapping("x_extension").Value!;

        using var services = BuildServiceProvider();
        return services.GetRequiredService<PluginComponentProviderRegistry>().Create<Sampler>("trace_id_ratio_based", mapping);
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        BuiltInComponentProviders.Register(services);
        return services.BuildServiceProvider();
    }
}
