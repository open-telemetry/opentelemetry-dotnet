// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative.Tests;

internal sealed class DeclarativeYamlTestFile : IDisposable
{
    private readonly DeclarativeYamlTestFileFactory factory;

    private DeclarativeYamlTestFile(DeclarativeYamlTestFileFactory factory, string path)
    {
        this.factory = factory;
        this.Path = path;
    }

    public string Path { get; }

    public static DeclarativeYamlTestFile CreateYamlFile(string yaml)
    {
        var factory = new DeclarativeYamlTestFileFactory();
        var path = factory.CreateYamlFile(yaml);
        return new DeclarativeYamlTestFile(factory, path);
    }

    public static DeclarativeYamlTestFile CreateDeclarativeYaml(
        string fileFormat = "1.0",
        bool? disabled = null,
        IReadOnlyDictionary<string, string>? resourceAttributes = null)
    {
        var factory = new DeclarativeYamlTestFileFactory();
        var path = factory.CreateDeclarativeYaml(fileFormat, disabled, resourceAttributes);
        return new DeclarativeYamlTestFile(factory, path);
    }

    public void Dispose() => this.factory.Dispose();
}
