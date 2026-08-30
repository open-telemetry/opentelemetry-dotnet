// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// An <see cref="IConfigurationSource"/> that creates <see cref="DeclarativeConfigurationProvider"/> instances
/// backed by a shared <see cref="DeclarativeConfigurationDocumentAccessor"/>.
/// </summary>
internal sealed class DeclarativeConfigurationSource(DeclarativeConfigurationDocumentAccessor accessor) : IConfigurationSource
{
    internal FilePath FilePath => this.Accessor.FilePath;

    internal DeclarativeConfigurationDocumentAccessor Accessor { get; } = accessor;

    // Build returns a new provider per call, so a source built into more than one
    // configuration root yields more than one provider. Sharing the accessor is what keeps them
    // reading one document produced by one parse.

    /// <inheritdoc/>
    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new DeclarativeConfigurationProvider(this.Accessor);
}
