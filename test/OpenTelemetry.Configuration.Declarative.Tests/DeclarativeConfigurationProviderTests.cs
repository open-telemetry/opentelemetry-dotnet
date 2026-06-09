// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#pragma warning disable OTEL1006

using Xunit;

namespace OpenTelemetry.Configuration.Declarative.Tests;

public sealed class DeclarativeConfigurationProviderTests
{
    [Fact]
    public void Load_MissingFile_ThrowsDeclarativeConfigurationException()
    {
        using var yamlFile = new DeclarativeYamlTestFileFactory();
        var provider = new DeclarativeConfigurationProvider(new FilePath(Path.Combine(yamlFile.TempDirectory, "nonexistent.yaml")));

        var ex = Assert.Throws<DeclarativeConfigurationException>(() => provider.Load());
        Assert.IsType<FileNotFoundException>(ex.InnerException);
    }

    [Fact]
    public void Load_ValidFile_PopulatesFlatKeys()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(
            disabled: true,
            resourceAttributes: new Dictionary<string, string> { ["service.name"] = "my-service" });

        var provider = new DeclarativeConfigurationProvider(new FilePath(yamlFile.Path));
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
        var provider = new DeclarativeConfigurationProvider(new FilePath(yamlFile.Path));
        provider.Load();

        Assert.False(provider.TryGet(OtelEnvironmentVariables.SdkDisabled, out _));
        Assert.False(provider.TryGet(OtelEnvironmentVariables.ResourceAttributes, out _));
    }

    [Fact]
    public void Load_SecondLoad_ReplacesDataSoRemovedYamlKeysDoNotPersist()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(disabled: true);
        var yamlWithoutDisabled = """
            file_format: "1.0"
            """;

        var provider = new DeclarativeConfigurationProvider(new FilePath(yamlFile.Path));
        provider.Load();
        Assert.True(provider.TryGet(OtelEnvironmentVariables.SdkDisabled, out _));

        File.WriteAllText(yamlFile.Path, yamlWithoutDisabled);
        provider.Load();

        Assert.False(provider.TryGet(OtelEnvironmentVariables.SdkDisabled, out _));
    }

    [Fact]
    public void Load_InvalidFileFormat_ThrowsDeclarativeConfigurationException()
    {
        using var yamlFile = DeclarativeYamlTestFile.CreateDeclarativeYaml(fileFormat: "99.0");
        var provider = new DeclarativeConfigurationProvider(new FilePath(yamlFile.Path));

        Assert.Throws<DeclarativeConfigurationException>(() => provider.Load());
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
        var provider = new DeclarativeConfigurationProvider(new FilePath(yamlFile.Path));
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
        var provider = new DeclarativeConfigurationProvider(new FilePath(yamlFile.Path));

        Assert.Throws<DeclarativeConfigurationException>(() => provider.Load());
    }

    [Fact]
    public void Load_FileFormat04_DoesNotThrow()
    {
        const string yaml = """
            file_format: "0.4"
            """;

        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(yaml);
        var provider = new DeclarativeConfigurationProvider(new FilePath(yamlFile.Path));

        provider.Load(); // must not throw
    }

    [Fact]
    public void Load_InvalidYamlSyntax_ThrowsDeclarativeConfigurationException()
    {
        const string yaml = "{ unclosed: [bracket";

        using var yamlFile = DeclarativeYamlTestFile.CreateYamlFile(yaml);
        var provider = new DeclarativeConfigurationProvider(new FilePath(yamlFile.Path));

        var ex = Assert.Throws<DeclarativeConfigurationException>(() => provider.Load());
        Assert.NotNull(ex.InnerException);
        Assert.IsType<YamlDotNet.Core.YamlException>(ex.InnerException, exactMatch: false);
    }

}
