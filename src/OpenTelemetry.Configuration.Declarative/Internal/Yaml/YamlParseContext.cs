// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Holds the YAML-specific state and services for one parse of a declarative configuration document.
/// </summary>
internal sealed class YamlParseContext
{
    private readonly Dictionary<YamlNode, ResolvedYamlScalar> resolved =
        new(YamlNodeReferenceEqualityComparer.Instance);

    private readonly Dictionary<YamlNode, IReadOnlyList<KeyValuePair<string, YamlNode>>> mappingKeys =
        new(YamlNodeReferenceEqualityComparer.Instance);

    private readonly Func<string, string?> resolveVariable;

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlParseContext"/> class.
    /// </summary>
    /// <param name="resolveVariable">
    /// Returns the value of a named environment variable, or <see langword="null"/> if not set.
    /// </param>
    internal YamlParseContext(Func<string, string?> resolveVariable)
    {
        Guard.ThrowIfNull(resolveVariable);

        this.resolveVariable = resolveVariable;
    }

    /// <summary>
    /// Applies environment variable substitution and then YAML 1.2 core-schema resolution to
    /// <paramref name="scalarNode"/>, returning the cached result on any later call for the same node.
    /// </summary>
    /// <param name="scalarNode">The scalar node.</param>
    /// <returns>The resolved scalar.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when the scalar contains an invalid <c>${...}</c> reference, or when an explicit core
    /// tag does not match the scalar's value.
    /// </exception>
    internal ResolvedYamlScalar ResolveScalar(YamlScalarNode scalarNode)
    {
        if (this.resolved.TryGetValue(scalarNode, out var existing))
        {
            return existing;
        }

        var value = YamlScalarResolver.Resolve(
            scalarNode,
            EnvironmentSubstitution.Substitute(scalarNode.Value ?? string.Empty, this.resolveVariable));

        this.resolved.Add(scalarNode, value);
        return value;
    }

    /// <summary>
    /// Validates that <paramref name="mappingNode"/> has unique YAML string keys and returns them paired
    /// with their value nodes, returning the memoized result on any later call for the same node.
    /// </summary>
    /// <param name="mappingNode">The mapping to validate.</param>
    /// <param name="path">The path of <paramref name="mappingNode"/>, used in error messages.</param>
    /// <returns>The resolved keys paired with their value nodes, in document order.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when a key is not a YAML string scalar, or when two keys resolve to the same string.
    /// </exception>
    internal IReadOnlyList<KeyValuePair<string, YamlNode>> ResolveMappingKeys(YamlMappingNode mappingNode, string path)
    {
        if (this.mappingKeys.TryGetValue(mappingNode, out var existing))
        {
            return existing;
        }

        var keys = mappingNode.EnsureUniqueStringKeys(path);

        this.mappingKeys.Add(mappingNode, keys);
        return keys;
    }
}
