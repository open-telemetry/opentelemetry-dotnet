// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable OTEL1006

using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class DeclarativeConfigurationExtensionTests
{
    [Fact]
    public void AddOpenTelemetryDeclarativeConfiguration_AppendsSourceAfterExisting()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(); // existing source at index 0
        builder.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path); // appended after

        var config = builder.Build();

        // In-memory does not set OTEL_SDK_DISABLED, so only YAML contributes here.
        Assert.Equal("true", config[OtelEnvironmentVariables.SdkDisabled]);
    }

    [Theory]
    [InlineData(true, "false")]
    [InlineData(false, "true")]
    public void AddOpenTelemetryDeclarativeConfiguration_Precedence_DependsOnSourceOrder(bool sourceAddedAfterYaml, string expected)
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var builder = new ConfigurationBuilder();

        if (sourceAddedAfterYaml)
        {
            builder.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path);
            builder.AddInMemoryCollection(new Dictionary<string, string?> { [OtelEnvironmentVariables.SdkDisabled] = "false" });
        }
        else
        {
            builder.AddInMemoryCollection(new Dictionary<string, string?> { [OtelEnvironmentVariables.SdkDisabled] = "false" });
            builder.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path);
        }

        var config = builder.Build();

        Assert.Equal(expected, config[OtelEnvironmentVariables.SdkDisabled]);
    }

    [Fact]
    public void AddOpenTelemetryDeclarativeConfiguration_Parameterless_NoEnvVar_IsNoOp()
    {
        using var envScope = EnvironmentVariableScope.Create(OtelEnvironmentVariables.ConfigFile, null);

        var builder = new ConfigurationBuilder();
        builder.AddOpenTelemetryDeclarativeConfiguration();

        Assert.Empty(builder.Sources);
    }

    [Fact]
    public void AddOpenTelemetryDeclarativeConfiguration_Parameterless_WhitespaceEnvVar_IsNoOp()
    {
        using var envScope = EnvironmentVariableScope.Create(OtelEnvironmentVariables.ConfigFile, "   ");

        var builder = new ConfigurationBuilder();
        builder.AddOpenTelemetryDeclarativeConfiguration();

        Assert.Empty(builder.Sources);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddOpenTelemetryDeclarativeConfiguration_WhitespaceFilePath_Throws(string filePath)
    {
        var builder = new ConfigurationBuilder();
        Assert.ThrowsAny<ArgumentException>(() => builder.AddOpenTelemetryDeclarativeConfiguration(filePath));
    }

    [Fact]
    public void AddOpenTelemetryDeclarativeConfiguration_Parameterless_ReadsEnvVar()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        using var envScope = EnvironmentVariableScope.Create(OtelEnvironmentVariables.ConfigFile, yamlFile.Path);

        var config = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration()
            .Build();

        Assert.Equal("true", config[OtelEnvironmentVariables.SdkDisabled]);
    }

    [Fact]
    public void UseDeclarativeConfiguration_WithConfigurationManager_InsertsInPlace()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        using var configManager = new ConfigurationManager();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configManager);

        new TestOpenTelemetryBuilder(services).UseDeclarativeConfiguration(yamlFile.Path);

        // IConfiguration instance must still be the same ConfigurationManager (in-place insert)
        var registered = ResolveConfiguration(services);
        Assert.Same(configManager, registered);
        Assert.Equal("true", registered[OtelEnvironmentVariables.SdkDisabled]);
    }

    [Theory]
    [InlineData(false, "true")]
    [InlineData(true, "false")]
    public void UseDeclarativeConfiguration_WithConfigurationManager_Precedence_DependsOnSourceOrder(bool sourceAddedAfterYaml, string expected)
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        using var configManager = new ConfigurationManager();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configManager);

        if (!sourceAddedAfterYaml)
        {
            configManager.AddInMemoryCollection(new Dictionary<string, string?> { [OtelEnvironmentVariables.SdkDisabled] = "false" });
        }

        new TestOpenTelemetryBuilder(services).UseDeclarativeConfiguration(yamlFile.Path);

        if (sourceAddedAfterYaml)
        {
            configManager.AddInMemoryCollection(new Dictionary<string, string?> { [OtelEnvironmentVariables.SdkDisabled] = "false" });
        }

        var config = ResolveConfiguration(services);
        Assert.Equal(expected, config[OtelEnvironmentVariables.SdkDisabled]);
    }

    [Fact]
    public void UseDeclarativeConfiguration_ClassicHost_ChainsExistingConfig()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        // Classic scenario: IConfiguration is a ConfigurationRoot (not ConfigurationManager)
        var existingConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["some-app-key"] = "app-value" })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(existingConfig);

        new TestOpenTelemetryBuilder(services).UseDeclarativeConfiguration(yamlFile.Path);

        var config = ResolveConfiguration(services);

        // IConfiguration was replaced with a new manager
        Assert.NotSame(existingConfig, config);

        // Existing keys still resolve via the chained config
        Assert.Equal("app-value", config["some-app-key"]);

        // YAML key also resolves
        Assert.Equal("true", config[OtelEnvironmentVariables.SdkDisabled]);
    }

    [Fact]
    public void UseDeclarativeConfiguration_Parameterless_NoEnvVar_IsNoOp()
    {
        using var envScope = EnvironmentVariableScope.Create(OtelEnvironmentVariables.ConfigFile, null);

        var services = new ServiceCollection();
        new TestOpenTelemetryBuilder(services).UseDeclarativeConfiguration();

        // No IConfiguration registration should have been added
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IConfiguration));
    }

    [Fact]
    public void UseDeclarativeConfiguration_Parameterless_WhitespaceEnvVar_IsNoOp()
    {
        using var envScope = EnvironmentVariableScope.Create(OtelEnvironmentVariables.ConfigFile, "   ");

        var services = new ServiceCollection();
        new TestOpenTelemetryBuilder(services).UseDeclarativeConfiguration();

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IConfiguration));
    }

    [Fact]
    public void UseDeclarativeConfiguration_Parameterless_ReadsEnvVar()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        using var envScope = EnvironmentVariableScope.Create(OtelEnvironmentVariables.ConfigFile, yamlFile.Path);

        using var configManager = new ConfigurationManager();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configManager);

        new TestOpenTelemetryBuilder(services).UseDeclarativeConfiguration();

        var config = ResolveConfiguration(services);
        Assert.Equal("true", config[OtelEnvironmentVariables.SdkDisabled]);
    }

    [Fact]
    public void AddOpenTelemetryDeclarativeConfiguration_CalledTwiceWithSamePath_InsertsSourceOnce()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var builder = new ConfigurationBuilder();
        builder.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path);
        builder.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path); // second call must be no-op

        Assert.Single(builder.Sources, s => s is DeclarativeConfigurationSource);
    }

    [Fact]
    public void AddOpenTelemetryDeclarativeConfiguration_RelativeAndAbsolutePath_InsertsSourceOnce()
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        var absolutePath = factory.CreateDeclarativeYaml(disabled: true);
        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(factory.TempDirectory);
            var relativePath = Path.GetFileName(absolutePath);

            var builder = new ConfigurationBuilder();
            builder.AddOpenTelemetryDeclarativeConfiguration(absolutePath);
            builder.AddOpenTelemetryDeclarativeConfiguration(relativePath);

            Assert.Single(builder.Sources, s => s is DeclarativeConfigurationSource);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    [Fact]
    public void AddOpenTelemetryDeclarativeConfiguration_DifferentPathCasingOnWindows_InsertsSourceOnce()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var builder = new ConfigurationBuilder();
        builder.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path);
        builder.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path.ToUpperInvariant());

        Assert.Single(builder.Sources, s => s is DeclarativeConfigurationSource);
    }

    [Fact]
    public void AddOpenTelemetryDeclarativeConfiguration_AndUseDeclarativeConfiguration_DoesNotDoubleSource()
    {
        // Verify that calling the IConfigurationBuilder extension directly and then
        // UseDeclarativeConfiguration on the same ConfigurationManager does not insert
        // the source twice. The source-level idempotency guard in the builder extension
        // prevents the second insert regardless of which API was used first.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        using var configManager = new ConfigurationManager();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configManager);

        configManager.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path); // direct insert

        new TestOpenTelemetryBuilder(services).UseDeclarativeConfiguration(yamlFile.Path); // would double-insert without the guard

        // Trigger factory resolution.
        _ = ResolveConfiguration(services);

        Assert.Single(configManager.Sources, s => s is DeclarativeConfigurationSource);
    }

    [Fact]
    public void UseDeclarativeConfiguration_WithImplementationFactory_ReadsYamlAndChainsExistingConfig()
    {
        // Exercises the ImplementationFactory path: IConfiguration registered via a factory
        // delegate (not an instance) resolving to a ConfigurationRoot. UseDeclarativeConfiguration
        // must wrap it in a new ConfigurationManager that chains the original.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var existingConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["some-app-key"] = "app-value" })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(_ => existingConfig); // factory registration

        var builder = new TestOpenTelemetryBuilder(services);
        builder.UseDeclarativeConfiguration(yamlFile.Path);

        var config = ResolveConfiguration(services);

        Assert.Equal("true", config[OtelEnvironmentVariables.SdkDisabled]);
        Assert.Equal("app-value", config["some-app-key"]);
    }

    [Fact]
    public void UseDeclarativeConfiguration_CalledTwiceWithDifferentPaths_SecondCallIsNoOp()
    {
        // When UseDeclarativeConfiguration is called twice on the same IServiceCollection
        // with different file paths, the second call is silently ignored and only the
        // first file path is used. This is the documented behaviour of the idempotency guard.
        using var yamlFile = new DeclarativeYamlTestFileFactory();
        var yamlPath1 = yamlFile.CreateDeclarativeYaml(disabled: true);
        var yamlPath2 = yamlFile.CreateDeclarativeYaml(disabled: false);

        using var configManager = new ConfigurationManager();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configManager);

        var builder = new TestOpenTelemetryBuilder(services);
        builder.UseDeclarativeConfiguration(yamlPath1);
        builder.UseDeclarativeConfiguration(yamlPath2); // second path - must be ignored

        var config = ResolveConfiguration(services);
        Assert.Equal("true", config[OtelEnvironmentVariables.SdkDisabled]); // first file wins
    }

    [Fact]
    public void UseDeclarativeConfiguration_CalledTwice_IsIdempotent()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        using var configManager = new ConfigurationManager();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configManager);

        var builder = new TestOpenTelemetryBuilder(services);
        builder.UseDeclarativeConfiguration(yamlFile.Path);
        builder.UseDeclarativeConfiguration(yamlFile.Path); // second call must be a no-op

        // Only one DeclarativeConfigurationSource should exist in the source list.
        Assert.Single(configManager.Sources.OfType<DeclarativeConfigurationSource>());
    }

    [Fact]
    public void UseDeclarativeConfiguration_CalledTwiceOnFactoryPath_IsIdempotent()
    {
        // Factory (classic ConfigurationRoot) path: second call must not double-wrap.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var existingConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["some-app-key"] = "app-value" })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(existingConfig);

        var builder = new TestOpenTelemetryBuilder(services);
        builder.UseDeclarativeConfiguration(yamlFile.Path);
        builder.UseDeclarativeConfiguration(yamlFile.Path); // second call must be a no-op

        // Exactly one IConfiguration descriptor should exist.
        Assert.Single(services, d => d.ServiceType == typeof(IConfiguration));

        var config = ResolveConfiguration(services);
        Assert.Equal("true", config[OtelEnvironmentVariables.SdkDisabled]);
        Assert.Equal("app-value", config["some-app-key"]);
    }

    [Fact]
    public void UseDeclarativeConfiguration_WithImplementationType_ResolvesAndInsertsSource()
    {
        // ConfigurationManager has a parameterless constructor, so it exercises the
        // ImplementationType code path via ActivatorUtilities.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration, ConfigurationManager>();

        var builder = new TestOpenTelemetryBuilder(services);
        builder.UseDeclarativeConfiguration(yamlFile.Path);

        var config = ResolveConfiguration(services);
        Assert.Equal("true", config[OtelEnvironmentVariables.SdkDisabled]);
    }

    [Fact]
    public void UseDeclarativeConfiguration_Parameterless_NullBuilder_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            OpenTelemetryBuilderDeclarativeConfigurationExtensions.UseDeclarativeConfiguration(null!));

    [Fact]
    public void UseDeclarativeConfiguration_NullBuilder_Throws() =>
        Assert.Throws<ArgumentNullException>(() =>
            OpenTelemetryBuilderDeclarativeConfigurationExtensions.UseDeclarativeConfiguration(null!, "path.yaml"));

    [Fact]
    public void UseDeclarativeConfiguration_NullFilePath_Throws()
    {
        var builder = new TestOpenTelemetryBuilder(new ServiceCollection());

        // ArgumentException.ThrowIfNullOrEmpty throws ArgumentNullException on modern .NET
        // and ArgumentException on .NET Framework, both of which satisfy this assertion.
        Assert.ThrowsAny<ArgumentException>(() => builder.UseDeclarativeConfiguration(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UseDeclarativeConfiguration_WhitespaceFilePath_Throws(string filePath)
    {
        var builder = new TestOpenTelemetryBuilder(new ServiceCollection());
        Assert.ThrowsAny<ArgumentException>(() => builder.UseDeclarativeConfiguration(filePath));
    }

    [Fact]
    public void UseDeclarativeConfiguration_NoExistingIConfiguration_CreatesNewConfigurationWithYaml()
    {
        // When no IConfiguration is registered at all, UseDeclarativeConfiguration must
        // still produce a working IConfiguration backed solely by the YAML source.
        // The NoExistingConfigurationRegistered event fires at registration time and the
        // factory creates a new ConfigurationManager containing only the YAML source.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var services = new ServiceCollection();
        var builder = new TestOpenTelemetryBuilder(services);
        builder.UseDeclarativeConfiguration(yamlFile.Path);

        var config = ResolveConfiguration(services);
        Assert.Equal("true", config[OtelEnvironmentVariables.SdkDisabled]);
    }

    [Fact]
    public void UseDeclarativeConfiguration_SourceAlreadyInExistingConfigurationRoot_DoesNotDoubleInsert()
    {
        // When the existing IConfigurationRoot already contains a DeclarativeConfigurationProvider
        // for the same file path (e.g. added via ConfigureAppConfiguration on HostBuilder),
        // the alreadyRegistered guard inside the factory lambda must skip re-adding the source.
        // YAML values must still be readable through the new wrapping ConfigurationManager.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        // Build a ConfigurationRoot that already has the YAML source.
        var existingRoot = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(existingRoot);

        var builder = new TestOpenTelemetryBuilder(services);
        builder.UseDeclarativeConfiguration(yamlFile.Path); // alreadyRegistered guard should fire

        var config = ResolveConfiguration(services);

        // YAML values must still be readable through the new wrapping ConfigurationManager.
        Assert.Equal("true", config[OtelEnvironmentVariables.SdkDisabled]);

        // The wrapping ConfigurationManager's own Sources must not contain a
        // DeclarativeConfigurationSource; the source lives inside the chained existing root.
        var managerSources = ((IConfigurationBuilder)config).Sources;
        Assert.DoesNotContain(managerSources, s => s is DeclarativeConfigurationSource);
    }

    [Fact]
    public void AddOpenTelemetryDeclarativeConfiguration_RelativePath_SurvivesCwdChange()
    {
        // FilePath.Path stores the absolute path at construction time, so the IConfigurationProvider
        // can still read the file even if the working directory changes before Load() is called.
        using var factory = new DeclarativeYamlTestFileFactory();
        var absolutePath = factory.CreateDeclarativeYaml(disabled: true);
        var originalCwd = Directory.GetCurrentDirectory();

        IConfigurationBuilder builder = new ConfigurationBuilder();
        try
        {
            Directory.SetCurrentDirectory(factory.TempDirectory);
            builder.AddOpenTelemetryDeclarativeConfiguration(Path.GetFileName(absolutePath));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }

        // cwd has been restored; Load runs here. Must succeed because the absolute path was stored.
        var config = builder.Build();
        Assert.Equal("true", config[OtelEnvironmentVariables.SdkDisabled]);
    }

    [Fact]
    public void UseDeclarativeConfiguration_RelativePath_SurvivesCwdChange()
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        var absolutePath = factory.CreateDeclarativeYaml(disabled: true);
        var originalCwd = Directory.GetCurrentDirectory();

        using var configManager = new ConfigurationManager();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configManager);

        try
        {
            Directory.SetCurrentDirectory(factory.TempDirectory);
            new TestOpenTelemetryBuilder(services)
                .UseDeclarativeConfiguration(Path.GetFileName(absolutePath));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }

        var config = ResolveConfiguration(services);
        Assert.Equal("true", config[OtelEnvironmentVariables.SdkDisabled]);
    }

    private static IConfiguration ResolveConfiguration(IServiceCollection services) =>
        services.BuildServiceProvider().GetRequiredService<IConfiguration>();

    private sealed class TestOpenTelemetryBuilder : IOpenTelemetryBuilder
    {
        public TestOpenTelemetryBuilder(IServiceCollection services)
        {
            this.Services = services;
        }

        public IServiceCollection Services { get; }
    }
}
