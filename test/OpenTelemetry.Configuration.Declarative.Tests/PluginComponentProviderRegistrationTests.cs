// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class PluginComponentProviderRegistrationTests
{
    [Fact]
    public void AddPluginComponentProviderOnBuilder_RegistersProviderAndReturnsBuilder()
    {
        var services = new ServiceCollection();
        IOpenTelemetryBuilder builder = new TestOpenTelemetryBuilder(services);

        var returned = builder.AddPluginComponentProvider("always_on", new TestComponentProvider());

        Assert.Same(builder, returned);
        using var serviceProvider = services.BuildServiceProvider();
        var component = serviceProvider
            .GetRequiredService<PluginComponentProviderRegistry>()
            .Create<TestComponent>("always_on", ConfigProperties.Empty);
        Assert.NotNull(component);
    }

    [Fact]
    public void AddPluginComponentProviderByTypeOnBuilder_RegistersProviderAndReturnsBuilder()
    {
        var services = new ServiceCollection();
        IOpenTelemetryBuilder builder = new TestOpenTelemetryBuilder(services);

        var returned = builder
            .AddPluginComponentProvider<TestComponent, FixedNameTestComponentProvider>(
                FixedNameTestComponentProvider.FixedName);

        Assert.Same(builder, returned);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(FixedNameTestComponentProvider));
    }

    [Fact]
    public void AddPluginComponentProviderOnBuilder_NullBuilder_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => OpenTelemetryBuilderPluginComponentProviderExtensions.AddPluginComponentProvider(
                null!,
                "always_on",
                new TestComponentProvider()));

        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void AddPluginComponentProviderByTypeOnBuilder_NullBuilder_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => OpenTelemetryBuilderPluginComponentProviderExtensions
                .AddPluginComponentProvider<TestComponent, FixedNameTestComponentProvider>(
                    null!,
                    FixedNameTestComponentProvider.FixedName));

        Assert.Equal("builder", exception.ParamName);
    }

    [Fact]
    public void AddPluginComponentProvider_RegistersOneDescriptorAndReturnsServices()
    {
        var services = new ServiceCollection();

        var returned = services.AddPluginComponentProvider("always_on", new TestComponentProvider());

        Assert.Same(services, returned);
        var descriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IPluginComponentProviderRegistration));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddPluginComponentProvider_SameProviderClassDifferentNames_BothSurvive()
    {
        var services = new ServiceCollection();
        var provider = new TestComponentProvider();

        services.AddPluginComponentProvider("always_on", provider);
        services.AddPluginComponentProvider("always_off", provider);

        using var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<PluginComponentProviderRegistry>();

        Assert.NotNull(registry.Create<TestComponent>("always_on", ConfigProperties.Empty));
        Assert.NotNull(registry.Create<TestComponent>("always_off", ConfigProperties.Empty));
    }

    [Fact]
    public void AddPluginComponentProvider_SameComponentTypeAndName_Throws()
    {
        var services = new ServiceCollection();
        services.AddPluginComponentProvider("always_on", new TestComponentProvider());

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddPluginComponentProvider("always_on", new TestComponentProvider()));

        Assert.Contains("always_on", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestComponent), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestComponentProvider), exception.Message, StringComparison.Ordinal);
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IPluginComponentProviderRegistration));
    }

    [Fact]
    public void AddPluginComponentProviderByType_DuplicateIsRejectedAtRegistration()
    {
        var services = new ServiceCollection();
        services.AddPluginComponentProvider<TestComponent, FixedNameTestComponentProvider>("same");

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddPluginComponentProvider<TestComponent, FixedNameTestComponentProvider>("same"));

        Assert.Contains("same", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestComponent), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(FixedNameTestComponentProvider), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddPluginComponentProvider_InstanceAndTypeDuplicateIsRejectedAtRegistration()
    {
        var services = new ServiceCollection();
        services.AddPluginComponentProvider("same", new TestComponentProvider());

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddPluginComponentProvider<TestComponent, FixedNameTestComponentProvider>("same"));

        Assert.Contains(nameof(TestComponentProvider), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(FixedNameTestComponentProvider), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddPluginComponentProvider_SameNameUnderDifferentComponentTypes_BothRegister()
    {
        var services = new ServiceCollection();

        services.AddPluginComponentProvider("shared_name", new TestComponentProvider());
        services.AddPluginComponentProvider("shared_name", new OtherTestComponentProvider());

        using var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<PluginComponentProviderRegistry>();

        Assert.NotNull(registry.Create<TestComponent>("shared_name", ConfigProperties.Empty));
        Assert.NotNull(registry.Create<OtherTestComponent>("shared_name", ConfigProperties.Empty));
    }

    [Fact]
    public void AddPluginComponentProvider_NullServices_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => PluginComponentProviderServiceCollectionExtensions.AddPluginComponentProvider(
                null!,
                "always_on",
                new TestComponentProvider()));

        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void AddPluginComponentProvider_NullProvider_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new ServiceCollection()
                .AddPluginComponentProvider<TestComponent>("always_on", null!));

        Assert.Equal("provider", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddPluginComponentProvider_InvalidName_Throws(string? name)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(
            () => new ServiceCollection()
                .AddPluginComponentProvider(name!, new TestComponentProvider()));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void AddPluginComponentProviderByType_NullServices_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => PluginComponentProviderServiceCollectionExtensions
                .AddPluginComponentProvider<TestComponent, FixedNameTestComponentProvider>(
                    null!,
                    FixedNameTestComponentProvider.FixedName));

        Assert.Equal("services", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddPluginComponentProviderByType_InvalidName_Throws(string? name)
    {
        var exception = Assert.ThrowsAny<ArgumentException>(
            () => new ServiceCollection()
                .AddPluginComponentProvider<TestComponent, FixedNameTestComponentProvider>(name!));

        Assert.Equal("name", exception.ParamName);
    }

    [Fact]
    public void AddPluginComponentProviderByType_ProviderIsConstructedWithItsDependencies()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TestDependency>();
        services.AddPluginComponentProvider<TestComponent, InjectedTestComponentProvider>("injected");

        using var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<PluginComponentProviderRegistry>();

        Assert.NotNull(registry.Create<TestComponent>("injected", ConfigProperties.Empty));
    }

    [Fact]
    public void AddPluginComponentProviderByType_SameProviderUnderDifferentNamesUsesOneProviderRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TestDependency>();
        services.AddPluginComponentProvider<TestComponent, InjectedTestComponentProvider>("first");
        services.AddPluginComponentProvider<TestComponent, InjectedTestComponentProvider>("second");

        Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(InjectedTestComponentProvider));

        using var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<PluginComponentProviderRegistry>();

        Assert.NotNull(registry.Create<TestComponent>("first", ConfigProperties.Empty));
        Assert.NotNull(registry.Create<TestComponent>("second", ConfigProperties.Empty));
    }

    private sealed class TestOpenTelemetryBuilder(IServiceCollection services) : IOpenTelemetryBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}
