// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace OpenTelemetry.Internal.Tests;

public class DelegatingOptionsFactoryTests
{
    [Fact]
    public void DelegatingOptionsFactory_FactoryFunc_IsUsedToCreateInstance()
    {
        var factoryFuncInvoked = false;
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.RegisterOptionsFactory(_ =>
        {
            factoryFuncInvoked = true;
            return new TestOptions { Value = "from-factory-func" };
        });

        using var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();
        Assert.IsType<DelegatingOptionsFactory<TestOptions>>(factory);

        var options = factory.Create(Options.DefaultName);

        Assert.True(factoryFuncInvoked);
        Assert.Equal("from-factory-func", options.Value);
    }

    [Fact]
    public void DelegatingOptionsFactory_FactoryFunc_ReceivesIConfigurationFromDI()
    {
        // Verifies that the IConfiguration registered in DI is passed to the factory func.
        // This would fail with standard OptionsFactory<T>, which does not pass IConfiguration.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("TestKey", "from-config")])
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.RegisterOptionsFactory(c => new TestOptions { Value = c["TestKey"] });

        using var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();
        Assert.IsType<DelegatingOptionsFactory<TestOptions>>(factory);

        var options = factory.Create(Options.DefaultName);

        Assert.Equal("from-config", options.Value);
    }

    [Fact]
    public void DelegatingOptionsFactory_FactoryFunc_ReceivesOptionsName()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.RegisterOptionsFactory((sp, config, name) => new TestOptions { Value = name });

        using var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();
        Assert.IsType<DelegatingOptionsFactory<TestOptions>>(factory);

        Assert.Equal("customName", factory.Create("customName").Value);
        Assert.Equal(Options.DefaultName, factory.Create(Options.DefaultName).Value);
    }

    [Fact]
    public void DelegatingOptionsFactory_Configure_OverridesFactoryFunc()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.RegisterOptionsFactory(_ => new TestOptions { Value = "factory" });
        services.Configure<TestOptions>(opts => opts.Value = "configure");

        using var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();
        Assert.IsType<DelegatingOptionsFactory<TestOptions>>(factory);

        Assert.Equal("configure", factory.Create(Options.DefaultName).Value);
    }

    [Fact]
    public void DelegatingOptionsFactory_NamedConfigure_AppliesOnlyToMatchingName()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.RegisterOptionsFactory(_ => new TestOptions());
        services.Configure<TestOptions>("alice", opts => opts.Value = "alice-configured");

        using var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();
        Assert.IsType<DelegatingOptionsFactory<TestOptions>>(factory);

        Assert.Null(factory.Create(Options.DefaultName).Value);
        Assert.Equal("alice-configured", factory.Create("alice").Value);
        Assert.Null(factory.Create("bob").Value);
    }

    [Fact]
    public void DelegatingOptionsFactory_ConfigureAll_AppliesToEveryName()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.RegisterOptionsFactory(_ => new TestOptions());
        services.ConfigureAll<TestOptions>(opts => opts.Value = "all-configured");

        using var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();
        Assert.IsType<DelegatingOptionsFactory<TestOptions>>(factory);

        Assert.Equal("all-configured", factory.Create(Options.DefaultName).Value);
        Assert.Equal("all-configured", factory.Create("anyName").Value);
    }

    [Fact]
    public void DelegatingOptionsFactory_PostConfigure_RunsAfterConfigure()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.RegisterOptionsFactory(_ => new TestOptions());
        services.Configure<TestOptions>(opts => opts.Value = "configure");
        services.PostConfigure<TestOptions>(opts => opts.Value = "postconfigure");

        using var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();
        Assert.IsType<DelegatingOptionsFactory<TestOptions>>(factory);

        Assert.Equal("postconfigure", factory.Create(Options.DefaultName).Value);
    }

    [Fact]
    public void DelegatingOptionsFactory_PostConfigureAll_AppliesToEveryName()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.RegisterOptionsFactory(_ => new TestOptions());
        services.PostConfigureAll<TestOptions>(opts => opts.Value = "post-all");

        using var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();
        Assert.IsType<DelegatingOptionsFactory<TestOptions>>(factory);

        Assert.Equal("post-all", factory.Create(Options.DefaultName).Value);
        Assert.Equal("post-all", factory.Create("anyName").Value);
    }

    [Fact]
    public void DelegatingOptionsFactory_PlainConfigure_SkippedForNamedInstance()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.RegisterOptionsFactory(_ => new TestOptions());
        services.Configure<TestOptions>(opts => opts.Value = "default-only");

        using var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();
        Assert.IsType<DelegatingOptionsFactory<TestOptions>>(factory);

        Assert.Equal("default-only", factory.Create(Options.DefaultName).Value);
        Assert.Null(factory.Create("myName").Value);
    }

    [Fact]
    public void DelegatingOptionsFactory_Validation_ThrowsOnFailure()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.RegisterOptionsFactory(_ => new TestOptions());
        services.AddSingleton<IValidateOptions<TestOptions>>(new AlwaysFailValidator());

        using var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();
        Assert.IsType<DelegatingOptionsFactory<TestOptions>>(factory);

        Assert.Throws<OptionsValidationException>(() => factory.Create(Options.DefaultName));
    }

    [Fact]
    public void DelegatingOptionsFactory_MultipleValidators_AllFailuresCollected()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.RegisterOptionsFactory(_ => new TestOptions());
        services.AddSingleton<IValidateOptions<TestOptions>>(new MessageValidator("error-1"));
        services.AddSingleton<IValidateOptions<TestOptions>>(new MessageValidator("error-2"));

        using var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();
        Assert.IsType<DelegatingOptionsFactory<TestOptions>>(factory);

        var ex = Assert.Throws<OptionsValidationException>(() => factory.Create(Options.DefaultName));
        Assert.Contains("error-1", ex.Failures);
        Assert.Contains("error-2", ex.Failures);
    }

    [Fact]
    public void DelegatingOptionsFactory_TryAddSingleton_FirstRegistrationWins()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IOptionsFactory<TestOptions>>(new CustomOptionsFactory());

        // Uses TryAddSingleton, so this registration should be ignored since an
        // IOptionsFactory<TestOptions> is already registered.
        services.RegisterOptionsFactory(_ => new TestOptions());

        using var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();

        Assert.IsType<CustomOptionsFactory>(factory);
    }

    [Fact]
    public void DelegatingOptionsFactory_FactoryThrows_PropagatesException()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.RegisterOptionsFactory<TestOptions>(_ => throw new InvalidOperationException("Factory error"));

        using var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();
        Assert.IsType<DelegatingOptionsFactory<TestOptions>>(factory);

        var ex = Assert.Throws<InvalidOperationException>(() => factory.Create(Options.DefaultName));
        Assert.Equal("Factory error", ex.Message);
    }

    [Fact]
    public void DelegatingOptionsFactory_FullPipeline_OrderIsCorrect()
    {
        // Verifies the complete priority order: factory → Configure → PostConfigure → Validate
        var order = new List<string>();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.RegisterOptionsFactory(_ =>
        {
            order.Add("factory");
            return new TestOptions();
        });
        services.Configure<TestOptions>(_ => order.Add("configure"));
        services.PostConfigure<TestOptions>(_ => order.Add("postconfigure"));
        services.AddSingleton<IValidateOptions<TestOptions>>(new OrderTrackingValidator(order));

        using var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();
        Assert.IsType<DelegatingOptionsFactory<TestOptions>>(factory);

        factory.Create(Options.DefaultName);

        static string[] GetExpectedOrder() => ["factory", "configure", "postconfigure", "validate"];
        Assert.Equal(GetExpectedOrder(), order);
    }

    [Fact]
    public void DelegatingOptionsFactory_NamedConfigure_WithValidation_ValidatesAfterConfigure()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.RegisterOptionsFactory(_ => new TestOptions());
        services.Configure<TestOptions>("alice", opts => opts.Value = "alice-configured");
        services.AddSingleton<IValidateOptions<TestOptions>>(new RequireValueValidator());

        using var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IOptionsFactory<TestOptions>>();
        Assert.IsType<DelegatingOptionsFactory<TestOptions>>(factory);

        var options = factory.Create("alice");
        Assert.Equal("alice-configured", options.Value);

        // Default instance without Configure should fail validation
        Assert.Throws<OptionsValidationException>(() => factory.Create(Options.DefaultName));
    }

    private sealed class TestOptions
    {
        public string? Value { get; set; }
    }

    private sealed class AlwaysFailValidator : IValidateOptions<TestOptions>
    {
        public ValidateOptionsResult Validate(string? name, TestOptions options)
            => ValidateOptionsResult.Fail("Forced failure for testing.");
    }

    private sealed class MessageValidator : IValidateOptions<TestOptions>
    {
        private readonly string message;

        public MessageValidator(string message)
        {
            this.message = message;
        }

        public ValidateOptionsResult Validate(string? name, TestOptions options)
            => ValidateOptionsResult.Fail(this.message);
    }

    private sealed class OrderTrackingValidator : IValidateOptions<TestOptions>
    {
        private readonly List<string> order;

        public OrderTrackingValidator(List<string> order)
        {
            this.order = order;
        }

        public ValidateOptionsResult Validate(string? name, TestOptions options)
        {
            this.order.Add("validate");
            return ValidateOptionsResult.Success;
        }
    }

    private sealed class RequireValueValidator : IValidateOptions<TestOptions>
    {
        public ValidateOptionsResult Validate(string? name, TestOptions options) =>
            string.IsNullOrEmpty(options.Value)
                ? ValidateOptionsResult.Fail($"Value is required for options named '{name}'")
                : ValidateOptionsResult.Success;
    }

    private sealed class CustomOptionsFactory : IOptionsFactory<TestOptions>
    {
        public TestOptions Create(string name) => new();
    }
}
