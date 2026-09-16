// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Extension methods for <see cref="YamlScalarKind"/>.
/// </summary>
internal static class YamlScalarKindExtensions
{
    /// <summary>
    /// Returns the YAML 1.2 core schema name for <paramref name="kind"/>, as used in messages
    /// shown to the author of a configuration document.
    /// </summary>
    /// <param name="kind">The kind to name.</param>
    /// <returns>The lower-case schema name.</returns>
    internal static string GetYamlKindName(this YamlScalarKind kind) => kind switch
    {
        YamlScalarKind.Boolean => "boolean",
        YamlScalarKind.Float => "float",
        YamlScalarKind.Integer => "integer",
        YamlScalarKind.Null => "null",
        YamlScalarKind.String or _ => "string",
    };
}
