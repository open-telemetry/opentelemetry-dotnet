// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Reads typed values from a <see cref="YamlMappingNode"/> into <see cref="ModelProperty{T}"/> results.
/// </summary>
/// <param name="context">The context for the parse this reader belongs to.</param>
internal sealed class YamlPropertyReader(YamlParseContext context)
{
    /// <summary>
    /// Reads a nested mapping property, invoking <paramref name="factory"/> to construct the typed value.
    /// </summary>
    /// <typeparam name="T">The typed model type produced by <paramref name="factory"/>.</typeparam>
    /// <param name="mappingNode">The mapping to read from.</param>
    /// <param name="key">The key to read.</param>
    /// <param name="factory">A delegate that converts the child <see cref="YamlMappingNode"/> into a <typeparamref name="T"/>.</param>
    /// <returns>The property value.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when the value is present but is not a non-null YAML mapping node.
    /// </exception>
    internal ModelProperty<T> ReadMapping<T>(YamlMappingNode mappingNode, string key, Func<YamlMappingNode, T> factory)
    {
        if (!mappingNode.TryGetValueNode(key, out var valueNode))
        {
            return ModelProperty<T>.Absent;
        }

        if (valueNode is YamlMappingNode mapping)
        {
            mapping.EnsureCoreCollectionTag(key);
            _ = context.ResolveMappingKeys(mapping, key);
            return ModelProperty<T>.Create(factory(mapping));
        }

        throw new DeclarativeConfigurationException(
            $"Field '{key}' must be a non-null YAML mapping but resolved to {valueNode.NodeType}.");
    }

    /// <summary>
    /// Reads a boolean-valued property.
    /// </summary>
    /// <param name="mappingNode">The mapping to read from.</param>
    /// <param name="key">The key to read.</param>
    /// <returns>The property value.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when the value does not resolve to a boolean or null.
    /// </exception>
    internal ModelProperty<bool> ReadBoolean(YamlMappingNode mappingNode, string key)
    {
        if (!mappingNode.TryGetValueNode(key, out var valueNode))
        {
            return ModelProperty<bool>.Absent;
        }

        var scalar = RequireScalar(valueNode, key);
        var resolved = context.ResolveScalar(scalar);
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
    /// <param name="mappingNode">The mapping to read from.</param>
    /// <param name="key">The key to read.</param>
    /// <returns>The property value.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when the value does not resolve to a string or null.
    /// </exception>
    internal ModelProperty<string> ReadString(YamlMappingNode mappingNode, string key)
    {
        if (!mappingNode.TryGetValueNode(key, out var valueNode))
        {
            return ModelProperty<string>.Absent;
        }

        var resolved = context.ResolveScalar(RequireScalar(valueNode, key));
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

    private static YamlScalarNode RequireScalar(YamlNode valueNode, string key) =>
        valueNode as YamlScalarNode ?? throw new DeclarativeConfigurationException(
            $"Field '{key}' must be a YAML scalar but resolved to {valueNode.NodeType}.");

    private static DeclarativeConfigurationException CreateTypeMismatch(
        string key,
        string expected,
        YamlScalarKind actual) =>
        new($"Field '{key}' must resolve to {expected} but resolved to YAML {actual.GetYamlKindName()}.");
}
