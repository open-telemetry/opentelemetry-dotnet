// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// An <see cref="IConfigurationSource"/> that creates <see cref="DeclarativeConfigurationProvider"/> instances to read OpenTelemetry
/// configuration from a declarative configuration YAML file.
/// </summary>
internal sealed class DeclarativeConfigurationSource : IConfigurationSource
{
    internal DeclarativeConfigurationSource(FilePath filePath)
    {
        this.FilePath = filePath;
    }

    public FilePath FilePath { get; }

    /// <inheritdoc/>
    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new DeclarativeConfigurationProvider(this.FilePath);
}
