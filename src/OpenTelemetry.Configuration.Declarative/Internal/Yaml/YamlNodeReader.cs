// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Low-level helpers for walking a YamlDotNet <see cref="YamlMappingNode"/> by key.
/// </summary>
/// <remarks>
/// <para>
/// Children are iterated and key strings compared directly (Ordinal) to avoid relying on
/// <see cref="YamlScalarNode"/> equality/hashing behaviour across YamlDotNet versions.
/// </para>
/// <para>
/// <b>Resolution order.</b> <see cref="ResolveScalar(YamlScalarNode)"/> is the single entry point
/// through which every scalar in this package is read, and it establishes one deterministic order:
/// YAML parse, then environment variable substitution on the decoded scalar content, then YAML 1.2
/// type resolution (<see cref="Yaml12ScalarResolver"/>). The OTel spec limits substitution to
/// scalar values and requires it to happen before type interpretation. Consequently,
/// <c>${PORT}</c> resolving to <c>4317</c> is an integer on every code path and
/// <c>"${PORT}"</c> is a string on every code path.
/// </para>
/// </remarks>
internal static class YamlNodeReader
{
    private const string MappingTag = "tag:yaml.org,2002:map";
    private const string SequenceTag = "tag:yaml.org,2002:seq";

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
    /// Ensures every mapping key resolves to a unique YAML string without applying environment substitution.
    /// </summary>
    /// <param name="node">The mapping to validate.</param>
    /// <param name="context">A description of the mapping, used in error messages.</param>
    public static void EnsureUniqueStringKeys(this YamlMappingNode node, string context)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in node.Children)
        {
            if (entry.Key is not YamlScalarNode keyNode)
            {
                throw new DeclarativeConfigurationException(
                    $"Mapping '{context}' must use YAML string scalar keys.");
            }

            // Mapping keys are not candidates for environment substitution. Resolve the authored
            // key directly so equivalent spellings such as `key` and `!!str key` compare equally.
            var resolved = Yaml12ScalarResolver.Resolve(keyNode, keyNode.Value ?? string.Empty);
            if (resolved.Kind != YamlScalarKind.String)
            {
                throw new DeclarativeConfigurationException(
                    $"Mapping '{context}' contains a YAML {GetYamlKindName(resolved.Kind)} key; " +
                    "declarative configuration property names must be strings.");
            }

            if (!keys.Add(resolved.Value))
            {
                throw new DeclarativeConfigurationException(
                    $"Mapping '{context}' contains duplicate key '{resolved.Value}'.");
            }
        }
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
    /// Applies environment variable substitution and then YAML 1.2 core-schema resolution.
    /// </summary>
    /// <param name="scalar">The scalar node.</param>
    /// <returns>The resolved scalar.</returns>
    /// <remarks>
    /// <para>
    /// Substitution runs on the scalar's decoded content, which is what
    /// <see cref="YamlScalarNode.Value"/> exposes. YAML 1.2 defines escape spellings as
    /// presentation details, and the OTel substitution rules apply only to scalar values.
    /// Consequently, an escape cannot smuggle a character into a <c>DEFAULT-VALUE</c> that its
    /// ABNF forbids: a double-quoted <c>"${VAR:-a\nb}"</c> carries a real newline and is rejected.
    /// <c>$$</c> is the OTel substitution escape.
    /// </para>
    /// <para>
    /// The scalar's <see cref="YamlScalarNode.Style"/> still matters, but only to
    /// <see cref="Yaml12ScalarResolver"/> for the type decision that follows.
    /// </para>
    /// </remarks>
    public static ResolvedYamlScalar ResolveScalar(this YamlScalarNode scalar) =>
        Yaml12ScalarResolver.Resolve(
            scalar,
            EnvironmentSubstitution.Substitute(scalar.Value ?? string.Empty));

    /// <summary>
    /// Ensures that a collection has either a non-specific tag or the YAML 1.2 core tag that
    /// corresponds to its node kind.
    /// </summary>
    /// <param name="node">The mapping or sequence node to validate.</param>
    /// <param name="context">A description of the node, used in the error message.</param>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when an explicit tag does not match the collection's node kind.
    /// </exception>
    /// <remarks>
    /// YAML tags are not decorative: an explicit <c>!!str</c> on a mapping does not make that
    /// mapping a string. The YAML 1.2 core schema resolves non-specific collection tags to
    /// <c>!!map</c> or <c>!!seq</c> according to the node kind, so a configuration parser must
    /// reject a collection whose explicit tag declares a different kind.
    /// </remarks>
    public static void EnsureCoreCollectionTag(this YamlNode node, string context)
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
    /// Returns the substituted string representation, or <see langword="null"/> for YAML null.
    /// </summary>
    /// <param name="scalar">The scalar node.</param>
    /// <returns>The substituted representation.</returns>
    public static string? GetScalarString(this YamlScalarNode scalar)
    {
        var resolved = scalar.ResolveScalar();
        return resolved.Kind == YamlScalarKind.Null ? null : resolved.Value;
    }

    /// <summary>
    /// Reports every key in <paramref name="node"/> that is not in <paramref name="known"/>, then throws.
    /// </summary>
    /// <param name="node">The mapping to check.</param>
    /// <param name="path">The dotted path of <paramref name="node"/>, used in the diagnostic messages.</param>
    /// <param name="known">Keys defined by the schema for this mapping.</param>
    /// <remarks>
    /// This parser currently supports only the supplied set of properties. An unrecognised key is
    /// most often a misspelling, but may also be a schema-defined property that this initial
    /// implementation does not yet read. Parse must not return a partial result in either case.
    /// </remarks>
    public static void EnsureNoUnrecognizedProperties(
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
