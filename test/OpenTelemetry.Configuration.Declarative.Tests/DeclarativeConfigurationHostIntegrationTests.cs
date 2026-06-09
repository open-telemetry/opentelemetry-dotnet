// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// Tests here call experimental APIs intentionally.
#pragma warning disable OTEL1006

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Configuration.Declarative.Tests;

/// <summary>
/// End-to-end tests verifying that YAML configuration flows through real IHost
/// infrastructure to the OTel SDK. Covers both the modern HostApplicationBuilder
/// (ConfigurationManager, in-place insert) and the classic HostBuilder
/// (ConfigurationRoot, chain-and-replace) paths.
/// </summary>
public sealed class DeclarativeConfigurationHostIntegrationTests
{
    // --------------------------------------------------------------------------
    // HostApplicationBuilder (modern hosting)
    // IConfiguration is backed by a ConfigurationManager. Sources can be
    // added directly on builder.Configuration, or via UseDeclarativeConfiguration
    // which resolves the ConfigurationManager and inserts the source in-place.
    // --------------------------------------------------------------------------

    [Fact]
    public void ModernHost_BuilderConfigurationExtension_ResourceAttributesFlowToSdk()
    {
        // Recommended API for HostApplicationBuilder: add source directly on
        // builder.Configuration (the live ConfigurationManager).
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "modern-host-svc" });

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path);
        builder.Services.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();

        var tracerProvider = host.Services.GetRequiredService<TracerProvider>();
        var resource = tracerProvider.GetResource();

        Assert.Contains(
            resource.Attributes,
            a => a.Key == "service.name" && (string)a.Value == "modern-host-svc");
    }

    [Fact]
    public void ModernHost_UseDeclarativeConfiguration_ResourceAttributesFlowToSdk()
    {
        // Alternative API: wire through IOpenTelemetryBuilder.UseDeclarativeConfiguration.
        // The overlay detects the factory-registered ConfigurationManager and inserts
        // the source in-place via the resolved instance.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "modern-host-svc-via-otel" });

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddOpenTelemetry()
            .UseDeclarativeConfiguration(yamlFile.Path)
            .WithTracing();

        using var host = builder.Build();

        var tracerProvider = host.Services.GetRequiredService<TracerProvider>();
        var resource = tracerProvider.GetResource();

        Assert.Contains(
            resource.Attributes,
            a => a.Key == "service.name" && (string)a.Value == "modern-host-svc-via-otel");
    }

    [Fact]
    public void ModernHost_DisabledTrue_ProducesNoopTracerProvider()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path);
        builder.Services.AddOpenTelemetry().WithTracing(b => b.AddSource("modern.disabled.source"));

        using var host = builder.Build();

        // NoopTracerProvider does not register as an ActivitySource listener.
        using var source = new ActivitySource("modern.disabled.source");
        _ = host.Services.GetRequiredService<TracerProvider>(); // trigger build
        using var activity = source.StartActivity("work");

        Assert.Null(activity);
    }

    [Fact]
    public void ModernHost_DisabledTrue_ProducesNoopMeterProvider()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path);
        builder.Services.AddOpenTelemetry().WithMetrics();

        using var host = builder.Build();

        var meterProvider = host.Services.GetRequiredService<MeterProvider>();
        Assert.Equal("NoopMeterProvider", meterProvider.GetType().Name);
    }

    [Fact]
    public void ModernHost_InvalidYaml_ThrowsDeclarativeConfigurationExceptionWhenSourceAdded()
    {
        const string yaml = "{ unclosed: [";

        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(yaml);

        var builder = Host.CreateApplicationBuilder();

        // ConfigurationManager loads providers eagerly when a source is added, so invalid
        // YAML fails during registration rather than at host.Build().
        Assert.Throws<DeclarativeConfigurationException>(() =>
            builder.Configuration.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path));
    }

    [Fact]
    public void ModernHost_SourceAddedAfterYaml_OverridesYaml()
    {
        // Prove overlay ordering: a source added to the ConfigurationManager after YAML
        // occupies a higher position in the chain and takes precedence over YAML values.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "from-yaml" });

        var builder = Host.CreateApplicationBuilder();

        // YAML is added first, then the in-memory source is added after it.
        builder.Configuration.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = "service.name=from-in-memory",
        });

        builder.Services.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();

        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();
        Assert.Contains(
            resource.Attributes,
            a => a.Key == "service.name" && (string)a.Value == "from-in-memory");
    }

    [Fact]
    public void ModernHost_YamlOverridesSourceAddedBeforeIt()
    {
        // Prove overlay ordering: YAML beats sources that were already in the
        // ConfigurationManager when AddOpenTelemetryDeclarativeConfiguration was called,
        // because YAML is appended last (highest position at that point).
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "from-yaml" });

        var builder = Host.CreateApplicationBuilder();

        // In-memory source added first, YAML appended after - YAML wins.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = "service.name=from-in-memory",
        });
        builder.Configuration.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path);

        builder.Services.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();

        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();
        Assert.Contains(
            resource.Attributes,
            a => a.Key == "service.name" && (string)a.Value == "from-yaml");
    }

    // --------------------------------------------------------------------------
    // HostBuilder (classic hosting)
    // IConfiguration is a built ConfigurationRoot. Sources can be added via
    // ConfigureAppConfiguration, or via UseDeclarativeConfiguration which
    // wraps the root in a new ConfigurationManager and chains the original.
    // --------------------------------------------------------------------------

    [Fact]
    public void ClassicHost_ConfigureAppConfiguration_ResourceAttributesFlowToSdk()
    {
        // Recommended API for HostBuilder: add the source inside ConfigureAppConfiguration
        // so it becomes part of the host's composed IConfiguration.
        using var yamlFile = new DeclarativeYamlTestFileFactory();
        var yamlPath = yamlFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "classic-host-svc" });

        using var host = new HostBuilder()
            .ConfigureAppConfiguration(configBuilder =>
                configBuilder.AddOpenTelemetryDeclarativeConfiguration(yamlPath))
            .ConfigureServices(services => services.AddOpenTelemetry().WithTracing())
            .Build();

        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.Contains(
            resource.Attributes,
            a => a.Key == "service.name" && (string)a.Value == "classic-host-svc");
    }

    [Fact]
    public void ClassicHost_UseDeclarativeConfiguration_ResourceAttributesFlowToSdk()
    {
        // UseDeclarativeConfiguration on IOpenTelemetryBuilder for HostBuilder:
        // the overlay detects the factory-registered ConfigurationRoot, wraps it
        // in a new ConfigurationManager, and chains the original so all keys resolve.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "classic-host-svc-via-otel" });

        using var host = new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddOpenTelemetry()
                    .UseDeclarativeConfiguration(yamlFile.Path)
                    .WithTracing();
            })
            .Build();

        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.Contains(
            resource.Attributes,
            a => a.Key == "service.name" && (string)a.Value == "classic-host-svc-via-otel");
    }

    [Fact]
    public void ClassicHost_DisabledTrue_ProducesNoopTracerProvider()
    {
        // Mirror of ModernHost_DisabledTrue_ProducesNoopTracerProvider for the classic host path.
        using var yamlFile = new DeclarativeYamlTestFileFactory();
        var yamlPath = yamlFile.CreateDeclarativeYaml(disabled: true);

        using var host = new HostBuilder()
            .ConfigureAppConfiguration(b => b.AddOpenTelemetryDeclarativeConfiguration(yamlPath))
            .ConfigureServices(s => s.AddOpenTelemetry().WithTracing(b => b.AddSource("classic.disabled.source")))
            .Build();

        using var source = new ActivitySource("classic.disabled.source");
        _ = host.Services.GetRequiredService<TracerProvider>();
        using var activity = source.StartActivity("work");

        Assert.Null(activity);
    }

    [Fact]
    public void ClassicHost_InvalidYaml_ThrowsDeclarativeConfigurationExceptionOnBuild()
    {
        const string yaml = "{ unclosed: [";

        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(yaml);

        var hostBuilder = new HostBuilder()
            .ConfigureAppConfiguration(b =>
                b.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path));

        // Classic HostBuilder composes a ConfigurationRoot during Build(); providers
        // load at that point rather than when the source is registered.
        Assert.Throws<DeclarativeConfigurationException>(() => hostBuilder.Build());
    }

    [Fact]
    public void ModernHost_YamlOverridesOtelEnvVar_WhenEnvRegisteredFirst()
    {
        // Demonstrates the canonical precedence model: YAML is appended after the host's
        // environment-variable source (added during HostApplicationBuilder construction),
        // so YAML takes precedence over OTel env vars already in the process environment.
        const string envVarName = "OTEL_RESOURCE_ATTRIBUTES";
        using var envScope = EnvironmentVariableScope.Create(envVarName, "service.name=from-env");
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "from-yaml" });

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path);
        builder.Services.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();

        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();
        Assert.Contains(
            resource.Attributes,
            a => a.Key == "service.name" && (string)a.Value == "from-yaml");
    }

    [Fact]
    public void BareServiceCollection_DeclarativeConfigRegisteredBeforeIConfiguration_YamlUnreachable()
    {
        // Call-order hazard: UseDeclarativeConfiguration is called when no IConfiguration
        // is present in the service collection. A fresh ConfigurationManager with only the
        // YAML source is registered. Any IConfiguration added AFTER takes the last-registered
        // slot; DI resolves that one instead and the YAML-backed instance is shadowed.
        // Prefer builder.Configuration.AddOpenTelemetryDeclarativeConfiguration on
        // HostApplicationBuilder, or ConfigureAppConfiguration on HostBuilder.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "from-yaml" });

        var services = new ServiceCollection();
        services.AddOpenTelemetry()
            .UseDeclarativeConfiguration(yamlFile.Path);

        // Simulates host infrastructure registering IConfiguration after OTel setup.
        var lateConfig = new ConfigurationManager();
        services.AddSingleton<IConfiguration>(lateConfig);

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IConfiguration>();

        Assert.Null(resolved["OTEL_RESOURCE_ATTRIBUTES"]);
    }

    [Fact]
    public void ClassicHost_UseDeclarativeConfiguration_ExistingConfigStillResolves()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        using var host = new HostBuilder()
            .ConfigureAppConfiguration(b =>
                b.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["some-app-key"] = "app-value",
                }))
            .ConfigureServices(services =>
            {
                services.AddOpenTelemetry()
                    .UseDeclarativeConfiguration(yamlFile.Path);
            })
            .Build();

        // Non-declarative key from host config must survive.
        var config = host.Services.GetRequiredService<IConfiguration>();
        Assert.Equal("app-value", config["some-app-key"]);
    }
}
