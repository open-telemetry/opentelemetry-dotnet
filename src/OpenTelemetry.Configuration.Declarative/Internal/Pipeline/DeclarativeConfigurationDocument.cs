// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The parsed result of a declarative configuration document, produced from a single file read.
/// </summary>
public sealed class DeclarativeConfigurationDocument
{
    internal DeclarativeConfigurationDocument(
        DeclarativeConfiguration model,
        ReadOnlyDictionary<string, string?> flatKeys,
        ConfigProperties properties)
    {
        this.Model = model;
        this.FlatKeys = flatKeys;
        this.Properties = properties;
    }

    /// <summary>
    /// Gets a schemaless view of every key the document contained.
    /// </summary>
    public ConfigProperties Properties { get; }

    /// <summary>
    /// Gets the typed configuration model.
    /// </summary>
    internal DeclarativeConfiguration Model { get; }

    /// <summary>
    /// Gets the flat <c>OTEL_*</c> key projection derived from <see cref="Model"/>.
    /// </summary>
    internal ReadOnlyDictionary<string, string?> FlatKeys { get; }
}
