// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Extension methods that read typed values from a <see cref="YamlMappingNode"/> into <see cref="ModelProperty{T}"/> results.
/// </summary>
/// <remarks>
/// All type resolution here is delegated to <see cref="YamlScalarResolver"/> so that the two
/// readers cannot disagree about what a given piece of text means. Substitution always runs before
/// core-schema type resolution.
/// </remarks>
internal static class YamlPropertyReader
{
    /// <summary>
    /// Reads a boolean-valued property.
    /// </summary>
    /// <param name="node">The mapping to read from.</param>
    /// <param name="key">The key to read.</param>
    /// <returns>The property value.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when the value does not resolve to a boolean or null.
    /// </exception>
    internal static ModelProperty<bool> ReadBoolean(this YamlMappingNode node, string key)
    {
        var valueNode = node.GetValueNode(key);
        if (valueNode is null)
        {
            return ModelProperty<bool>.Absent;
        }

        var scalar = RequireScalar(valueNode, key);
        var resolved = scalar.ResolveScalar();
        if (resolved.Kind == YamlScalarKind.Null)
        {
            return ModelProperty<bool>.Null;
        }

        if (resolved.Kind != YamlScalarKind.Boolean ||
            !YamlScalarResolver.TryGetBoolean(resolved.Value, out var boolValue))
        {
            throw CreateTypeMismatch(key, "boolean or null", resolved.Kind);
        }

        return ModelProperty<bool>.Create(boolValue);
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
    internal static ModelProperty<string> ReadString(this YamlMappingNode node, string key)
    {
        var valueNode = node.GetValueNode(key);
        if (valueNode is null)
        {
            return ModelProperty<string>.Absent;
        }

        var resolved = RequireScalar(valueNode, key).ResolveScalar();
        if (resolved.Kind == YamlScalarKind.Null)
        {
            return ModelProperty<string>.Null;
        }

        if (resolved.Kind != YamlScalarKind.String)
        {
            throw CreateTypeMismatch(key, "string or null", resolved.Kind);
        }

        return ModelProperty<string>.Create(resolved.Value);
    }

    /// <summary>
    /// Reads a nested mapping property, invoking <paramref name="factory"/> to construct the typed value.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="ReadBoolean"/> and <see cref="ReadString"/>, a present null value is not
    /// modelled as <see cref="ModelProperty{T}.Null"/> - it is rejected as a type mismatch. The
    /// configuration schema does not define a null-mapping state, so a null value here is always an error.
    /// </remarks>
    /// <typeparam name="T">The typed model type produced by <paramref name="factory"/>.</typeparam>
    /// <param name="node">The mapping to read from.</param>
    /// <param name="key">The key to read.</param>
    /// <param name="factory">A delegate that converts the child <see cref="YamlMappingNode"/> into a <typeparamref name="T"/>.</param>
    /// <returns>The property value.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when the value is present but is not a non-null YAML mapping node.
    /// </exception>
    internal static ModelProperty<T> ReadMapping<T>(this YamlMappingNode node, string key, Func<YamlMappingNode, T> factory)
    {
        var valueNode = node.GetValueNode(key);

        if (valueNode is null)
        {
            return ModelProperty<T>.Absent;
        }

        if (valueNode is YamlMappingNode mapping)
        {
            mapping.EnsureCoreCollectionTag(key);
            mapping.EnsureUniqueStringKeys(key);
            return ModelProperty<T>.Create(factory(mapping));
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

    private static string GetYamlKindName(YamlScalarKind kind) => kind switch
    {
        YamlScalarKind.Boolean => "boolean",
        YamlScalarKind.Float => "float",
        YamlScalarKind.Integer => "integer",
        YamlScalarKind.Null => "null",
        YamlScalarKind.String or _ => "string",
    };
}
