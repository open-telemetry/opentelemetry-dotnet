// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class StrictModeMaskTests
{
    [Fact]
    public void EarlierTracesSampler_IsMasked_CoreDefaultSamplerApplies()
    {
        const string sourceName = "strict-mode.sampler";
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_TRACES_SAMPLER"] = "always_off" })
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .AddSource(sourceName)
            .Build();

        using var source = new ActivitySource(sourceName);

        // The core default is ParentBased(AlwaysOn); a masked "always_off" must not suppress this.
        Assert.NotNull(source.StartActivity("work"));
    }

    [Fact]
    public void EarlierSdkDisabledTrue_DocumentOmitsDisabled_SdkStaysEnabled()
    {
        const string sourceName = "strict-mode.disabled-mask";
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [OtelEnvironmentVariables.SdkDisabled] = "true" })
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .AddSource(sourceName)
            .Build();

        using var source = new ActivitySource(sourceName);
        Assert.NotNull(source.StartActivity("work"));
    }

    [Fact]
    public void EarlierSdkDisabledFalse_DocumentDisablesTrue_DocumentWins()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [OtelEnvironmentVariables.SdkDisabled] = "false" })
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .Build();

        Assert.Equal("NoopTracerProvider", tracerProvider.GetType().Name);
    }

    [Fact]
    public void EarlierOutOfScopeKeys_StayVisible()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [OtelEnvironmentVariables.ConfigFile] = "/some/path.yaml",
                ["OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY"] = "true",
                ["Logging:OpenTelemetry:LogLevel:Default"] = "Warning",
                ["OpenTelemetry:Otlp:Endpoint"] = "http://collector:4318",
                ["some-app-key"] = "app-value",
            })
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        Assert.Equal("/some/path.yaml", config[OtelEnvironmentVariables.ConfigFile]);
        Assert.Equal("true", config["OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY"]);
        Assert.Equal("Warning", config["Logging:OpenTelemetry:LogLevel:Default"]);

        // The OpenTelemetry section is not masked until a binder reads it into SDK settings.
        Assert.Equal("http://collector:4318", config["OpenTelemetry:Otlp:Endpoint"]);
        Assert.Equal("app-value", config["some-app-key"]);
        Assert.Contains(config.GetChildren(), section => section.Key == "Logging");
        Assert.Contains(config.GetSection("Logging").GetChildren(), section => section.Key == "OpenTelemetry");
    }

    [Fact]
    public void ConfigurationManagerInstance_EarlierKeyIsMasked()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        using var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_TRACES_SAMPLER"] = "always_off" });

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddOpenTelemetry().UseDeclarativeConfiguration(yamlFile.Path);

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Null(serviceProvider.GetRequiredService<IConfiguration>()["OTEL_TRACES_SAMPLER"]);
    }

    [Fact]
    public void ConfigurationRootInstance_DescriptorReplaced_EarlierKeyIsMasked()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var root = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_TRACES_SAMPLER"] = "always_off" })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(root);
        services.AddOpenTelemetry().UseDeclarativeConfiguration(yamlFile.Path);

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Null(serviceProvider.GetRequiredService<IConfiguration>()["OTEL_TRACES_SAMPLER"]);
    }

    [Fact]
    public void LegacyHostBuilder_EarlierKeyIsMasked()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        using var host = new HostBuilder()
            .ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_TRACES_SAMPLER"] = "always_off" }))
            .ConfigureServices(s => s.AddOpenTelemetry().UseDeclarativeConfiguration(yamlFile.Path))
            .Build();

        Assert.Null(host.Services.GetRequiredService<IConfiguration>()["OTEL_TRACES_SAMPLER"]);
    }

    [Fact]
    public void BeforeLoad_InScopeKeysReadNull()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var provider = new DeclarativeConfigurationProvider(
            new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path)));

        Assert.True(provider.TryGet("OTEL_TRACES_SAMPLER", out var value), "Expected the in-scope key to be found.");
        Assert.Null(value);
        Assert.False(provider.TryGet("some-app-key", out _), "Expected the out-of-scope key not to be found.");
    }

    [Fact]
    public void EarlierKey_DifferentCasing_IsStillMasked()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["otel_traces_sampler"] = "always_off" })
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        Assert.Null(config["OTEL_TRACES_SAMPLER"]);
    }

    [Fact]
    public void EarlierHierarchicalKey_IsMasked_AndHiddenFromEnumeration()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_FOO:BAR"] = "value" })
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        Assert.Null(config["OTEL_FOO:BAR"]);
        Assert.False(config.GetSection("OTEL_FOO").Exists(), "Expected the masked hierarchical key section not to exist.");
        Assert.DoesNotContain(config.GetChildren(), section => section.Key == "OTEL_FOO");
        Assert.Contains(config.GetChildren(), section => section.Key == OtelEnvironmentVariables.SdkDisabled);
    }

    [Fact]
    public void EarlierReservedSection_IsMasked_AndHiddenFromEnumeration()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenTelemetry:Console:Targets"] = "Debug" })
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        Assert.Null(config["OpenTelemetry:Console:Targets"]);
        Assert.False(config.GetSection("OpenTelemetry:Console").Exists(), "Expected the masked reserved section not to exist.");
        Assert.DoesNotContain(config.GetSection("OpenTelemetry").GetChildren(), section => section.Key == "Console");
    }

    [Fact]
    public void EarlierReservedSection_EnvironmentSpelling_IsAlsoMasked()
    {
        const string envVarName = "OpenTelemetry__Console__Targets";
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        Environment.SetEnvironmentVariable(envVarName, "Debug");
        try
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
                .Build();

            Assert.Null(config["OpenTelemetry:Console:Targets"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
        }
    }

    [Fact]
    public void EarlierAppOwnedOpenTelemetryKeys_StayVisible()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenTelemetry:ServiceName"] = "my-service",
                ["OpenTelemetry:Enabled"] = "true",
                ["OpenTelemetry:ConsoleFoo:Bar"] = "value",
                ["OpenTelemetryFoo:Bar"] = "value",
            })
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        Assert.Equal("my-service", config["OpenTelemetry:ServiceName"]);
        Assert.Equal("true", config["OpenTelemetry:Enabled"]);
        Assert.Equal("value", config["OpenTelemetry:ConsoleFoo:Bar"]);
        Assert.Equal("value", config["OpenTelemetryFoo:Bar"]);
        Assert.Contains(config.GetSection("OpenTelemetry").GetChildren(), section => section.Key == "ServiceName");
    }

    [Fact]
    public void LaterSource_StillOverridesDeclarativeConfiguration()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var config = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .AddInMemoryCollection(new Dictionary<string, string?> { [OtelEnvironmentVariables.SdkDisabled] = "true" })
            .Build();

        Assert.Equal("true", config[OtelEnvironmentVariables.SdkDisabled]);
    }

    [Fact]
    public void LaterHierarchicalKey_IsVisibleAndIncludedInEnumeration()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var config = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_FOO:BAR"] = "value" })
            .Build();

        Assert.Equal("value", config["OTEL_FOO:BAR"]);
        Assert.True(config.GetSection("OTEL_FOO").Exists(), "Expected the later hierarchical key section to exist.");
        Assert.Contains(config.GetChildren(), section => section.Key == "OTEL_FOO");
        Assert.Contains(config.AsEnumerable(), pair => pair.Key == "OTEL_FOO:BAR" && pair.Value == "value");
    }

    [Fact]
    public void OptionsBoundThroughFactory_EarlierValueIsMasked_DefaultApplies()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_BSP_SCHEDULE_DELAY"] = "123456" })
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        IServiceProvider? capturedServices = null;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .ConfigureResource(resource => resource.AddDetector(sp =>
            {
                capturedServices = sp;
                return new EmptyResourceDetector();
            }))
            .Build();

        var options = capturedServices!
            .GetRequiredService<IOptionsMonitor<BatchExportActivityProcessorOptions>>()
            .Get(Options.DefaultName);

        Assert.Equal(5_000, options.ScheduledDelayMilliseconds);
    }

    [Theory]
    [InlineData("OTEL_RESOURCE_ATTRIBUTES", "service.name=from-resource-attributes")]
    [InlineData("OTEL_SERVICE_NAME", "from-service-name")]
    public void HostApplicationBuilder_EarlierServiceNameSetting_IsMasked_HostSuppliesApplicationName(
        string key,
        string value)
    {
        const string ApplicationName = "strict-mode-application";
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ApplicationName = ApplicationName,
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [key] = value });
        builder.AddOpenTelemetry()
            .UseDeclarativeConfiguration(yamlFile.Path)
            .WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();
        var serviceName = Assert.Single(resource.Attributes, attribute => attribute.Key == "service.name");

        Assert.Equal(ApplicationName, Assert.IsType<string>(serviceName.Value));
    }

    [Fact]
    public void HostApplicationBuilder_DocumentServiceName_WinsOverMaskedEarlierSettings()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "from-document" });

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = "service.name=from-resource-attributes",
            ["OTEL_SERVICE_NAME"] = "from-service-name",
        });
        builder.AddOpenTelemetry()
            .UseDeclarativeConfiguration(yamlFile.Path)
            .WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();
        var serviceName = Assert.Single(resource.Attributes, attribute => attribute.Key == "service.name");

        Assert.Equal("from-document", Assert.IsType<string>(serviceName.Value));
    }

    [Fact]
    public void ResourceAttributesList_StillApplies_WhenNotMaskedByEarlierSource()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributesList: "service.name=from-list");

        var config = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .Build();

        var resource = tracerProvider.GetResource();
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "from-list");
    }

    [Fact]
    public void EmptyFile_MasksEverything_SuppliesNothing()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(string.Empty);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [OtelEnvironmentVariables.SdkDisabled] = "true" })
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        Assert.Null(config[OtelEnvironmentVariables.SdkDisabled]);
    }

    [Fact]
    public void FailedParse_RemovesSource_NoMaskRemains()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(fileFormat: "99.0");

        using var configManager = new ConfigurationManager();
        configManager.AddInMemoryCollection(new Dictionary<string, string?> { [OtelEnvironmentVariables.SdkDisabled] = "true" });

        Assert.Throws<DeclarativeConfigurationException>(() => configManager.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path));
        Assert.Empty(((IConfigurationRoot)configManager).Providers.OfType<DeclarativeConfigurationProvider>());
        Assert.Equal("true", configManager[OtelEnvironmentVariables.SdkDisabled]);
    }

    private sealed class EmptyResourceDetector : IResourceDetector
    {
        public Resource Detect() => Resource.Empty;
    }
}
