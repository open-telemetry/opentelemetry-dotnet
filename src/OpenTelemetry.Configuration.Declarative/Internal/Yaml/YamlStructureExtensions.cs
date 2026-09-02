// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Stateless helpers for navigating and validating the structure of a YamlDotNet document.
/// </summary>
internal static class YamlStructureExtensions
{
    private const string MappingTag = "tag:yaml.org,2002:map";
    private const string SequenceTag = "tag:yaml.org,2002:seq";

    /// <summary>
    /// Returns the raw value node for <paramref name="key"/>, or <see langword="null"/> if the key is absent.
    /// The node is returned regardless of its type, so callers can distinguish scalars, sequences and mappings.
    /// </summary>
    /// <param name="mappingNode">The mapping to search.</param>
    /// <param name="key">The key to find.</param>
    /// <returns>The value node, or <see langword="null"/> if the key is absent.</returns>
    internal static YamlNode? GetValueNode(this YamlMappingNode mappingNode, string key)
    {
        foreach (var entry in mappingNode.Children)
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
    /// Attempts to get the raw value node for <paramref name="key"/>, returning a boolean indicating success.
    /// </summary>
    /// <param name="mappingNode">The mapping to search.</param>
    /// <param name="key">The key to find.</param>
    /// <param name="value">The value node if found, or <see langword="null"/> if the key is absent.</param>
    /// <returns><see langword="true"/> if the key was found; otherwise, <see langword="false"/>.</returns>
    internal static bool TryGetValueNode(this YamlMappingNode mappingNode, string key, [NotNullWhen(true)] out YamlNode? value)
    {
        value = mappingNode.GetValueNode(key);
        return value is not null;
    }

    /// <summary>
    /// Ensures every mapping key resolves to a unique YAML string without applying environment substitution.
    /// </summary>
    /// <param name="node">The mapping to validate.</param>
    /// <param name="context">A description of the mapping, used in error messages.</param>
    /// <returns>The resolved key strings paired with their value nodes, in document order.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when a key is not a YAML string scalar, or when two keys resolve to the same string.
    /// </exception>
    internal static IReadOnlyList<KeyValuePair<string, YamlNode>> EnsureUniqueStringKeys(this YamlMappingNode node, string context)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<KeyValuePair<string, YamlNode>>(node.Children.Count);

        foreach (var entry in node.Children)
        {
            if (entry.Key is not YamlScalarNode keyNode)
            {
                throw new DeclarativeConfigurationException(
                    $"Mapping '{context}' must use YAML string scalar keys.");
            }

            // Mapping keys are not candidates for environment substitution. Resolve the authored
            // key directly so equivalent spellings such as `key` and `!!str key` compare equally.
            var resolved = YamlScalarResolver.Resolve(keyNode, keyNode.Value ?? string.Empty);
            if (resolved.Kind != YamlScalarKind.String)
            {
                throw new DeclarativeConfigurationException(
                    $"Mapping '{context}' contains a YAML {resolved.Kind.GetYamlKindName()} key; " +
                    "declarative configuration property names must be strings.");
            }

            if (!keys.Add(resolved.Value))
            {
                throw new DeclarativeConfigurationException(
                    $"Mapping '{context}' contains duplicate key '{resolved.Value}'.");
            }

            entries.Add(new(resolved.Value, entry.Value));
        }

        return entries;
    }

    /// <summary>
    /// Ensures that a collection has either a non-specific tag or the YAML 1.2 core tag that
    /// corresponds to its node kind.
    /// </summary>
    /// <param name="node">The mapping or sequence node to validate.</param>
    /// <param name="context">A description of the node, used in the error message.</param>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when an explicit tag does not match the collection's node kind.
    /// </exception>
    internal static void EnsureCoreCollectionTag(this YamlNode node, string context)
    {
        var expectedTag = node switch
        {
            YamlMappingNode => MappingTag,
            YamlSequenceNode => SequenceTag,
            _ => throw new ArgumentException("The node must be a YAML mapping or sequence.", nameof(node)),
        };

        var tag = node.Tag;
        if (tag.IsEmpty || tag.IsNonSpecific || string.Equals(tag.Value, expectedTag, StringComparison.Ordinal))
        {
            return;
        }

        throw new DeclarativeConfigurationException(
            $"YAML {node.NodeType} '{context}' has explicit tag '{tag}' but must use '{expectedTag}'.");
    }

    /// <summary>
    /// Reports every key in <paramref name="node"/> that is not in <paramref name="known"/>, then throws.
    /// </summary>
    /// <param name="node">The mapping to check.</param>
    /// <param name="path">The dotted path of <paramref name="node"/>, used in the diagnostic messages.</param>
    /// <param name="known">Keys defined by the schema for this mapping.</param>
    internal static void EnsureNoUnrecognizedProperties(
        this YamlMappingNode node,
        string path,
        IReadOnlyCollection<string> known)
    {
        string? firstUnknownProperty = null;

        foreach (var entry in node.Children)
        {
            if (entry.Key is not YamlScalarNode keyNode)
            {
                var nonScalarPath = $"{path}.<non-scalar key>";
                OpenTelemetryDeclarativeConfigurationEventSource.Log.UnknownConfigurationProperty(nonScalarPath);
                firstUnknownProperty ??= nonScalarPath;
                continue;
            }

            var key = keyNode.Value;
            if (key is not null && known.Contains(key))
            {
                continue;
            }

            var display = $"{path}.{(key is null ? "<null>" : key.Length == 0 ? "<empty>" : key)}";
            OpenTelemetryDeclarativeConfigurationEventSource.Log.UnknownConfigurationProperty(display);
            firstUnknownProperty ??= display;
        }

        if (firstUnknownProperty is not null)
        {
            throw new DeclarativeConfigurationException(
                $"Property '{firstUnknownProperty}' is not supported by this declarative configuration implementation.");
        }
    }
}
