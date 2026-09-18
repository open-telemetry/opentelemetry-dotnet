// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class DeclarativeConfigurationDocumentAcquisitionTests
{
    private const int FailedToLoadConfigurationEventId = 12;
    private const int DocumentAccessorNotAvailableEventId = 28;
    private const int MultipleConfigurationDocumentsReachableEventId = 30;

    [Fact]
    public void GetOpenTelemetryDeclarativeConfiguration_NullServiceProvider_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            ((IServiceProvider)null!).GetOpenTelemetryDeclarativeConfiguration());

    [Fact]
    public void GetOpenTelemetryDeclarativeConfiguration_NullConfigurationRoot_Throws()
        => Assert.Throws<ArgumentNullException>(() =>
            ((IConfigurationRoot)null!).GetOpenTelemetryDeclarativeConfiguration());

    [Fact]
    public void GetOpenTelemetryDeclarativeConfiguration_ServiceProvider_NoDeclarativeSource_ReturnsNull()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        using var listener = CreateVerboseListener();
        using var sp = services.BuildServiceProvider();

        Assert.Null(sp.GetOpenTelemetryDeclarativeConfiguration());
        Assert.Single(listener.Messages, e => e.EventId == DocumentAccessorNotAvailableEventId);
    }

    [Fact]
    public void GetOpenTelemetryDeclarativeConfiguration_ConfigurationRoot_NoDeclarativeSource_ReturnsNull()
    {
        var configuration = new ConfigurationBuilder().Build();
        Assert.Null(configuration.GetOpenTelemetryDeclarativeConfiguration());
    }

    [Fact]
    public void GetOpenTelemetryDeclarativeConfiguration_ServiceProvider_ReturnsSameDocumentAsProvider()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var services = new ServiceCollection();
        new TestOpenTelemetryBuilder(services).UseDeclarativeConfiguration(yamlFile.Path);
        using var sp = services.BuildServiceProvider();

        var providerDocument = GetProviderDocument(sp);
        var document1 = sp.GetOpenTelemetryDeclarativeConfiguration();
        var document2 = sp.GetOpenTelemetryDeclarativeConfiguration();

        Assert.NotNull(document1);
        Assert.Same(providerDocument, document1);
        Assert.Same(document1, document2);
    }

    [Fact]
    public void GetOpenTelemetryDeclarativeConfiguration_ModernHost_ReturnsProviderDocument()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var builder = Host.CreateApplicationBuilder();
        builder.AddOpenTelemetry().UseDeclarativeConfiguration(yamlFile.Path);

        using var host = builder.Build();

        Assert.Same(
            GetProviderDocument(host.Services),
            host.Services.GetOpenTelemetryDeclarativeConfiguration());
    }

    [Fact]
    public void GetOpenTelemetryDeclarativeConfiguration_ClassicHost_ReturnsProviderDocument()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        using var host = new HostBuilder()
            .ConfigureServices(services =>
                services.AddOpenTelemetry().UseDeclarativeConfiguration(yamlFile.Path))
            .Build();

        Assert.Same(
            GetProviderDocument(host.Services),
            host.Services.GetOpenTelemetryDeclarativeConfiguration());
    }

    [Fact]
    public void GetOpenTelemetryDeclarativeConfiguration_ConfigureAppConfiguration_ReturnsProviderDocument()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        using var host = new HostBuilder()
            .ConfigureAppConfiguration(builder =>
                builder.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path))
            .Build();

        Assert.Same(
            GetProviderDocument(host.Services),
            host.Services.GetOpenTelemetryDeclarativeConfiguration());
    }

    [Fact]
    public void GetOpenTelemetryDeclarativeConfiguration_ConfigurationRoot_ReturnsSameDocumentOnRepeatedCalls()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var configuration = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        var document1 = configuration.GetOpenTelemetryDeclarativeConfiguration();
        var document2 = configuration.GetOpenTelemetryDeclarativeConfiguration();

        Assert.NotNull(document1);
        Assert.Same(document1, document2);
    }

    [Fact]
    public void GetOpenTelemetryDeclarativeConfiguration_ConfigurationManager_RegistrationTimeRead_ReturnsDocument()
    {
        // The IConfiguration overload must work before a service provider exists, which is the only
        // way to read the document at registration time on the source-only integration path.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        using var configuration = new ConfigurationManager();
        configuration.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path);

        var document = configuration.GetOpenTelemetryDeclarativeConfiguration();

        Assert.NotNull(document);
        Assert.Equal(
            ConfigValueOutcome.Present,
            document.Properties.GetBoolean("disabled").Outcome);
    }

    [Fact]
    public void GetOpenTelemetryDeclarativeConfiguration_ServiceProvider_ParseFailure_Throws()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(fileFormat: "99.0");

        var services = new ServiceCollection();
        new TestOpenTelemetryBuilder(services).UseDeclarativeConfiguration(yamlFile.Path);
        using var sp = services.BuildServiceProvider();

        using var listener = CreateErrorListener();
        var ex1 = Assert.Throws<DeclarativeConfigurationException>(
            () => sp.GetOpenTelemetryDeclarativeConfiguration());
        var ex2 = Assert.Throws<DeclarativeConfigurationException>(
            () => sp.GetOpenTelemetryDeclarativeConfiguration());

        Assert.Same(ex1, ex2);
        Assert.Single(listener.Messages, e => e.EventId == FailedToLoadConfigurationEventId);
    }

    [Fact]
    public void GetOpenTelemetryDeclarativeConfiguration_ConfigurationManager_ParseFailure_Throws()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(fileFormat: "99.0");

        // ConfigurationManager.Add() calls Load() eagerly. The parse failure throws, but the
        // source is still registered in Sources with a Lazy<T> that has cached the exception.
        // Subsequent GetOpenTelemetryDeclarativeConfiguration() calls rethrow the same instance.
        using var configuration = new ConfigurationManager();
        Assert.Throws<DeclarativeConfigurationException>(() =>
            configuration.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path));

        var ex1 = Assert.Throws<DeclarativeConfigurationException>(
            () => configuration.GetOpenTelemetryDeclarativeConfiguration());
        var ex2 = Assert.Throws<DeclarativeConfigurationException>(
            () => configuration.GetOpenTelemetryDeclarativeConfiguration());

        Assert.Same(ex1, ex2);
    }

    [Fact]
    public void GetOpenTelemetryDeclarativeConfiguration_MultipleReachableDocuments_ReturnsWinnerAndWarns()
    {
        using var chainedFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        using var directFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var chainedRoot = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(chainedFile.Path)
            .Build();

        var combined = new ConfigurationBuilder()
            .AddConfiguration(chainedRoot)
            .AddOpenTelemetryDeclarativeConfiguration(directFile.Path)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(combined);

        using var listener = CreateWarningListener();
        using var sp = services.BuildServiceProvider();

        var document = sp.GetOpenTelemetryDeclarativeConfiguration();

        Assert.NotNull(document);
        var disabled = document.Properties.GetBoolean("disabled");
        Assert.Equal(ConfigValueOutcome.Present, disabled.Outcome);
        Assert.False(disabled.Value);
        Assert.Single(listener.Messages, e => e.EventId == MultipleConfigurationDocumentsReachableEventId);
    }

    [Fact]
    public void GetOpenTelemetryDeclarativeConfiguration_DocumentShape_PropertiesNonNullAndConsistent()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml();

        var services = new ServiceCollection();
        new TestOpenTelemetryBuilder(services).UseDeclarativeConfiguration(yamlFile.Path);
        using var sp = services.BuildServiceProvider();

        var document = sp.GetOpenTelemetryDeclarativeConfiguration();

        Assert.NotNull(document);
        Assert.NotNull(document.Properties);
        Assert.Same(document.Properties, document.Properties);
    }

    private static DeclarativeConfigurationDocument GetProviderDocument(IServiceProvider serviceProvider)
    {
        var configuration = Assert.IsType<IConfigurationRoot>(
            serviceProvider.GetRequiredService<IConfiguration>(),
            exactMatch: false);
        var provider = Assert.Single(configuration.Providers.OfType<DeclarativeConfigurationProvider>());
        return provider.Accessor.GetDocumentForProvider();
    }

    private static TestEventListener CreateVerboseListener()
    {
        var listener = new TestEventListener();
        listener.EnableEvents(
            OpenTelemetryDeclarativeConfigurationEventSource.Log,
            EventLevel.Verbose,
            EventKeywords.All);
        return listener;
    }

    private static TestEventListener CreateWarningListener()
    {
        var listener = new TestEventListener();
        listener.EnableEvents(
            OpenTelemetryDeclarativeConfigurationEventSource.Log,
            EventLevel.Warning,
            EventKeywords.All);
        return listener;
    }

    private static TestEventListener CreateErrorListener()
    {
        var listener = new TestEventListener();
        listener.EnableEvents(
            OpenTelemetryDeclarativeConfigurationEventSource.Log,
            EventLevel.Error,
            EventKeywords.All);
        return listener;
    }

    private sealed class TestOpenTelemetryBuilder(IServiceCollection services) : IOpenTelemetryBuilder
    {
        public IServiceCollection Services { get; } = services;
    }
}
