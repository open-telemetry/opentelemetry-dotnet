// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Low-level helpers for walking a YamlDotNet <see cref="YamlMappingNode"/> by key.
/// </summary>
/// <remarks>
/// Children are iterated and key strings compared directly (Ordinal) to avoid relying on
/// <see cref="YamlScalarNode"/> equality/hashing behaviour across YamlDotNet versions.
/// Environment-variable substitution (<c>${VAR}</c> etc.) is applied to scalar values as they are
/// read, so the typed model holds post-substitution values.
/// </remarks>
internal static class YamlNodeReader
{
    /// <summary>
    /// Returns the raw value node for <paramref name="key"/>, or <see langword="null"/> if the key is absent.
    /// The node is returned regardless of its type, so callers can distinguish scalars, sequences and mappings.
    /// </summary>
    /// <param name="node">The mapping to search.</param>
    /// <param name="key">The key to find.</param>
    /// <returns>The value node, or <see langword="null"/> if the key is absent.</returns>
    public static YamlNode? GetValueNode(this YamlMappingNode node, string key)
    {
        foreach (var entry in node.Children)
        {
            if (entry.Key is YamlScalarNode keyNode &&
                string.Equals(keyNode.Value, key, StringComparison.Ordinal))
            {
                return entry.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the substituted scalar value for <paramref name="key"/>, or <see langword="null"/> if the key
    /// is absent, its value node is not a scalar, or its scalar value is null.
    /// </summary>
    /// <param name="node">The mapping to search.</param>
    /// <param name="key">The key to find.</param>
    /// <returns>The substituted scalar value, or <see langword="null"/> if absent, non-scalar, or YAML null.</returns>
    public static string? GetScalarString(this YamlMappingNode node, string key) =>
        node.GetValueNode(key) is not YamlScalarNode { Value: not null } scalar ? null : scalar.GetScalarString();

    /// <summary>
    /// Returns the substituted scalar value, or <see langword="null"/> if the scalar resolves to YAML null.
    /// </summary>
    /// <param name="scalar">The scalar node.</param>
    /// <returns>The substituted scalar value, or <see langword="null"/>.</returns>
    public static string? GetScalarString(this YamlScalarNode scalar)
    {
        if (scalar.Value is null)
        {
            return null;
        }

        var value = EnvironmentSubstitution.Substitute(scalar.Value);
        return IsPlainNullScalar(scalar, value)
            ? null
            : value;
    }

    // YAML 1.2 core schema null values (plain style only): empty, ~, null, Null, NULL.
    private static bool IsPlainNullScalar(YamlScalarNode scalar, string substitutedValue) =>
        scalar.Style == ScalarStyle.Plain &&
            (substitutedValue.Length == 0 ||
                string.Equals(substitutedValue, "~", StringComparison.Ordinal) ||
                string.Equals(substitutedValue, "null", StringComparison.Ordinal) ||
                string.Equals(substitutedValue, "Null", StringComparison.Ordinal) ||
                string.Equals(substitutedValue, "NULL", StringComparison.Ordinal));
}
