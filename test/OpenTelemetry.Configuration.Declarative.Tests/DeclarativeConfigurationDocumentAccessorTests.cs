// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class DeclarativeConfigurationDocumentAccessorTests
{
    private const int FailedToLoadConfigurationEventId = 12;
    private const int DocumentParsedOnDemandEventId = 27;
    private const int DocumentAccessorNotAvailableEventId = 28;
    private const int DifferentSourceAlreadyRegisteredEventId = 29;
    private const int MultipleConfigurationDocumentsReachableEventId = 30;

    [Fact]
    public void GetDocument_BeforeAnyLoad_ParsesAndReturnsDocument()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        var accessor = new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path));

        using var listener = CreateVerboseListener();
        var document = accessor.GetDocument();

        Assert.NotNull(document);
        Assert.NotNull(document.Model);
        Assert.NotNull(document.FlatKeys);
        Assert.Single(listener.Messages, e => e.EventId == DocumentParsedOnDemandEventId);
    }

    [Fact]
    public void Load_AfterGetDocument_UsesSameDocument()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        var accessor = new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path));

        // A typed consumer triggers the parse.
        var documentFromAccessor = accessor.GetDocument();

        // Provider loads after; must not re-parse and must use the same document.
        var provider = new DeclarativeConfigurationProvider(accessor);
        provider.Load();

        var documentFromProvider = accessor.GetDocument();

        Assert.Same(documentFromAccessor, documentFromProvider);
        Assert.True(provider.TryGet(OtelEnvironmentVariables.SdkDisabled, out _));
    }

    [Fact]
    public void GetDocument_CalledMultipleTimes_ReturnsSameInstance()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        var accessor = new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path));

        var first = accessor.GetDocument();
        var second = accessor.GetDocument();
        var third = accessor.GetDocument();

        Assert.Same(first, second);
        Assert.Same(first, third);
    }

    [Fact]
    public void FlatKeys_IsReadOnly_MutationThrows()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        var accessor = new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path));

        var document = accessor.GetDocument();

        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, string?>)document.FlatKeys)["new.key"] = "value");
    }

    [Fact]
    public void ModelCollections_AreReadOnly_MutationThrows()
    {
        const string yaml = """
            file_format: "1.1"
            resource:
              attributes:
                - name: scalar
                  value: value
                - name: sequence
                  type: string_array
                  value: [one, two]
            """;

        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(yaml);
        var accessor = new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path));

        var model = accessor.GetDocument().Model;
        var resource = model.Resource.Value;
        var attributes = resource.Attributes.Value;
        Assert.True(attributes[1].TryGetSequenceValues(out var sequenceValues));

        Assert.Throws<NotSupportedException>(() =>
            ((IList<ResourceAttributeEntry>)attributes).Add(attributes[0]));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<ResolvedYamlScalar>)sequenceValues).Add(sequenceValues[0]));
    }

    [Fact]
    public void GetDocument_ConcurrentAccess_ParsesOnceAndReturnsSameInstance()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        var parseCount = 0;
        var accessor = new DeclarativeConfigurationDocumentAccessor(
            new FilePath(yamlFile.Path),
            filePath =>
            {
                Interlocked.Increment(ref parseCount);
                return DeclarativeConfigurationReader.Read(filePath);
            });

        const int threadCount = 8;
        var results = new DeclarativeConfigurationDocument?[threadCount];
        using var barrier = new Barrier(threadCount);

        var threads = Enumerable.Range(0, threadCount).Select(i => new Thread(() =>
        {
            barrier.SignalAndWait();
            results[i] = accessor.GetDocument();
        })).ToArray();

        foreach (var t in threads)
        {
            t.Start();
        }

        foreach (var t in threads)
        {
            t.Join();
        }

        var first = results[0];
        Assert.NotNull(first);
        Assert.All(results, r => Assert.Same(first, r));
        Assert.Equal(1, parseCount);
    }

    [Fact]
    public async Task GetDocument_ConcurrentWithProviderLoad_ConsumerWinnerEmitsOnDemandEventOnce()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        using var parseStarted = new ManualResetEventSlim();
        using var releaseParse = new ManualResetEventSlim();
        var parseCount = 0;
        var accessor = new DeclarativeConfigurationDocumentAccessor(
            new FilePath(yamlFile.Path),
            filePath =>
            {
                Interlocked.Increment(ref parseCount);
                parseStarted.Set();
                releaseParse.Wait();
                return DeclarativeConfigurationReader.Read(filePath);
            });
        var provider = new DeclarativeConfigurationProvider(accessor);

        using var listener = new ConcurrentEventListener();
        var consumerTask = Task.Run(() => accessor.GetDocument());
        Assert.True(parseStarted.Wait(TimeSpan.FromSeconds(10)));
        var providerTask = Task.Run(provider.Load);
        releaseParse.Set();
        await Task.WhenAll(consumerTask, providerTask);

        Assert.Equal(1, parseCount);
        Assert.Equal(1, listener.GetEventIdCount(DocumentParsedOnDemandEventId));
    }

    [Fact]
    public async Task GetDocument_ConcurrentWithProviderLoad_ProviderWinnerDoesNotEmitOnDemandEvent()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        using var parseStarted = new ManualResetEventSlim();
        using var releaseParse = new ManualResetEventSlim();
        var parseCount = 0;
        var accessor = new DeclarativeConfigurationDocumentAccessor(
            new FilePath(yamlFile.Path),
            filePath =>
            {
                Interlocked.Increment(ref parseCount);
                parseStarted.Set();
                releaseParse.Wait();
                return DeclarativeConfigurationReader.Read(filePath);
            });
        var provider = new DeclarativeConfigurationProvider(accessor);

        using var listener = new ConcurrentEventListener();
        var providerTask = Task.Run(provider.Load);
        Assert.True(parseStarted.Wait(TimeSpan.FromSeconds(10)));
        var consumerTask = Task.Run(() => accessor.GetDocument());
        releaseParse.Set();
        await Task.WhenAll(providerTask, consumerTask);

        Assert.Equal(1, parseCount);
        Assert.Equal(0, listener.GetEventIdCount(DocumentParsedOnDemandEventId));
    }

    [Fact]
    public void GetDocument_InvalidFileFormat_ThrowsDeclarativeConfigurationException()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(fileFormat: "99.0");
        var accessor = new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path));

        Assert.Throws<DeclarativeConfigurationException>(accessor.GetDocument);
    }

    [Fact]
    public void Load_InvalidFileFormat_ThrowsDeclarativeConfigurationException()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(fileFormat: "99.0");
        var accessor = new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path));
        var provider = new DeclarativeConfigurationProvider(accessor);

        Assert.Throws<DeclarativeConfigurationException>(provider.Load);
    }

    [Fact]
    public void GetDocument_ParseFailure_ExceptionIsCachedAndEventEmittedOnce()
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        var path = factory.CreateYamlFile("file_format: \"99.0\"");
        var parseCount = 0;
        var accessor = new DeclarativeConfigurationDocumentAccessor(
            new FilePath(path),
            filePath =>
            {
                Interlocked.Increment(ref parseCount);
                return DeclarativeConfigurationReader.Read(filePath);
            });

        using var listener = CreateErrorListener();

        var ex1 = Assert.Throws<DeclarativeConfigurationException>(accessor.GetDocument);
        var ex2 = Assert.Throws<DeclarativeConfigurationException>(accessor.GetDocument);

        // Lazy caches the exception; both calls surface the same instance.
        Assert.Same(ex1, ex2);
        Assert.Equal(1, parseCount);

        // FailedToLoadConfiguration emitted exactly once, not on each re-throw.
        Assert.Single(listener.Messages, e => e.EventId == FailedToLoadConfigurationEventId);
    }

    [Fact]
    public void GetDocument_MissingFile_IsWrappedAsDeclarativeConfigurationException()
    {
        using var factory = new DeclarativeYamlTestFileFactory();
        var accessor = new DeclarativeConfigurationDocumentAccessor(
            new FilePath(Path.Combine(factory.TempDirectory, "nonexistent.yaml")));

        var ex = Assert.Throws<DeclarativeConfigurationException>(accessor.GetDocument);
        Assert.IsType<FileNotFoundException>(ex.InnerException);
    }

    [Fact]
    public void UseDeclarativeConfiguration_RegistersAccessorAsSingleton()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var services = new ServiceCollection();
        new TestOpenTelemetryBuilder(services).UseDeclarativeConfiguration(yamlFile.Path);

        using var sp = services.BuildServiceProvider();
        var accessor1 = sp.GetService<DeclarativeConfigurationDocumentAccessor>();
        var accessor2 = sp.GetService<DeclarativeConfigurationDocumentAccessor>();

        Assert.NotNull(accessor1);
        Assert.Same(accessor1, accessor2);
    }

    [Fact]
    public void UseDeclarativeConfiguration_AccessorMatchesProviderAccessor()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var services = new ServiceCollection();
        new TestOpenTelemetryBuilder(services).UseDeclarativeConfiguration(yamlFile.Path);

        using var sp = services.BuildServiceProvider();
        var config = sp.GetRequiredService<IConfiguration>();
        var root = Assert.IsType<IConfigurationRoot>(config, exactMatch: false);
        var provider = root.Providers
            .OfType<DeclarativeConfigurationProvider>()
            .Single();

        var registeredAccessor = sp.GetRequiredService<DeclarativeConfigurationDocumentAccessor>();

        Assert.Same(provider.Accessor, registeredAccessor);
    }

    [Fact]
    public void UseDeclarativeConfiguration_SourceAlreadyOnConfigurationManager_ReusesAccessor()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        using var configuration = new ConfigurationManager();
        configuration.AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path);

        var providerAccessor = configuration.Sources
            .OfType<DeclarativeConfigurationSource>()
            .Single()
            .Accessor;
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        new TestOpenTelemetryBuilder(services).UseDeclarativeConfiguration(yamlFile.Path);

        using var serviceProvider = services.BuildServiceProvider();
        var registeredAccessor =
            serviceProvider.GetRequiredService<DeclarativeConfigurationDocumentAccessor>();

        Assert.Same(providerAccessor, registeredAccessor);
        Assert.Single(configuration.Sources.OfType<DeclarativeConfigurationSource>());
    }

    [Fact]
    public void UseDeclarativeConfiguration_SourceAlreadyOnConfigurationRoot_ReusesAccessor()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        var configuration = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();
        var providerAccessor = configuration.Providers
            .OfType<DeclarativeConfigurationProvider>()
            .Single()
            .Accessor;
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        new TestOpenTelemetryBuilder(services).UseDeclarativeConfiguration(yamlFile.Path);

        using var serviceProvider = services.BuildServiceProvider();
        var registeredAccessor =
            serviceProvider.GetRequiredService<DeclarativeConfigurationDocumentAccessor>();

        Assert.Same(providerAccessor, registeredAccessor);
    }

    [Fact]
    public void UseDeclarativeConfiguration_DifferentSourceAlreadyOnConfigurationRoot_FirstAccessorWins()
    {
        using var yamlFile1 = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        using var yamlFile2 = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);
        var configuration = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile1.Path)
            .Build();
        var providerAccessor = configuration.Providers
            .OfType<DeclarativeConfigurationProvider>()
            .Single()
            .Accessor;
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        new TestOpenTelemetryBuilder(services).UseDeclarativeConfiguration(yamlFile2.Path);

        using var listener = CreateWarningListener();
        using var serviceProvider = services.BuildServiceProvider();
        var resolvedConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
        var registeredAccessor =
            serviceProvider.GetRequiredService<DeclarativeConfigurationDocumentAccessor>();

        Assert.Same(providerAccessor, registeredAccessor);
        Assert.Equal("true", resolvedConfiguration[OtelEnvironmentVariables.SdkDisabled]);
        var warning = Assert.Single(listener.Messages, e => e.EventId == DifferentSourceAlreadyRegisteredEventId);
        Assert.Equal(yamlFile1.Path, warning.Payload![0]);
        Assert.Equal(yamlFile2.Path, warning.Payload[1]);
    }

    [Fact]
    public void UseDeclarativeConfiguration_SourceReturnedByConfigurationFactory_ReusesAccessor()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        var configuration = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();
        var providerAccessor = configuration.Providers
            .OfType<DeclarativeConfigurationProvider>()
            .Single()
            .Accessor;
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(_ => configuration);
        new TestOpenTelemetryBuilder(services).UseDeclarativeConfiguration(yamlFile.Path);

        using var serviceProvider = services.BuildServiceProvider();

        // Resolve the accessor first. Its registration must force IConfiguration construction
        // before choosing between the new candidate and the provider's existing accessor.
        var registeredAccessor =
            serviceProvider.GetRequiredService<DeclarativeConfigurationDocumentAccessor>();

        Assert.Same(providerAccessor, registeredAccessor);
    }

    [Fact]
    public void UseDeclarativeConfiguration_SecondCall_IsNoOpAndFirstAccessorWins()
    {
        using var yamlFile1 = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        using var yamlFile2 = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var services = new ServiceCollection();
        var builder = new TestOpenTelemetryBuilder(services);
        builder.UseDeclarativeConfiguration(yamlFile1.Path);
        builder.UseDeclarativeConfiguration(yamlFile2.Path); // no-op

        using var sp = services.BuildServiceProvider();
        var accessor = sp.GetRequiredService<DeclarativeConfigurationDocumentAccessor>();

        Assert.Equal(yamlFile1.Path, accessor.FilePath.DisplayPath);
    }

    [Fact]
    public void Resolver_SourceAddedViaBuilderExtension_FindsProviderAccessor()
    {
        // The scan fallback recovers the accessor when UseDeclarativeConfiguration was never called,
        // which is the only route available to IConfigurationBuilder - it has no IServiceCollection.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var configRoot = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();
        var providerAccessor = configRoot.Providers
            .OfType<DeclarativeConfigurationProvider>()
            .Single()
            .Accessor;

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configRoot);

        using var sp = services.BuildServiceProvider();
        var accessor = DeclarativeConfigurationDocumentAccessorResolver.Find(sp);

        Assert.Same(providerAccessor, accessor);
    }

    [Fact]
    public void Resolver_SourceBehindChainedConfiguration_FindsProviderAccessor()
    {
        // A configuration composed from another root exposes the YAML keys but reaches the
        // declarative provider only through ChainedConfigurationProvider, so the scan recurses.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var yamlRoot = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();
        var providerAccessor = yamlRoot.Providers
            .OfType<DeclarativeConfigurationProvider>()
            .Single()
            .Accessor;
        var chained = new ConfigurationBuilder().AddConfiguration(yamlRoot).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(chained);
        services.AddRepresentativeDeclarativeConfigurationConsumer();

        using var sp = services.BuildServiceProvider();
        var consumer = sp.GetRequiredService<RepresentativeDeclarativeConfigurationConsumer>();

        Assert.Equal("true", chained[OtelEnvironmentVariables.SdkDisabled]);
        Assert.Same(providerAccessor, consumer.Accessor);
        Assert.Same(providerAccessor.GetDocument(), consumer.Document);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Resolver_ChainedAndDirectSources_SelectsHighestPrecedenceDocumentAndWarns(bool chainedFirst)
    {
        // The single-source guard cannot see a declarative source that arrives through
        // AddConfiguration, so two documents can coexist. Flat keys then resolve from the last
        // provider that supplies them, so the resolver must select the same document, whichever
        // order the two were registered in. Otherwise flat and typed consumers disagree.
        using var chainedFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        using var directFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var chainedRoot = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(chainedFile.Path)
            .Build();

        var builder = new ConfigurationBuilder();
        if (chainedFirst)
        {
            builder.AddConfiguration(chainedRoot).AddOpenTelemetryDeclarativeConfiguration(directFile.Path);
        }
        else
        {
            builder.AddOpenTelemetryDeclarativeConfiguration(directFile.Path).AddConfiguration(chainedRoot);
        }

        var configuration = builder.Build();

        var chainedAccessor = chainedRoot.Providers
            .OfType<DeclarativeConfigurationProvider>()
            .Single()
            .Accessor;
        var directAccessor = configuration.Providers
            .OfType<DeclarativeConfigurationProvider>()
            .Single()
            .Accessor;

        var expected = chainedFirst ? directAccessor : chainedAccessor;
        var ignored = chainedFirst ? chainedAccessor : directAccessor;

        using var listener = CreateWarningListener();

        var resolved = DeclarativeConfigurationDocumentAccessorResolver.FindInConfiguration(configuration);

        Assert.Same(expected, resolved);

        // The selected document is the one whose flat value the application observes.
        var expectedFlatValue = chainedFirst ? "false" : "true";
        Assert.Equal(expectedFlatValue, configuration[OtelEnvironmentVariables.SdkDisabled]);
        Assert.Equal(
            expectedFlatValue,
            resolved!.GetDocument().FlatKeys[OtelEnvironmentVariables.SdkDisabled]);

        var warning = Assert.Single(listener.Messages, e => e.EventId == MultipleConfigurationDocumentsReachableEventId);
        Assert.Equal(expected.FilePath.DisplayPath, warning.Payload![0]);
        Assert.Equal(ignored.FilePath.DisplayPath, warning.Payload![1]);
    }

    [Fact]
    public void Resolver_SameConfigurationChainedTwice_IsOneDocumentAndDoesNotWarn()
    {
        // One document reachable by two routes is not an ambiguous configuration.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var yamlRoot = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();
        var providerAccessor = yamlRoot.Providers
            .OfType<DeclarativeConfigurationProvider>()
            .Single()
            .Accessor;

        var configuration = new ConfigurationBuilder()
            .AddConfiguration(yamlRoot)
            .AddConfiguration(yamlRoot)
            .Build();

        using var listener = CreateWarningListener();

        Assert.Same(
            providerAccessor,
            DeclarativeConfigurationDocumentAccessorResolver.FindInConfiguration(configuration));
        Assert.DoesNotContain(listener.Messages, e => e.EventId == MultipleConfigurationDocumentsReachableEventId);
    }

    [Fact]
    public void Resolver_NoDeclarativeProviderInConfigRoot_ReturnsNullAndLogs()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        using var listener = CreateVerboseListener();
        using var sp = services.BuildServiceProvider();

        Assert.Null(DeclarativeConfigurationDocumentAccessorResolver.Find(sp));
        Assert.Single(listener.Messages, e => e.EventId == DocumentAccessorNotAvailableEventId);
    }

    [Fact]
    public void Resolver_NonRootConfiguration_ReturnsNullAndLogs()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new NonRootConfiguration());

        using var listener = CreateVerboseListener();
        using var sp = services.BuildServiceProvider();

        Assert.Null(DeclarativeConfigurationDocumentAccessorResolver.Find(sp));
        Assert.Single(listener.Messages, e => e.EventId == DocumentAccessorNotAvailableEventId);
    }

    [Fact]
    public void Resolver_NoConfigurationRegisteredAtAll_ReturnsNullAndDoesNotThrow()
    {
        using var sp = new ServiceCollection().BuildServiceProvider();

        Assert.Null(DeclarativeConfigurationDocumentAccessorResolver.Find(sp));
    }

    [Fact]
    public void Resolver_ExplicitRegistrationTakesPrecedenceOverScan()
    {
        // An accessor registered by UseDeclarativeConfiguration wins over whatever a scan of the
        // configuration would turn up. The two views can still disagree, as they do here: nothing
        // stops an application replacing its IConfiguration registration, or clearing its sources,
        // after the accessor was registered. Explicit registration wins by design because it is the
        // caller's stated intent; the flat overlay is what is lost in that case.
        using var yamlFile1 = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        using var yamlFile2 = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);

        var scannableRoot = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile1.Path)
            .Build();
        var explicitAccessor = new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile2.Path));

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(scannableRoot);
        services.AddSingleton(explicitAccessor);

        using var sp = services.BuildServiceProvider();

        Assert.Same(explicitAccessor, DeclarativeConfigurationDocumentAccessorResolver.Find(sp));
    }

    [Fact]
    public void RepresentativeConsumer_BuilderSource_UsesProviderAccessorAndDocument()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        var configuration = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();
        var providerAccessor = configuration.Providers
            .OfType<DeclarativeConfigurationProvider>()
            .Single()
            .Accessor;
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddRepresentativeDeclarativeConfigurationConsumer();

        using var serviceProvider = services.BuildServiceProvider();
        var consumer =
            serviceProvider.GetRequiredService<RepresentativeDeclarativeConfigurationConsumer>();

        Assert.Same(providerAccessor, consumer.Accessor);
        Assert.Same(providerAccessor.GetDocument(), consumer.Document);
    }

    [Fact]
    public void RepresentativeConsumer_SecondBuilderSourceAttempt_UsesFirstProviderAccessorAndDocument()
    {
        using var yamlFile1 = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        using var yamlFile2 = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: false);
        var configuration = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile1.Path)
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile2.Path)
            .Build();
        var provider = configuration.Providers
            .OfType<DeclarativeConfigurationProvider>()
            .Single();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddRepresentativeDeclarativeConfigurationConsumer();

        using var serviceProvider = services.BuildServiceProvider();
        var consumer =
            serviceProvider.GetRequiredService<RepresentativeDeclarativeConfigurationConsumer>();

        Assert.Equal(yamlFile1.Path, provider.FilePath.DisplayPath);
        Assert.Same(provider.Accessor, consumer.Accessor);
        Assert.Same(provider.Accessor.GetDocument(), consumer.Document);
        var document = Assert.IsType<DeclarativeConfigurationDocument>(consumer.Document);
        Assert.Equal(
            "true",
            document.FlatKeys[OtelEnvironmentVariables.SdkDisabled]);
    }

    [Fact]
    public void RepresentativeConsumer_ExplicitRegistration_UsesSingleProviderAccessor()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        var services = new ServiceCollection();
        services.AddRepresentativeDeclarativeConfigurationConsumer();
        new TestOpenTelemetryBuilder(services).UseDeclarativeConfiguration(yamlFile.Path);

        using var serviceProvider = services.BuildServiceProvider();
        var configuration = Assert.IsType<IConfigurationRoot>(
            serviceProvider.GetRequiredService<IConfiguration>(), exactMatch: false);
        var providerAccessor = configuration.Providers
            .OfType<DeclarativeConfigurationProvider>()
            .Single()
            .Accessor;
        var consumer =
            serviceProvider.GetRequiredService<RepresentativeDeclarativeConfigurationConsumer>();

        Assert.Same(providerAccessor, consumer.Accessor);
        Assert.Same(providerAccessor.GetDocument(), consumer.Document);
    }

    [Fact]
    public void RepresentativeConsumer_MissingProvider_HasNoDocumentAndDoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddRepresentativeDeclarativeConfigurationConsumer();

        using var listener = CreateVerboseListener();
        using var serviceProvider = services.BuildServiceProvider();
        var consumer =
            serviceProvider.GetRequiredService<RepresentativeDeclarativeConfigurationConsumer>();

        Assert.Null(consumer.Accessor);
        Assert.Null(consumer.Document);
        Assert.Single(listener.Messages, e => e.EventId == DocumentAccessorNotAvailableEventId);
    }

    [Fact]
    public void Load_DoesNotEmitDocumentParsedOnDemand_WhenAccessorWasAlreadyQueried()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        var accessor = new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path));

        // A typed consumer queries the accessor before Load() runs.
        using var listener = CreateVerboseListener();
        _ = accessor.GetDocument();
        Assert.Single(listener.Messages, e => e.EventId == DocumentParsedOnDemandEventId);

        var provider = new DeclarativeConfigurationProvider(accessor);
        provider.Load();

        Assert.Single(listener.Messages, e => e.EventId == DocumentParsedOnDemandEventId);
    }

    [Fact]
    public void GetDocument_DoesNotEmitDocumentParsedOnDemand_WhenLoadRanFirst()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        var accessor = new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path));
        var provider = new DeclarativeConfigurationProvider(accessor);

        using var listener = CreateVerboseListener();
        provider.Load();
        _ = accessor.GetDocument();

        Assert.DoesNotContain(listener.Messages, e => e.EventId == DocumentParsedOnDemandEventId);
    }

    [Fact]
    public void Load_OnSecondProviderSharingAccessor_DoesNotEmitDocumentParsedOnDemand()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        var accessor = new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path));
        var firstProvider = new DeclarativeConfigurationProvider(accessor);
        var secondProvider = new DeclarativeConfigurationProvider(accessor);

        using var listener = CreateVerboseListener();
        firstProvider.Load();
        secondProvider.Load();

        Assert.DoesNotContain(listener.Messages, e => e.EventId == DocumentParsedOnDemandEventId);
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

    private sealed class NonRootConfiguration : IConfiguration
    {
        private readonly IConfiguration inner = new ConfigurationBuilder().Build();

        public string? this[string key]
        {
            get => this.inner[key];
            set => this.inner[key] = value;
        }

        public IEnumerable<IConfigurationSection> GetChildren() => this.inner.GetChildren();

        public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() =>
            this.inner.GetReloadToken();

        public IConfigurationSection GetSection(string key) => this.inner.GetSection(key);
    }

    private sealed class ConcurrentEventListener : EventListener
    {
        private readonly ConcurrentQueue<int> eventIds = new();

        internal ConcurrentEventListener()
        {
            this.EnableEvents(
                OpenTelemetryDeclarativeConfigurationEventSource.Log,
                EventLevel.Verbose,
                EventKeywords.All);
        }

        internal int GetEventIdCount(int eventId) => this.eventIds.Count(id => id == eventId);

        protected override void OnEventWritten(EventWrittenEventArgs eventData) =>
            this.eventIds.Enqueue(eventData.EventId);
    }
}
