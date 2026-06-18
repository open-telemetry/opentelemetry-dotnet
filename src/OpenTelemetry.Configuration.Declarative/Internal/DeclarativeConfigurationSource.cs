// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// An <see cref="IConfigurationSource"/> that creates <see cref="DeclarativeConfigurationProvider"/> instances to read OpenTelemetry
/// configuration from a declarative configuration YAML file.
/// </summary>
internal sealed class DeclarativeConfigurationSource(FilePath filePath) : IConfigurationSource
{
    public FilePath FilePath { get; } = filePath;

    /// <inheritdoc/>
    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new DeclarativeConfigurationProvider(this.FilePath);
}
