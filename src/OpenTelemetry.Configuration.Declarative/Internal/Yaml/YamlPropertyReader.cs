// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Extension methods that read typed values from a <see cref="YamlMappingNode"/> into <see cref="ConfigProperty{T}"/> results.
/// </summary>
/// <remarks>
/// All type resolution here is delegated to <see cref="Yaml12ScalarResolver"/> so that the two
/// readers cannot disagree about what a given piece of text means. Substitution always runs before
/// core-schema type resolution.
/// </remarks>
internal static class YamlPropertyReader
{
    internal static ConfigProperty<bool> ReadBoolean(this YamlMappingNode node, string key)
    {
        var valueNode = node.GetValueNode(key);
        if (valueNode is null)
        {
            return ConfigProperty<bool>.Absent;
        }

        var scalar = RequireScalar(valueNode, key);
        var resolved = scalar.ResolveScalar();
        if (resolved.Kind == YamlScalarKind.Null)
        {
            return ConfigProperty<bool>.Null;
        }

        if (resolved.Kind != YamlScalarKind.Boolean ||
            !Yaml12ScalarResolver.TryGetBoolean(resolved.Value, out var boolValue))
        {
            throw CreateTypeMismatch(key, "boolean or null", resolved.Kind);
        }

        return ConfigProperty<bool>.Create(boolValue);
    }

    /// <summary>
    /// Reads a string-valued property.
    /// </summary>
    /// <param name="node">The mapping to read from.</param>
    /// <param name="key">The key to read.</param>
    /// <returns>The property value.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when the value does not resolve to a string or null.
    /// </exception>
    internal static ConfigProperty<string> ReadString(this YamlMappingNode node, string key)
    {
        var valueNode = node.GetValueNode(key);
        if (valueNode is null)
        {
            return ConfigProperty<string>.Absent;
        }

        var resolved = RequireScalar(valueNode, key).ResolveScalar();
        if (resolved.Kind == YamlScalarKind.Null)
        {
            return ConfigProperty<string>.Null;
        }

        if (resolved.Kind != YamlScalarKind.String)
        {
            throw CreateTypeMismatch(key, "string or null", resolved.Kind);
        }

        return ConfigProperty<string>.Create(resolved.Value);
    }

    internal static ConfigProperty<T> ReadMapping<T>(this YamlMappingNode node, string key, Func<YamlMappingNode, T> factory)
    {
        var valueNode = node.GetValueNode(key);

        if (valueNode is null)
        {
            return ConfigProperty<T>.Absent;
        }

        if (valueNode is YamlMappingNode mapping)
        {
            mapping.EnsureCoreCollectionTag(key);
            mapping.EnsureUniqueStringKeys(key);
            return ConfigProperty<T>.Create(factory(mapping));
        }

        throw new DeclarativeConfigurationException(
            $"Field '{key}' must be a non-null YAML mapping but resolved to {valueNode.NodeType}.");
    }

    private static YamlScalarNode RequireScalar(YamlNode valueNode, string key) =>
        valueNode as YamlScalarNode ?? throw new DeclarativeConfigurationException(
            $"Field '{key}' must be a YAML scalar but resolved to {valueNode.NodeType}.");

    private static DeclarativeConfigurationException CreateTypeMismatch(
        string key,
        string expected,
        YamlScalarKind actual) =>
        new($"Field '{key}' must resolve to {expected} but resolved to YAML {GetYamlKindName(actual)}.");

    private static string GetYamlKindName(YamlScalarKind kind) =>
        kind switch
        {
            YamlScalarKind.Boolean => "boolean",
            YamlScalarKind.Integer => "integer",
            YamlScalarKind.Float => "float",
            YamlScalarKind.Null => "null",
            _ => "string",
        };
}
