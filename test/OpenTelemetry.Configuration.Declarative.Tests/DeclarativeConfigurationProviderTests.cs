// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class DeclarativeConfigurationProviderTests
{
    [Fact]
    public void Load_MissingFile_ThrowsDeclarativeConfigurationException()
    {
        using var yamlFile = new DeclarativeYamlTestFileFactory();
        var provider = new DeclarativeConfigurationProvider(new DeclarativeConfigurationDocumentAccessor(new FilePath(Path.Combine(yamlFile.TempDirectory, "nonexistent.yaml"))));

        var ex = Assert.Throws<DeclarativeConfigurationException>(provider.Load);
        Assert.IsType<FileNotFoundException>(ex.InnerException);
    }

    [Fact]
    public void Load_ValidFile_PopulatesFlatKeys()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            disabled: true,
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "my-service" });

        var provider = new DeclarativeConfigurationProvider(new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path)));
        provider.Load();

        Assert.True(provider.TryGet(OtelEnvironmentVariables.SdkDisabled, out var disabled));
        Assert.Equal("true", disabled);

        Assert.True(provider.TryGet(OtelEnvironmentVariables.ResourceAttributes, out var attrs));
        Assert.Equal("service.name=my-service", attrs);
    }

    [Fact]
    public void Load_EmptyFile_LeavesDataEmpty()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(string.Empty);
        var provider = new DeclarativeConfigurationProvider(new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path)));
        provider.Load();

        Assert.False(provider.TryGet(OtelEnvironmentVariables.SdkDisabled, out _));
        Assert.False(provider.TryGet(OtelEnvironmentVariables.ResourceAttributes, out _));
    }

    [Fact]
    public void Load_SecondLoad_LeavesDataUnchangedAndLogs()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        var yamlWithoutDisabled = """
            file_format: "1.0"
            """;

        using var listener = new TestEventListener();
        listener.EnableEvents(
            OpenTelemetryDeclarativeConfigurationEventSource.Log,
            EventLevel.Warning,
            EventKeywords.All);

        var accessor = new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path));
        var provider = new DeclarativeConfigurationProvider(accessor);
        provider.Load();
        Assert.True(provider.TryGet(OtelEnvironmentVariables.SdkDisabled, out _));

        // Rewrite the file; the second Load() must ignore the change.
        File.WriteAllText(yamlFile.Path, yamlWithoutDisabled);
        provider.Load();

        // Key still present - second load was a no-op.
        Assert.True(provider.TryGet(OtelEnvironmentVariables.SdkDisabled, out _));
        Assert.Single(listener.Messages, e => e.EventId == 26);
    }

    [Fact]
    public void Load_SecondLoad_DoesNotReparse()
    {
        var parseCount = 0;
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var accessor = new DeclarativeConfigurationDocumentAccessor(
            new FilePath(yamlFile.Path),
            filePath =>
            {
                Interlocked.Increment(ref parseCount);
                return DeclarativeConfigurationReader.Read(filePath);
            });
        var provider = new DeclarativeConfigurationProvider(accessor);

        provider.Load();
        provider.Load();

        Assert.Equal(1, parseCount);
    }

    [Fact]
    public void Load_SetsDataToTheDocumentsFlatKeysWithoutCopying()
    {
        // Identity, not equivalence: the provider must publish the document's own dictionary, so that
        // every provider sharing an accessor exposes one projection rather than a copy per provider.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        var accessor = new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path));
        var provider = new DeclarativeConfigurationProvider(accessor);

        provider.Load();

        // Data is protected on ConfigurationProvider and the provider is sealed, so there is no
        // subclass seam; reflection is the only way to assert on the instance itself.
        var data = typeof(ConfigurationProvider)
            .GetProperty("Data", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(provider);

        Assert.Same(accessor.GetDocument().FlatKeys, data);
    }

    [Fact]
    public void Load_ProviderRebuiltFromSameSource_ReusesDocumentWithoutReparsing()
    {
        // ConfigurationManager rebuilds its providers when the sources collection is mutated other
        // than by appending. The rebuilt provider shares the accessor, so the file is not re-read.
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        var parseCount = 0;
        var accessor = new DeclarativeConfigurationDocumentAccessor(
            new FilePath(yamlFile.Path),
            filePath =>
            {
                Interlocked.Increment(ref parseCount);
                return DeclarativeConfigurationReader.Read(filePath);
            });

        using var manager = new ConfigurationManager();
        manager.AddOpenTelemetryDeclarativeConfiguration(accessor);
        var originalProvider = ((IConfigurationRoot)manager).Providers
            .OfType<DeclarativeConfigurationProvider>()
            .Single();

        manager.Sources.Insert(0, new MemoryConfigurationSource());

        var rebuiltProvider = ((IConfigurationRoot)manager).Providers
            .OfType<DeclarativeConfigurationProvider>()
            .Single();

        Assert.NotSame(originalProvider, rebuiltProvider);
        Assert.Same(accessor, rebuiltProvider.Accessor);
        Assert.Equal("true", manager[OtelEnvironmentVariables.SdkDisabled]);
        Assert.Equal(1, parseCount);
    }

    [Fact]
    public void Reload_OnConfigurationRoot_DoesNotThrow()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);

        var root = new ConfigurationBuilder()
            .AddOpenTelemetryDeclarativeConfiguration(yamlFile.Path)
            .Build();

        // IConfigurationRoot.Reload() must not throw even though the provider ignores it.
        var ex = Record.Exception(root.Reload);
        Assert.Null(ex);
    }

    [Fact]
    public void Load_InvalidFileFormat_ThrowsDeclarativeConfigurationException()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(fileFormat: "99.0");
        var provider = new DeclarativeConfigurationProvider(new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path)));

        Assert.Throws<DeclarativeConfigurationException>(provider.Load);
    }

    [Fact]
    public void Load_SubstitutesThenTranslates()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: ${SERVICE_NAME:-default-svc}
            """;

        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(yaml);
        var provider = new DeclarativeConfigurationProvider(new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path)));
        provider.Load();

        Assert.True(provider.TryGet(OtelEnvironmentVariables.ResourceAttributes, out var attrs));
        Assert.Equal("service.name=default-svc", attrs);
    }

    [Fact]
    public void Load_InvalidSubstitutionInResourceValue_ThrowsDeclarativeConfigurationException()
    {
        const string yaml = """
            file_format: "1.0"
            resource:
              attributes:
                - name: service.name
                  value: ${1INVALID}
            """;

        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(yaml);
        var provider = new DeclarativeConfigurationProvider(new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path)));

        Assert.Throws<DeclarativeConfigurationException>(provider.Load);
    }

    [Fact]
    public void Load_FileFormat04_ThrowsDeclarativeConfigurationException()
    {
        const string yaml = """
            file_format: "0.4"
            """;

        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(yaml);
        var provider = new DeclarativeConfigurationProvider(new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path)));

        Assert.Throws<DeclarativeConfigurationException>(provider.Load);
    }

    [Fact]
    public void Load_InvalidYamlSyntax_ThrowsDeclarativeConfigurationException()
    {
        const string yaml = "{ unclosed: [bracket";

        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(yaml);
        var provider = new DeclarativeConfigurationProvider(new DeclarativeConfigurationDocumentAccessor(new FilePath(yamlFile.Path)));

        var ex = Assert.Throws<DeclarativeConfigurationException>(provider.Load);
        Assert.NotNull(ex.InnerException);
        Assert.IsType<YamlDotNet.Core.YamlException>(ex.InnerException, exactMatch: false);
    }
}
