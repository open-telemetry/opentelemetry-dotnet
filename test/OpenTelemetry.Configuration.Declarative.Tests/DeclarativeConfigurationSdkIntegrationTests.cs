// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative.Tests;

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

#if OPENTELEMETRY_EXPERIMENTAL_FEATURES_EXPOSED
    [Fact]
    public void DisabledTrue_YamlFile_ProducesNoopLoggerProvider()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        using var loggerProvider = BuildLoggerProvider(yamlFile.Path);

        Assert.Equal("NoopLoggerProvider", loggerProvider.GetType().Name);
    }
#endif

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
        // resource.attributes routes through DeclarativeResourceDetector; UseDeclarativeConfiguration
        // registers both the config source and the detector.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "my-test-service" });

        using var host = BuildHostWithTracerProvider(yamlFile.Path);

        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();
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

        using var host = BuildHostWithTracerProvider(yamlFile.Path);

        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "svc");
        Assert.Contains(resource.Attributes, a => a.Key == "service.version" && (string)a.Value == "2.0.0");
        Assert.Contains(resource.Attributes, a => a.Key == "deployment.environment" && (string)a.Value == "test");
    }

    [Fact]
    public void ResourceAttributesAndSchemaUrl_FlowToAllSignalResources()
    {
        const string schemaUrl = "https://opentelemetry.io/schemas/1.24.0";
        const string yaml = $"""
            file_format: "1.0"
            resource:
              schema_url: "{schemaUrl}"
              attributes:
                - name: deployment.replicas
                  type: int
                  value: 3
            """;

        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(yaml);
        using var host = new HostBuilder()
            .ConfigureServices(services =>
                services.AddOpenTelemetry()
                    .UseDeclarativeConfiguration(yamlFile.Path)
                    .WithTracing()
                    .WithMetrics()
                    .WithLogging())
            .Build();

        AssertDeclarativeResource(
            host.Services.GetRequiredService<TracerProvider>().GetResource(),
            schemaUrl);
        AssertDeclarativeResource(
            host.Services.GetRequiredService<MeterProvider>().GetResource(),
            schemaUrl);
        AssertDeclarativeResource(
            host.Services.GetRequiredService<LoggerProvider>().GetResource(),
            schemaUrl);
    }

    [Theory]
    [InlineData(" leading", " leading")]
    [InlineData("trailing ", "trailing ")]
    [InlineData(" both ", " both ")]
    [InlineData("\\tleading\\t", "\tleading\t")]
    [InlineData("\\r\\nleading\\r\\n", "\r\nleading\r\n")]
    [InlineData("\\u00A0leading\\u00A0", "\u00A0leading\u00A0")]
    public void ResourceAttributes_StringWithSurroundingWhitespace_PreservesWhitespace(
        string yamlValue,
        string expected)
    {
        // The detector path does not URL-encode values, so whitespace is preserved verbatim.
        var yaml = $"""
            file_format: "1.1"
            resource:
              attributes:
                - name: review.whitespace
                  value: "{yamlValue}"
            """;

        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(yaml);
        using var host = BuildHostWithTracerProvider(yamlFile.Path);

        var actual = host.Services.GetRequiredService<TracerProvider>().GetResource().Attributes
            .Single(attribute => attribute.Key == "review.whitespace")
            .Value;

        Assert.Equal(expected, Assert.IsType<string>(actual));
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
        using var host = BuildHostWithTracerProvider(yamlFile.Path);

        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();
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
        using var host = BuildHostWithTracerProvider(yamlFile.Path);

        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();
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
            resourceAttributesList: "service.name=from-yaml");

        // YAML is added first, then the in-memory source is added after it.
        // Because the in-memory source occupies a higher position in the chain,
        // its OTEL_RESOURCE_ATTRIBUTES value wins.
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
        // DeclarativeResourceDetector (position 5) outranks OtelEnvResourceDetector (position 3).
        // Even when OTEL_RESOURCE_ATTRIBUTES says "from-env", the YAML typed attribute wins.
        const string envVarName = "OTEL_RESOURCE_ATTRIBUTES";
        using var envScope = EnvironmentVariableScope.Create(envVarName, "service.name=from-env");

        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "from-yaml" });

        using var host = BuildHostWithTracerProvider(yamlFile.Path);

        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.Contains(
            resource.Attributes,
            a => a.Key == "service.name" && (string)a.Value == "from-yaml");
    }

    [Fact]
    public void ResourceAttributes_OverrideSameNameAttributesListEntry()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes_list: precedence.key=from-list
              attributes:
                - name: precedence.key
                  value: from-attributes
            """;

        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(yaml);
        using var host = BuildHostWithTracerProvider(yamlFile.Path);

        var attribute = Assert.Single(
            host.Services.GetRequiredService<TracerProvider>().GetResource().Attributes,
            item => item.Key == "precedence.key");

        Assert.Equal("from-attributes", Assert.IsType<string>(attribute.Value));
    }

    [Fact]
    public void ResourceAttributes_ServiceNameOverridesOtelServiceName()
    {
        using var environment = EnvironmentVariableScope.Create(
            "OTEL_SERVICE_NAME",
            "from-environment");
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "from-yaml" });
        using var host = BuildHostWithTracerProvider(yamlFile.Path);

        var attribute = Assert.Single(
            host.Services.GetRequiredService<TracerProvider>().GetResource().Attributes,
            item => item.Key == "service.name");

        Assert.Equal("from-yaml", Assert.IsType<string>(attribute.Value));
    }

    [Fact]
    public void ConfigureResource_BeforeDeclarativeConfiguration_IsOverriddenByYaml()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["precedence.key"] = "from-yaml" });
        using var host = BuildHostWithProgrammaticResource(
            yamlFile.Path,
            configureAfterDeclarativeConfiguration: false);

        var attribute = Assert.Single(
            host.Services.GetRequiredService<TracerProvider>().GetResource().Attributes,
            item => item.Key == "precedence.key");

        Assert.Equal("from-yaml", Assert.IsType<string>(attribute.Value));
    }

    [Fact]
    public void ConfigureResource_AfterDeclarativeConfiguration_OverridesYaml()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["precedence.key"] = "from-yaml" });
        using var host = BuildHostWithProgrammaticResource(
            yamlFile.Path,
            configureAfterDeclarativeConfiguration: true);

        var attribute = Assert.Single(
            host.Services.GetRequiredService<TracerProvider>().GetResource().Attributes,
            item => item.Key == "precedence.key");

        Assert.Equal("from-programmatic", Assert.IsType<string>(attribute.Value));
    }

    [Fact]
    public void SourceOnlyPath_ResourceAttributes_DoNotContributeToResource()
    {
        // AddOpenTelemetryDeclarativeConfiguration does NOT register the detector.
        // Under option A, resource.attributes on that path contribute nothing to the Resource.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "should-not-appear" });

        var config = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .Build();

        var resource = tracerProvider.GetResource();

        Assert.DoesNotContain(
            resource.Attributes,
            a => a.Key == "service.name" && a.Value is string v && v == "should-not-appear");
    }

    [Fact]
    public void PlainSdk_SharedConfigurationRoot_RepresentativeConsumersShareProviderAccessor()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);
        var configuration = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();
        var providerAccessor = configuration.Providers
            .OfType<DeclarativeConfigurationProvider>()
            .Single()
            .Accessor;
        RepresentativeDeclarativeConfigurationConsumer? tracerConsumer = null;
        RepresentativeDeclarativeConfigurationConsumer? meterConsumer = null;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(configuration);
                services.AddRepresentativeDeclarativeConfigurationConsumer();
            })
            .ConfigureResource(resource => resource.AddDetector(serviceProvider =>
            {
                tracerConsumer =
                    serviceProvider.GetRequiredService<RepresentativeDeclarativeConfigurationConsumer>();
                return new EmptyResourceDetector();
            }))
            .Build();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<IConfiguration>(configuration);
                services.AddRepresentativeDeclarativeConfigurationConsumer();
            })
            .ConfigureResource(resource => resource.AddDetector(serviceProvider =>
            {
                meterConsumer =
                    serviceProvider.GetRequiredService<RepresentativeDeclarativeConfigurationConsumer>();
                return new EmptyResourceDetector();
            }))
            .Build();

        var resolvedTracerConsumer =
            Assert.IsType<RepresentativeDeclarativeConfigurationConsumer>(tracerConsumer);
        var resolvedMeterConsumer =
            Assert.IsType<RepresentativeDeclarativeConfigurationConsumer>(meterConsumer);

        Assert.Same(providerAccessor, resolvedTracerConsumer.Accessor);
        Assert.Same(providerAccessor, resolvedMeterConsumer.Accessor);
        Assert.Same(resolvedTracerConsumer.Document, resolvedMeterConsumer.Document);
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

        return builder.Build();
    }

    private static MeterProvider BuildMeterProvider(string yamlPath)
    {
        var config = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlPath)
            .Build();

        return Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .Build();
    }

#if OPENTELEMETRY_EXPERIMENTAL_FEATURES_EXPOSED
    private static LoggerProvider BuildLoggerProvider(string yamlPath)
    {
        var config = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlPath)
            .Build();

        return Sdk.CreateLoggerProviderBuilder()
            .ConfigureServices(s => s.AddSingleton<IConfiguration>(config))
            .Build()!;
    }
#endif

    private static IHost BuildHostWithTracerProvider(string yamlPath) =>
        new HostBuilder()
            .ConfigureServices(services =>
                services.AddOpenTelemetry()
                    .UseDeclarativeConfiguration(yamlPath)
                    .WithTracing())
            .Build();

    private static IHost BuildHostWithProgrammaticResource(
        string yamlPath,
        bool configureAfterDeclarativeConfiguration)
    {
        return new HostBuilder()
            .ConfigureServices(services =>
            {
                var builder = services.AddOpenTelemetry();

                if (!configureAfterDeclarativeConfiguration)
                {
                    builder.ConfigureResource(resource => resource.AddAttributes(
                        [new("precedence.key", "from-programmatic")]));
                }

                builder.UseDeclarativeConfiguration(yamlPath);

                if (configureAfterDeclarativeConfiguration)
                {
                    builder.ConfigureResource(resource => resource.AddAttributes(
                        [new("precedence.key", "from-programmatic")]));
                }

                builder.WithTracing();
            })
            .Build();
    }

    private static void AssertDeclarativeResource(Resource resource, string expectedSchemaUrl)
    {
        var attribute = Assert.Single(
            resource.Attributes,
            item => item.Key == "deployment.replicas");

        Assert.Equal(3L, Assert.IsType<long>(attribute.Value));
        Assert.Equal(expectedSchemaUrl, resource.SchemaUrl);
    }

    private sealed class EmptyResourceDetector : IResourceDetector
    {
        public Resource Detect() => Resource.Empty;
    }
}
