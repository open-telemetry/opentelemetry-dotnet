// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.ObjectModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The parsed result of a declarative configuration document, produced from a single file read.
/// </summary>
public sealed class DeclarativeConfigurationDocument
{
    private readonly Dictionary<string, string?> referencedEnvironmentVariables;

    internal DeclarativeConfigurationDocument(
        DeclarativeConfiguration model,
        ReadOnlyDictionary<string, string?> flatKeys,
        ConfigProperties properties,
        Dictionary<string, string?>? referencedEnvironmentVariables = null)
    {
        this.Model = model;
        this.FlatKeys = flatKeys;
        this.Properties = properties;
        this.referencedEnvironmentVariables = referencedEnvironmentVariables
            ?? [with(OtelEnvironmentVariables.NameComparer)];
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

    /// <summary>
    /// Determines whether the document references an environment variable through substitution.
    /// </summary>
    /// <param name="name">The environment variable name.</param>
    /// <returns>
    /// <see langword="true"/> when the document imports the variable; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    internal bool ReferencesEnvironmentVariable(string name) =>
        this.referencedEnvironmentVariables.ContainsKey(name);

    /// <summary>
    /// Gets the value an environment variable resolved to when the document was read.
    /// </summary>
    /// <param name="name">The environment variable name.</param>
    /// <param name="value">
    /// The resolved value, or <see langword="null"/> if the variable was not set.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the document references the variable; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    internal bool TryGetReferencedEnvironmentVariable(string name, out string? value) =>
        this.referencedEnvironmentVariables.TryGetValue(name, out value);
}
