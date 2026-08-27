// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class PluginComponentProviderRegistryTests
{
    [Fact]
    public void Constructor_NullServiceProvider_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new PluginComponentProviderRegistry(null!));

        Assert.Equal("serviceProvider", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RegistrationWithoutName_Throws(string? name)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPluginComponentProviderRegistration>(
            new UntrustedPluginComponentProviderRegistration(name, new object()));
        services.AddSingleton<PluginComponentProviderRegistry>();
        using var serviceProvider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            serviceProvider.GetRequiredService<PluginComponentProviderRegistry>);

        Assert.Contains(nameof(UntrustedPluginComponentProviderRegistration), exception.Message, StringComparison.Ordinal);
        Assert.Contains("has no name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_DuplicateRegistration_Throws()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPluginComponentProviderRegistration>(
            new UntrustedPluginComponentProviderRegistration(
                "always_on",
                new object(),
                typeof(TestComponentProvider)));
        services.AddSingleton<IPluginComponentProviderRegistration>(
            new UntrustedPluginComponentProviderRegistration(
                "always_on",
                new object(),
                typeof(FixedNameTestComponentProvider)));
        services.AddSingleton<PluginComponentProviderRegistry>();
        using var serviceProvider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            serviceProvider.GetRequiredService<PluginComponentProviderRegistry>);

        Assert.Contains("always_on", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestComponent), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestComponentProvider), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(FixedNameTestComponentProvider), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_PassesPropertiesAndContainerToProvider()
    {
        var properties = new ConfigPropertiesBuilder().Add("ratio", 0.25).Build();
        using var serviceProvider = BuildServiceProvider(("trace_id_ratio_based", new TestComponentProvider()));

        var registry = serviceProvider.GetRequiredService<PluginComponentProviderRegistry>();
        var component = registry.Create<TestComponent>("trace_id_ratio_based", properties);

        Assert.Same(properties, component.Properties);
        Assert.Same(
            registry,
            component.ServiceProvider.GetRequiredService<PluginComponentProviderRegistry>());
    }

    [Fact]
    public void Create_ReturnsANewComponentOnEveryCall()
    {
        using var serviceProvider = BuildServiceProvider(("always_on", new TestComponentProvider()));
        var registry = serviceProvider.GetRequiredService<PluginComponentProviderRegistry>();

        var first = registry.Create<TestComponent>("always_on", ConfigProperties.Empty);
        var second = registry.Create<TestComponent>("always_on", ConfigProperties.Empty);

        Assert.NotSame(first, second);
    }

    [Fact]
    public void Create_NameMatchingIsOrdinal()
    {
        using var serviceProvider = BuildServiceProvider(("always_on", new TestComponentProvider()));
        var registry = serviceProvider.GetRequiredService<PluginComponentProviderRegistry>();

        Assert.Throws<DeclarativeConfigurationException>(
            () => registry.Create<TestComponent>("Always_On", ConfigProperties.Empty));
    }

    [Fact]
    public void Create_ComponentTypeMatchingIsExact()
    {
        using var serviceProvider = BuildServiceProvider(("always_on", new TestComponentProvider()));
        var registry = serviceProvider.GetRequiredService<PluginComponentProviderRegistry>();

        Assert.Throws<DeclarativeConfigurationException>(
            () => registry.Create<OtherTestComponent>("always_on", ConfigProperties.Empty));
    }

    [Fact]
    public void Create_UnknownName_ThrowsListingTheRegisteredNames()
    {
        using var serviceProvider = BuildServiceProvider(
            ("always_on", new TestComponentProvider()),
            ("trace_id_ratio_based", new TestComponentProvider()));
        var registry = serviceProvider.GetRequiredService<PluginComponentProviderRegistry>();

        var exception = Assert.Throws<DeclarativeConfigurationException>(
            () => registry.Create<TestComponent>("always_upside_down", ConfigProperties.Empty));

        Assert.Contains("always_upside_down", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestComponent), exception.Message, StringComparison.Ordinal);
        Assert.Contains("always_on", exception.Message, StringComparison.Ordinal);
        Assert.Contains("trace_id_ratio_based", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_NoProvidersForTheComponentType_SaysSo()
    {
        var services = new ServiceCollection();
        services.AddPluginComponentProvider("always_on", new OtherTestComponentProvider());
        using var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<PluginComponentProviderRegistry>();

        var exception = Assert.Throws<DeclarativeConfigurationException>(
            () => registry.Create<TestComponent>("always_on", ConfigProperties.Empty));

        Assert.Contains("No component providers are registered", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ProviderThrows_PropagatesTheSameException()
    {
        var thrown = new NotSupportedException("the provider rejected its configuration");
        using var serviceProvider = BuildServiceProvider(
            ("always_on", new ThrowingTestComponentProvider(thrown)));
        var registry = serviceProvider.GetRequiredService<PluginComponentProviderRegistry>();

        var caught = Assert.Throws<NotSupportedException>(
            () => registry.Create<TestComponent>("always_on", ConfigProperties.Empty));

        Assert.Same(thrown, caught);
    }

    [Fact]
    public void Create_ProviderReturnsNull_ThrowsNamingTheProviderAndExpectedType()
    {
        using var serviceProvider = BuildServiceProvider(("always_on", new NullTestComponentProvider()));
        var registry = serviceProvider.GetRequiredService<PluginComponentProviderRegistry>();

        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.Create<TestComponent>("always_on", ConfigProperties.Empty));

        Assert.Contains(nameof(NullTestComponentProvider), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(TestComponent), exception.Message, StringComparison.Ordinal);
        Assert.Contains("always_on", exception.Message, StringComparison.Ordinal);
        Assert.Contains("null", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_UntrustedRegistrationReturnsIncompatibleType_Throws()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPluginComponentProviderRegistration>(
            new UntrustedPluginComponentProviderRegistration("always_on", new OtherTestComponent()));
        services.AddSingleton<PluginComponentProviderRegistry>();
        using var serviceProvider = services.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(
            () => serviceProvider
                .GetRequiredService<PluginComponentProviderRegistry>()
                .Create<TestComponent>("always_on", ConfigProperties.Empty));

        Assert.Contains(nameof(UntrustedPluginComponentProviderRegistration), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(OtherTestComponent), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_NullArguments_Throw()
    {
        using var serviceProvider = BuildServiceProvider(("always_on", new TestComponentProvider()));
        var registry = serviceProvider.GetRequiredService<PluginComponentProviderRegistry>();

        var nullNameException = Assert.Throws<ArgumentNullException>(
            () => registry.Create<TestComponent>(null!, ConfigProperties.Empty));
        var nullPropertiesException = Assert.Throws<ArgumentNullException>(
            () => registry.Create<TestComponent>("always_on", null!));

        Assert.Equal("name", nullNameException.ParamName);
        Assert.Equal("properties", nullPropertiesException.ParamName);
    }

    [Fact]
    public void Constructor_SeesOnlyProvidersRegisteredInItsContainer()
    {
        using var first = BuildServiceProvider(("always_on", new TestComponentProvider()));
        using var second = BuildServiceProvider(("always_off", new TestComponentProvider()));

        var registry = first.GetRequiredService<PluginComponentProviderRegistry>();

        Assert.NotNull(registry.Create<TestComponent>("always_on", ConfigProperties.Empty));
        Assert.Throws<DeclarativeConfigurationException>(
            () => registry.Create<TestComponent>("always_off", ConfigProperties.Empty));
    }

    [Fact]
    public async Task Create_IsSafeToCallConcurrently()
    {
        var properties = new ConfigPropertiesBuilder().Add("ratio", 0.5).Build();
        using var serviceProvider = BuildServiceProvider(
            ("always_on", new TestComponentProvider()),
            ("trace_id_ratio_based", new TestComponentProvider()));
        var registry = serviceProvider.GetRequiredService<PluginComponentProviderRegistry>();

        var names = Enumerable.Range(0, 128)
            .Select(index => index % 2 == 0 ? "always_on" : "trace_id_ratio_based")
            .ToArray();

        var components = await Task.WhenAll(
            names.Select(name => Task.Run(
                () => registry.Create<TestComponent>(name, properties))));

        Assert.Equal(names.Length, components.Length);
        Assert.All(components, component => Assert.Same(properties, component.Properties));
        Assert.Equal(names.Length, components.Distinct().Count());
    }

    [Fact]
    public void Registry_DoesNotOwnComponentsItCreates()
    {
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(PluginComponentProviderRegistry)));

        DisposableTestComponent component;
        var services = new ServiceCollection();
        services.AddPluginComponentProvider("batch", new DisposableTestComponentProvider());

        using (var serviceProvider = services.BuildServiceProvider())
        {
            component = serviceProvider
                .GetRequiredService<PluginComponentProviderRegistry>()
                .Create<DisposableTestComponent>("batch", ConfigProperties.Empty);
        }

        Assert.False(component.Disposed);
    }

    [Fact]
    public void Create_ReceivesProperties()
    {
        var headers = new ConfigPropertiesBuilder()
            .Add("authorization", "secret")
            .Build();
        var expectedTags = new[] { "one", "two" };
        var properties = new ConfigPropertiesBuilder()
            .Add("endpoint", "https://collector.example")
            .Add("enabled", true)
            .Add("timeout", 10)
            .Add("ratio", 0.25)
            .Add("headers", headers)
            .AddScalarList("tags", expectedTags)
            .Build();
        using var serviceProvider = BuildServiceProvider(("custom", new TestComponentProvider()));

        var component = serviceProvider
            .GetRequiredService<PluginComponentProviderRegistry>()
            .Create<TestComponent>("custom", properties);

        Assert.Equal("https://collector.example", component.Properties.GetString("endpoint").Value);
        Assert.True(component.Properties.GetBoolean("enabled").Value);
        Assert.Equal(10, component.Properties.GetInt("timeout").Value);
        Assert.Equal(0.25, component.Properties.GetDouble("ratio").Value);
        Assert.Equal(
            "secret",
            component.Properties.GetProperties("headers").Value!.GetString("authorization").Value);
        Assert.Equal(
            expectedTags,
            component.Properties.GetScalarList<string>("tags").Value);
    }

    private static ServiceProvider BuildServiceProvider(
        params (string Name, PluginComponentProvider<TestComponent> Provider)[] registrations)
    {
        var services = new ServiceCollection();

        foreach (var registration in registrations)
        {
            services.AddPluginComponentProvider(registration.Name, registration.Provider);
        }

        return services.BuildServiceProvider();
    }

    private sealed class UntrustedPluginComponentProviderRegistration : IPluginComponentProviderRegistration
    {
        private readonly object component;

        public UntrustedPluginComponentProviderRegistration(
            string? name,
            object component,
            Type? providerType = null)
        {
            this.Name = name!;
            this.component = component;
            this.ProviderType = providerType ?? this.GetType();
        }

        public Type ComponentType => typeof(TestComponent);

        public string Name { get; }

        public Type ProviderType { get; }

        public object Create(ConfigProperties properties, IServiceProvider serviceProvider) => this.component;
    }
}
