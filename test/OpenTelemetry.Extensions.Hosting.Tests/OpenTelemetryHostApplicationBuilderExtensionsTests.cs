// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Extensions.Hosting.Implementation;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Extensions.Hosting.Tests;

public class OpenTelemetryHostApplicationBuilderExtensionsTests
{
    [Fact]
    public void AddOpenTelemetry_NullBuilder_Throws()
    {
        IHostApplicationBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddOpenTelemetry());
    }

    [Fact]
    public void AddOpenTelemetry_ReturnsBuilderBackedByHostServices()
    {
        var builder = Host.CreateApplicationBuilder();

        var openTelemetryBuilder = builder.AddOpenTelemetry();

        Assert.Same(builder.Services, openTelemetryBuilder.Services);
    }

    [Fact]
    public void AddOpenTelemetry_CalledTwice_RegistersSingleHostedService()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddOpenTelemetry();
        builder.AddOpenTelemetry();

        Assert.Single(builder.Services, IsTelemetryHostedService);
    }

    [Fact]
    public void AddOpenTelemetry_MixedWithServiceCollectionOverload_RegistersSingleHostedService()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddOpenTelemetry();
        builder.Services.AddOpenTelemetry();

        Assert.Single(builder.Services, IsTelemetryHostedService);
    }

    [Fact]
    public async Task AddOpenTelemetry_StartsTracerProviderWithHost()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddSource("MyApp"));

        using var host = builder.Build();

        await host.StartAsync();

        var tracerProvider = host.Services.GetRequiredService<TracerProvider>();
        Assert.NotNull(tracerProvider);

        using var activitySource = new ActivitySource("MyApp");
        Assert.True(activitySource.HasListeners());

        await host.StopAsync();
    }

    [Fact]
    public void AddOpenTelemetry_RegistersHostConfigurationAsInstance()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddOpenTelemetry();

        var descriptor = Assert.Single(
            builder.Services,
            d => d.ServiceType == typeof(IConfigurationManager));

        Assert.Same(builder.Configuration, descriptor.ImplementationInstance);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddOpenTelemetry_RegisteredHostConfigurationResolvesToLiveConfiguration()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddOpenTelemetry();

        using var host = builder.Build();

        var configurationManager = host.Services.GetRequiredService<IConfigurationManager>();

        Assert.Same(builder.Configuration, configurationManager);

        // The live builder is what makes contributing a configuration source during setup
        // possible. Verify the registered instance is still a usable builder.
        Assert.IsType<IConfigurationBuilder>(configurationManager, exactMatch: false);
    }

    [Fact]
    public void AddOpenTelemetry_DoesNotOverrideExistingConfigurationManagerRegistration()
    {
        var builder = Host.CreateApplicationBuilder();

        using var applicationConfiguration = new ConfigurationManager();
        builder.Services.AddSingleton<IConfigurationManager>(applicationConfiguration);

        builder.AddOpenTelemetry();

        var descriptor = Assert.Single(
            builder.Services,
            d => d.ServiceType == typeof(IConfigurationManager));

        Assert.Same(applicationConfiguration, descriptor.ImplementationInstance);
    }

    [Fact]
    public void AddOpenTelemetry_DoesNotDisturbHostConfigurationRegistration()
    {
        var builder = Host.CreateApplicationBuilder();

        var descriptorsBefore = builder.Services
            .Count(d => d.ServiceType == typeof(IConfiguration));

        builder.AddOpenTelemetry();

        Assert.Equal(
            descriptorsBefore,
            builder.Services.Count(d => d.ServiceType == typeof(IConfiguration)));

        using var host = builder.Build();

        Assert.Same(builder.Configuration, host.Services.GetRequiredService<IConfiguration>());
    }

    [Fact]
    public void AddOpenTelemetry_SetsDefaultServiceNameFromApplicationName()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
    }

    [Fact]
    public void AddOpenTelemetry_SetsDefaultDeploymentEnvironmentFromEnvironmentName()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.EnvironmentName = "Staging";

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.Contains(resource.Attributes, a => a.Key == "deployment.environment.name" && (string)a.Value == "Staging");
    }

    [Fact]
    public void AddOpenTelemetry_SetsHostEnvironmentDefaultsForAllSignals()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Environment.EnvironmentName = "Staging";

        builder.AddOpenTelemetry()
            .WithLogging()
            .WithMetrics()
            .WithTracing();

        using var host = builder.Build();

        BaseProvider[] providers =
        [
            host.Services.GetRequiredService<LoggerProvider>(),
            host.Services.GetRequiredService<MeterProvider>(),
            host.Services.GetRequiredService<TracerProvider>(),
        ];

        foreach (var provider in providers)
        {
            var resource = provider.GetResource();
            Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
            Assert.Contains(resource.Attributes, a => a.Key == "deployment.environment.name" && (string)a.Value == "Staging");
        }
    }

    [Fact]
    public void AddOpenTelemetry_OtelServiceNameInConfiguration_OverridesServiceNameDefault()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_SERVICE_NAME"] = "env-service",
        });

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "env-service");
    }

    [Fact]
    public void AddOpenTelemetry_OtelResourceAttributesServiceName_OverridesServiceNameDefault()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = "service.name=from-otel-resource-attributes",
        });

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "from-otel-resource-attributes");
    }

    [Fact]
    public void AddOpenTelemetry_OtelResourceAttributesDeploymentEnv_OverridesDeploymentEnvDefault()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.EnvironmentName = "Development";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = "deployment.environment.name=staging",
        });

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "deployment.environment.name" && (string)a.Value == "Development");
        Assert.Contains(resource.Attributes, a => a.Key == "deployment.environment.name" && (string)a.Value == "staging");
    }

    [Fact]
    public void AddOpenTelemetry_DifferentlyCasedResourceAttribute_DoesNotOverrideHostDefault()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = "SERVICE.NAME=from-otel-resource-attributes",
        });

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.Contains(resource.Attributes, a => a.Key == "SERVICE.NAME" && (string)a.Value == "from-otel-resource-attributes");
    }

    [Fact]
    public void AddOpenTelemetry_ExplicitConfigureResource_OverridesHostEnvironmentDefaults()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Environment.EnvironmentName = "Development";

        builder.AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddAttributes(
            [
                new("service.name", "explicit-service"),
                new("deployment.environment.name", "prod"),
            ]))
            .WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.DoesNotContain(resource.Attributes, a => a.Key == "deployment.environment.name" && (string)a.Value == "Development");
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "explicit-service");
        Assert.Contains(resource.Attributes, a => a.Key == "deployment.environment.name" && (string)a.Value == "prod");
    }

    [Fact]
    public void AddOpenTelemetry_ExplicitConfigurationRegisteredFirst_OverridesHostEnvironmentDefaults()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Environment.EnvironmentName = "Development";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddAttributes(
            [
                new("service.name", "explicit-service"),
                new("deployment.environment.name", "prod"),
            ]));

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.DoesNotContain(resource.Attributes, a => a.Key == "deployment.environment.name" && (string)a.Value == "Development");
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "explicit-service");
        Assert.Contains(resource.Attributes, a => a.Key == "deployment.environment.name" && (string)a.Value == "prod");
    }

    [Fact]
    public void AddOpenTelemetry_CalledAgain_DoesNotOverrideExplicitConfiguration()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Environment.EnvironmentName = "Development";

        builder.AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddAttributes(
            [
                new("service.name", "explicit-service"),
                new("deployment.environment.name", "prod"),
            ]));

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.DoesNotContain(resource.Attributes, a => a.Key == "deployment.environment.name" && (string)a.Value == "Development");
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "explicit-service");
        Assert.Contains(resource.Attributes, a => a.Key == "deployment.environment.name" && (string)a.Value == "prod");
    }

    [Fact]
    public void AddOpenTelemetry_EmptyApplicationName_DoesNotSetServiceName()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = string.Empty;

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && string.IsNullOrEmpty((string)a.Value));
    }

    [Fact]
    public void AddOpenTelemetry_WhitespaceApplicationName_DoesNotSetServiceName()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "   ";

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "   ");
    }

    [Fact]
    public void AddOpenTelemetry_EmptyEnvironmentName_DoesNotSetDeploymentEnvironmentName()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.EnvironmentName = string.Empty;

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "deployment.environment.name");
    }

    [Fact]
    public void AddOpenTelemetry_OtelResourceAttributesMultipleAttributes_OverridesBothDefaults()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Environment.EnvironmentName = "Development";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = "service.name=from-env,deployment.environment.name=staging",
        });

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "from-env");
        Assert.DoesNotContain(resource.Attributes, a => a.Key == "deployment.environment.name" && (string)a.Value == "Development");
        Assert.Contains(resource.Attributes, a => a.Key == "deployment.environment.name" && (string)a.Value == "staging");
    }

    [Fact]
    public void AddOpenTelemetry_OtelResourceAttributesWithKeyWhitespace_OverridesDefault()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = " service.name = trimmed-service",
        });

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
    }

    [Fact]
    public void AddOpenTelemetry_OtelServiceNameEmptyString_DoesNotSuppressHostDefault()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_SERVICE_NAME"] = string.Empty,
        });

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
    }

    [Fact]
    public void AddOpenTelemetry_CalledTwice_RegistersSingleSetOfHostEnvironmentConfigureBuilders()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddOpenTelemetry();

        var serviceCountAfterFirst = builder.Services.Count;

        builder.AddOpenTelemetry();

        Assert.Equal(serviceCountAfterFirst, builder.Services.Count);
    }

    [Fact]
    public void AddOpenTelemetry_PerSignalConfigureResourceInWithTracing_OverridesHostDefault()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";

        builder.AddOpenTelemetry()
            .WithTracing(b => b.ConfigureResource(rb => rb.AddAttributes(
            [
                new("service.name", "per-signal-service"),
            ])));

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "per-signal-service");
    }

    [Fact]
    public void AddOpenTelemetry_ConfigureResourceViaServicesAfterHostSetup_OverridesHostDefault()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";

        builder.AddOpenTelemetry().WithTracing();

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddAttributes(
            [
                new("service.name", "from-services"),
            ]));

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "from-services");
    }

    [Fact]
    public void AddOpenTelemetry_MultipleConfigureResourceSameKey_LastCallWins()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";

        builder.AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddAttributes([new("service.name", "first-call")]))
            .ConfigureResource(rb => rb.AddAttributes([new("service.name", "second-call")]))
            .WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "first-call");
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "second-call");
    }

    [Fact]
    public void AddOpenTelemetry_MultipleConfigureResourceDifferentKeys_BothAttributesPresent()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";

        builder.AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddAttributes([new("custom.key.a", "value-a")]))
            .ConfigureResource(rb => rb.AddAttributes([new("custom.key.b", "value-b")]))
            .WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.Contains(resource.Attributes, a => a.Key == "custom.key.a" && (string)a.Value == "value-a");
        Assert.Contains(resource.Attributes, a => a.Key == "custom.key.b" && (string)a.Value == "value-b");
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
    }

    [Fact]
    public void AddOpenTelemetry_ConfigureResourcePartialOverride_HostDefaultFillsUnsetAttribute()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Environment.EnvironmentName = "Staging";

        builder.AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddAttributes([new("service.name", "explicit-service")]))
            .WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "explicit-service");
        Assert.Contains(resource.Attributes, a => a.Key == "deployment.environment.name" && (string)a.Value == "Staging");
    }

    [Fact]
    public void AddOpenTelemetry_ConfigureResourceAddsCustomAttribute_CoexistsWithHostDefaults()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Environment.EnvironmentName = "Staging";

        builder.AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddAttributes([new("custom.attr", "custom-value")]))
            .WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.Contains(resource.Attributes, a => a.Key == "deployment.environment.name" && (string)a.Value == "Staging");
        Assert.Contains(resource.Attributes, a => a.Key == "custom.attr" && (string)a.Value == "custom-value");
    }

    [Fact]
    public void AddOpenTelemetry_PerSignalConfigureResource_DoesNotAffectOtherSignals()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";

        builder.AddOpenTelemetry()
            .WithTracing(b => b.ConfigureResource(rb => rb.AddAttributes([new("service.name", "tracer-service")])))
            .WithMetrics();

        using var host = builder.Build();

        var tracerResource = host.Services.GetRequiredService<TracerProvider>().GetResource();
        var meterResource = host.Services.GetRequiredService<MeterProvider>().GetResource();

        Assert.DoesNotContain(tracerResource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.Contains(tracerResource.Attributes, a => a.Key == "service.name" && (string)a.Value == "tracer-service");
        Assert.Contains(meterResource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
    }

    [Fact]
    public void AddOpenTelemetry_ConfigureResourceAddService_OverridesHostDefault()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";

        builder.AddOpenTelemetry()
            .ConfigureResource(rb => rb.AddService("explicit-service"))
            .WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "explicit-service");
    }

    [Fact]
    public void AddOpenTelemetry_ConfigureResourceWithClear_RemovesHostDefaults()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";

        builder.AddOpenTelemetry()
            .ConfigureResource(rb => rb.Clear().AddAttributes([new("service.name", "fresh-service")]))
            .WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "fresh-service");
    }

    [Fact]
    public void AddOpenTelemetry_OtelServiceNameWhitespace_DoesNotSuppressHostDefault()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_SERVICE_NAME"] = "   ",
        });

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
    }

    [Fact]
    public void AddOpenTelemetry_WhitespaceEnvironmentName_DoesNotSetDeploymentEnvironmentName()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.EnvironmentName = "   ";

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "deployment.environment.name");
    }

    [Fact]
    public void AddOpenTelemetry_OtelServiceNameSet_DoesNotSuppressDeploymentEnvironmentDefault()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Environment.EnvironmentName = "Staging";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_SERVICE_NAME"] = "env-service",
        });

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "env-service");
        Assert.Contains(resource.Attributes, a => a.Key == "deployment.environment.name" && (string)a.Value == "Staging");
    }

    [Fact]
    public void AddOpenTelemetry_OtelResourceAttributesMalformedEntryBeforeValid_SkipsMalformedAndAppliesValid()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = "malformed,service.name=valid",
        });

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "valid");
    }

    [Fact]
    public void AddOpenTelemetry_OtelResourceAttributesEmptyValue_SuppressesHostDefault()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = "service.name=",
        });

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        // The key is present (eq > 0) so the host default is suppressed even though the value is empty.
        Assert.DoesNotContain(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
    }

    [Fact]
    public void AddOpenTelemetry_OtelResourceAttributesLeadingEquals_DoesNotSuppressHostDefault()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = "=value",
        });

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        // eq == 0 so eq > 0 is false; the pair is skipped and the host default is applied.
        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
    }

    [Fact]
    public void AddOpenTelemetry_OtelResourceAttributesWhitespaceOnly_DoesNotSuppressHostDefaults()
    {
        var builder = CreateBuilderWithoutOtelResourceEnvironmentVariables();
        builder.Environment.ApplicationName = "MyTestApp";
        builder.Environment.EnvironmentName = "Staging";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = "   ",
        });

        builder.AddOpenTelemetry().WithTracing();

        using var host = builder.Build();
        var resource = host.Services.GetRequiredService<TracerProvider>().GetResource();

        Assert.Contains(resource.Attributes, a => a.Key == "service.name" && (string)a.Value == "MyTestApp");
        Assert.Contains(resource.Attributes, a => a.Key == "deployment.environment.name" && (string)a.Value == "Staging");
    }

    private static HostApplicationBuilder CreateBuilderWithoutOtelResourceEnvironmentVariables()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_RESOURCE_ATTRIBUTES"] = null,
            ["OTEL_SERVICE_NAME"] = null,
        });

        return builder;
    }

    private static bool IsTelemetryHostedService(ServiceDescriptor descriptor)
        => descriptor.ServiceType == typeof(IHostedService)
            && descriptor.ImplementationType == typeof(TelemetryHostedService);
}
