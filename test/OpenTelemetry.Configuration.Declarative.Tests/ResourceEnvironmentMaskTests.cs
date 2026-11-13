// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative.Tests;

[Collection(EnvVarsCollectionDefinition.Name)]
public sealed class ResourceEnvironmentMaskTests
{
    private const string ApplicationName = "resource-mask-tests";
    private const string EnvironmentAttributes = "env.attribute=from-env,service.name=from-resource-env";
    private const string EnvironmentServiceName = "from-service-env";

    public enum RegistrationKind
    {
        /// <summary>No application configuration is registered.</summary>
        NoConfiguration,

        /// <summary>A live configuration manager is registered.</summary>
        ConfigurationManager,

        /// <summary>An immutable configuration root is registered.</summary>
        ConfigurationRoot,

        /// <summary>The host-builder API registers declarative configuration.</summary>
        ModernHost,

        /// <summary>The service-collection API registers configuration on a modern host.</summary>
        ModernHostServices,

        /// <summary>A classic host resolves configuration through a factory.</summary>
        ClassicHost,

        /// <summary>An existing document is reused through a chained root.</summary>
        ChainedRoot,

        /// <summary>An existing document is reused through two chained roots.</summary>
        NestedChainedRoot,
    }

    public enum DocumentKind
    {
        /// <summary>The file is empty.</summary>
        EmptyFile,

        /// <summary>The resource section is omitted.</summary>
        NoResource,

        /// <summary>The resource section is an empty mapping.</summary>
        EmptyResource,

        /// <summary>The attribute list is explicitly null.</summary>
        NullAttributesList,

        /// <summary>The attribute list is empty.</summary>
        EmptyAttributesList,

        /// <summary>The attribute list contains only whitespace.</summary>
        WhitespaceAttributesList,

        /// <summary>Typed attributes configure the resource.</summary>
        TypedAttributes,

        /// <summary>An attribute list configures the resource.</summary>
        AttributesList,

        /// <summary>Typed attributes override matching attribute-list entries.</summary>
        MixedAttributes,
    }

    public static TheoryData<RegistrationKind, DocumentKind> MaskingCases
    {
        get
        {
            var cases = new TheoryData<RegistrationKind, DocumentKind>();
#if NET
            var registrations = Enum.GetValues<RegistrationKind>();
            var documents = Enum.GetValues<DocumentKind>();
#else
            var registrations = (RegistrationKind[])Enum.GetValues(typeof(RegistrationKind));
            var documents = (DocumentKind[])Enum.GetValues(typeof(DocumentKind));
#endif
            foreach (var registration in registrations)
            {
                foreach (var document in documents)
                {
                    cases.Add(registration, document);
                }
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(MaskingCases))]
    public void ProcessEnvironment_IsMaskedOnAllSignals(
        RegistrationKind registration,
        DocumentKind document)
    {
        using var attributesScope = EnvironmentVariableScope.Create(
            OtelEnvironmentVariables.ResourceAttributes, EnvironmentAttributes);
        using var serviceScope = EnvironmentVariableScope.Create(
            OtelEnvironmentVariables.ServiceName, EnvironmentServiceName);
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(GetYaml(document));

        WithServices(registration, yamlFile.Path, services =>
        {
            AssertAllResources(services, resource =>
            {
                var attributes = resource.Attributes.ToDictionary(attribute => attribute.Key, attribute => attribute.Value);
                Assert.Equal("opentelemetry", attributes["telemetry.sdk.name"]);
                Assert.Equal("dotnet", attributes["telemetry.sdk.language"]);
                Assert.DoesNotContain("env.attribute", attributes.Keys);

                var serviceName = Assert.IsType<string>(attributes["service.name"]);
                if (document is DocumentKind.TypedAttributes or DocumentKind.AttributesList or DocumentKind.MixedAttributes)
                {
                    Assert.Equal("from-yaml", serviceName);
                    Assert.Equal("from-yaml", attributes["kept.attribute"]);
                    if (document is DocumentKind.MixedAttributes)
                    {
                        Assert.Equal("from-list", attributes["list.only"]);
                    }
                }
                else if (registration is RegistrationKind.ModernHost)
                {
                    Assert.Equal(ApplicationName, serviceName);
                }
                else
                {
                    Assert.StartsWith("unknown_service", serviceName, StringComparison.Ordinal);
                }
            });
        });
    }

    [Theory]
    [InlineData(RegistrationKind.NoConfiguration)]
    [InlineData(RegistrationKind.NestedChainedRoot)]
    public void ExplicitEnvironmentSubstitution_IsPreservedOnAllSignals(RegistrationKind registration)
    {
        using var attributesScope = EnvironmentVariableScope.Create(
            OtelEnvironmentVariables.ResourceAttributes, EnvironmentAttributes);
        using var serviceScope = EnvironmentVariableScope.Create(
            OtelEnvironmentVariables.ServiceName, EnvironmentServiceName);
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile("""
            file_format: "1.0"
            resource:
              attributes_list: "${OTEL_RESOURCE_ATTRIBUTES}"
              attributes:
                - name: service.name
                  value: "${OTEL_SERVICE_NAME}"
            """);

        WithServices(registration, yamlFile.Path, services =>
            AssertAllResources(services, resource =>
            {
                var attributes = resource.Attributes.ToDictionary(attribute => attribute.Key, attribute => attribute.Value);
                Assert.Equal("from-env", attributes["env.attribute"]);
                Assert.Equal(EnvironmentServiceName, attributes["service.name"]);
            }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithoutDeclarativeConfiguration_ProcessEnvironmentStillApplies(bool registerConfiguration)
    {
        using var attributesScope = EnvironmentVariableScope.Create(
            OtelEnvironmentVariables.ResourceAttributes, EnvironmentAttributes);
        using var serviceScope = EnvironmentVariableScope.Create(
            OtelEnvironmentVariables.ServiceName, EnvironmentServiceName);
        using var configuration = new ConfigurationManager();
        var services = new ServiceCollection();
        if (registerConfiguration)
        {
            services.AddSingleton<IConfiguration>(configuration);
        }

        services.AddOpenTelemetry().WithTracing().WithMetrics().WithLogging();
        using var provider = services.BuildServiceProvider();

        AssertAllResources(provider, resource =>
        {
            var attributes = resource.Attributes.ToDictionary(attribute => attribute.Key, attribute => attribute.Value);
            Assert.Equal("from-env", attributes["env.attribute"]);
            Assert.Equal(EnvironmentServiceName, attributes["service.name"]);
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void LaterSource_OverridesMaskOnAllSignals(int chainDepth)
    {
        using var attributesScope = EnvironmentVariableScope.Create(
            OtelEnvironmentVariables.ResourceAttributes, EnvironmentAttributes);
        using var serviceScope = EnvironmentVariableScope.Create(
            OtelEnvironmentVariables.ServiceName, EnvironmentServiceName);
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml();
        using var configuration = new ConfigurationManager();
        configuration.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path);
        using var outer = new ConfigurationManager();
        outer.AddConfiguration(configuration);
        using var nested = new ConfigurationManager();
        nested.AddConfiguration(outer);
        var effective = chainDepth switch
        {
            0 => configuration,
            1 => outer,
            _ => nested,
        };
        effective.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [OtelEnvironmentVariables.ResourceAttributes] = "later.attribute=from-later,service.name=from-later-attributes",
            [OtelEnvironmentVariables.ServiceName] = "from-later-service",
        });

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(effective);
        services.AddOpenTelemetry().WithTracing().WithMetrics().WithLogging();
        using var provider = services.BuildServiceProvider();

        AssertAllResources(provider, resource =>
        {
            var attributes = resource.Attributes.ToDictionary(attribute => attribute.Key, attribute => attribute.Value);
            Assert.Equal("from-later", attributes["later.attribute"]);
            Assert.Equal("from-later-service", attributes["service.name"]);
            Assert.DoesNotContain("env.attribute", attributes.Keys);
        });
    }

    private static string GetYaml(DocumentKind document) => document switch
    {
        DocumentKind.EmptyFile => string.Empty,
        DocumentKind.NoResource => "file_format: \"1.0\"",
        DocumentKind.EmptyResource => "file_format: \"1.0\"\nresource: {}",
        DocumentKind.NullAttributesList => "file_format: \"1.0\"\nresource:\n  attributes_list: null",
        DocumentKind.EmptyAttributesList => "file_format: \"1.0\"\nresource:\n  attributes_list: \"\"",
        DocumentKind.WhitespaceAttributesList => "file_format: \"1.0\"\nresource:\n  attributes_list: \"  \"",
        DocumentKind.TypedAttributes => """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: from-yaml
                - name: kept.attribute
                  value: from-yaml
            """,
        DocumentKind.AttributesList => """
            file_format: "1.0"
            resource:
              attributes_list: "service.name=from-yaml,kept.attribute=from-yaml"
            """,
        DocumentKind.MixedAttributes => """
            file_format: "1.0"
            resource:
              attributes_list: "service.name=from-list,list.only=from-list"
              attributes:
                - name: service.name
                  value: from-yaml
                - name: kept.attribute
                  value: from-yaml
            """,
        _ => throw new ArgumentOutOfRangeException(nameof(document)),
    };

    private static void WithServices(
        RegistrationKind registration,
        string path,
        Action<IServiceProvider> assert)
    {
        if (registration is RegistrationKind.ModernHost or RegistrationKind.ModernHostServices)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ApplicationName = ApplicationName,
            });
            var telemetry = registration is RegistrationKind.ModernHost
                ? builder.AddOpenTelemetry()
                : builder.Services.AddOpenTelemetry();
            telemetry.UseDeclarativeConfiguration(path).WithTracing().WithMetrics().WithLogging();
            using var host = builder.Build();
            assert(host.Services);
            return;
        }

        if (registration is RegistrationKind.ClassicHost)
        {
            using var host = new HostBuilder()
                .ConfigureAppConfiguration(configuration => configuration.AddEnvironmentVariables())
                .ConfigureServices(services =>
                    services.AddOpenTelemetry().UseDeclarativeConfiguration(path)
                        .WithTracing().WithMetrics().WithLogging())
                .Build();
            assert(host.Services);
            return;
        }

        var services = new ServiceCollection();
        if (registration is RegistrationKind.NoConfiguration)
        {
            BuildAndAssert(services, path, assert);
            return;
        }

        if (registration is RegistrationKind.ConfigurationRoot)
        {
            using var root = (ConfigurationRoot)new ConfigurationBuilder().AddEnvironmentVariables().Build();
            services.AddSingleton<IConfiguration>(root);
            BuildAndAssert(services, path, assert);
            return;
        }

        using var configuration = new ConfigurationManager();
        configuration.AddEnvironmentVariables();
        if (registration is RegistrationKind.ConfigurationManager)
        {
            services.AddSingleton<IConfiguration>(configuration);
            BuildAndAssert(services, path, assert);
            return;
        }

        // Seed a document through the public API, then reuse it through immutable chained roots.
        var seedServices = new ServiceCollection();
        seedServices.AddSingleton<IConfiguration>(configuration);
        seedServices.AddOpenTelemetry().UseDeclarativeConfiguration(path);
        using var outer = (ConfigurationRoot)new ConfigurationBuilder().AddConfiguration(configuration).Build();
        using var nested = (ConfigurationRoot)new ConfigurationBuilder().AddConfiguration(outer).Build();
        services.AddSingleton<IConfiguration>(
            registration is RegistrationKind.ChainedRoot ? outer : nested);
        BuildAndAssert(services, path, assert);
    }

    private static void BuildAndAssert(
        IServiceCollection services,
        string path,
        Action<IServiceProvider> assert)
    {
        services.AddOpenTelemetry().UseDeclarativeConfiguration(path)
            .WithTracing().WithMetrics().WithLogging();
        using var provider = services.BuildServiceProvider();
        assert(provider);
    }

    private static void AssertAllResources(IServiceProvider services, Action<Resource> assert)
    {
        assert(services.GetRequiredService<TracerProvider>().GetResource());
        assert(services.GetRequiredService<MeterProvider>().GetResource());
        assert(services.GetRequiredService<LoggerProvider>().GetResource());
    }
}
