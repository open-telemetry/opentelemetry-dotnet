// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable OTEL1006

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative.Tests;

/// <summary>
/// End-to-end tests proving that YAML > IConfiguration > OTel SDK produces
/// the expected observable effects. Each test covers one full path through the
/// pipeline so regressions at any layer are caught here.
/// </summary>
public sealed class DeclarativeConfigurationSdkIntegrationTests
{
    [Fact]
    public void DisabledTrue_YamlFile_ProducesNoopTracerProvider()
    {
        const string disabledSourceName = "disabled.source";
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        using var tracerProvider = BuildTracerProvider(yamlFile.Path, sourceName: disabledSourceName);

        // NoopTracerProvider does not listen to any ActivitySource.
        // Even with AddSource registered, StartActivity returns null.
        using var source = new ActivitySource(disabledSourceName);
        using var activity = source.StartActivity("work");
        Assert.Null(activity);
    }

    [Fact]
    public void DisabledFalse_YamlFile_ProducesRealTracerProvider()
    {
        const string enabledSourceName = "enabled.source";
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        using var tracerProvider = BuildTracerProvider(yamlFile.Path, sourceName: enabledSourceName);

        using var source = new ActivitySource(enabledSourceName);
        using var activity = source.StartActivity("work");
        Assert.NotNull(activity);
    }

    [Fact]
    public void DisabledTrue_YamlFile_ProducesNoopMeterProvider()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        using var meterProvider = BuildMeterProvider(yamlFile.Path);

        Assert.Equal("NoopMeterProvider", meterProvider.GetType().Name);

        using var meter = new Meter("disabled.metrics.meter");
        var counter = meter.CreateCounter<long>("test_counter");
        counter.Add(1);
        meterProvider.ForceFlush();
    }

    [Fact]
    public void DisabledTrue_YamlFile_ProducesNoopLoggerProvider()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        using var loggerProvider = BuildLoggerProvider(yamlFile.Path);

        Assert.Equal("NoopLoggerProvider", loggerProvider.GetType().Name);
    }

    [Fact]
    public void DisabledFalse_YamlOverridesInMemoryOtelSdkDisabled_ProducesRealTracerProvider()
    {
        const string enabledSourceName = "yaml.enabled.source";
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [OtelEnvironmentVariables.SdkDisabled] = "true" })
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .AddSource(enabledSourceName)
            .Build()!;

        using var source = new ActivitySource(enabledSourceName);
        Assert.NotNull(source.StartActivity("work"));
    }

    [Fact]
    public void ResourceAttributes_SingleAttribute_FlowToSdkResource()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "my-test-service" });

        using var tracerProvider = BuildTracerProvider(yamlFile.Path);

        var resource = tracerProvider.GetResource();
        Assert.Contains(
            resource.Attributes,
            a => a.Key == "service.name" && (string)a.Value == "my-test-service");
    }

    [Fact]
    public void ResourceAttributes_MultipleAttributes_AllFlowToSdkResource()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string>
            {
                ["service.name"] = "svc",
                ["service.version"] = "2.0.0",
                ["deployment.environment"] = "test",
            });

        using var tracerProvider = BuildTracerProvider(yamlFile.Path);

        var resource = tracerProvider.GetResource();
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "svc");
        Assert.Contains(resource.Attributes, a => a.Key == "service.version" && (string)a.Value == "2.0.0");
        Assert.Contains(resource.Attributes, a => a.Key == "deployment.environment" && (string)a.Value == "test");
    }

    [Fact]
    public void EnvSubstitution_DefaultUsed_ResourceAttributeFlowsToSdk()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: ${OTEL_TEST_SVC_NAME:-substituted-service}
            """;

        // Ensure env var is not set so the default kicks in.
        using var envScope = EnvironmentVariableScope.Create("OTEL_TEST_SVC_NAME", null);

        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(yaml);
        using var tracerProvider = BuildTracerProvider(yamlFile.Path);

        var resource = tracerProvider.GetResource();
        Assert.Contains(
            resource.Attributes,
            a => a.Key == "service.name" && (string)a.Value == "substituted-service");
    }

    [Fact]
    public void EnvSubstitution_EnvVarSet_ResourceAttributeUsesEnvValue()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: ${OTEL_TEST_SVC_NAME:-fallback}
            """;

        using var envScope = EnvironmentVariableScope.Create("OTEL_TEST_SVC_NAME", "from-env");
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(yaml);
        using var tracerProvider = BuildTracerProvider(yamlFile.Path);

        var resource = tracerProvider.GetResource();
        Assert.Contains(
            resource.Attributes,
            a => a.Key == "service.name" && (string)a.Value == "from-env");
    }

    [Fact]
    public void OverlayPrecedence_SourceAddedAfterYaml_OverridesYaml()
    {
        // Prove overlay ordering: a source added after YAML (higher position in the
        // IConfiguration chain) takes precedence over YAML values.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "from-yaml" });

        // YAML is added first, then the in-memory source is added after it.
        // Because the in-memory source occupies a higher position in the chain,
        // it wins regardless of source types.
        var config = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_RESOURCE_ATTRIBUTES"] = "service.name=from-in-memory",
            })
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .Build();

        var resource = tracerProvider.GetResource();

        Assert.Contains(
            resource.Attributes,
            a => a.Key == "service.name" && (string)a.Value == "from-in-memory");
    }

    [Fact]
    public void OverlayPrecedence_YamlWinsOverSourceAddedBeforeIt()
    {
        // Prove overlay ordering: YAML beats sources that were already in the builder
        // when AddOpenTelemetryDeclarativeConfiguration was called, because YAML is
        // appended last (highest position at that point).
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "from-yaml" });

        // In-memory source is added first, YAML is appended after it.
        // YAML occupies a higher position so its value wins.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_RESOURCE_ATTRIBUTES"] = "service.name=from-in-memory",
            })
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .Build();

        var resource = tracerProvider.GetResource();

        Assert.Contains(
            resource.Attributes,
            a => a.Key == "service.name" && (string)a.Value == "from-yaml");
    }

    private static TracerProvider BuildTracerProvider(string yamlPath, string? sourceName = null)
    {
        var config = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlPath)
            .Build();

        var builder = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config));

        if (sourceName != null)
        {
            builder = builder.AddSource(sourceName);
        }

        return builder.Build()!;
    }

    private static MeterProvider BuildMeterProvider(string yamlPath)
    {
        var config = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlPath)
            .Build();

        return Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .Build()!;
    }

    private static LoggerProvider BuildLoggerProvider(string yamlPath)
    {
        var config = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlPath)
            .Build();

        return Sdk.CreateLoggerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .Build()!;
    }
}
