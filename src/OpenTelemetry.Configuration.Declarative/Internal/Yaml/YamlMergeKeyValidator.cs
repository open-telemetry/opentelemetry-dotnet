// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Rejects YAML 1.1 merge keys throughout a loaded document.
/// </summary>
internal static class YamlMergeKeyValidator
{
    private const string MergeKey = "<<";
    private const string MergeTag = "tag:yaml.org,2002:merge";

    /// <summary>
    /// Throws when <paramref name="root"/> contains a YAML 1.1 merge key.
    /// </summary>
    /// <param name="root">The document root.</param>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when the document contains a plain, untagged <c>&lt;&lt;</c> key or an explicitly
    /// merge-tagged key.
    /// </exception>
    internal static void ThrowIfPresent(YamlNode root)
    {
        var visited = new HashSet<YamlNode>(YamlNodeReferenceEqualityComparer.Instance);
        Visit(root, YamlPath.Root, visited);
    }

    private static bool IsMergeKey(YamlNode key) =>
        key is YamlScalarNode scalar &&
        ((!scalar.Tag.IsEmpty &&
                !scalar.Tag.IsNonSpecific &&
                string.Equals(scalar.Tag.Value, MergeTag, StringComparison.Ordinal)) ||
            (string.Equals(scalar.Value, MergeKey, StringComparison.Ordinal) &&
                YamlScalarResolver.IsPlain(scalar) &&
                scalar.Tag.IsEmpty));

    private static void Visit(YamlNode node, string path, HashSet<YamlNode> visited)
    {
        if (!visited.Add(node))
        {
            return;
        }

        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (var entry in mapping.Children)
                {
                    if (IsMergeKey(entry.Key))
                    {
                        var mergePath = YamlPath.Child(path, MergeKey);
                        throw new DeclarativeConfigurationException(
                            $"YAML 1.1 merge key '{MergeKey}' at '{mergePath}' " +
                            $"(line {entry.Key.Start.Line}, column {entry.Key.Start.Column}) is not supported. " +
                            "Declarative configuration files should use the YAML 1.2 core schema.");
                    }

                    var key = (entry.Key as YamlScalarNode)?.Value;
                    Visit(entry.Value, key is null ? path : YamlPath.Child(path, key), visited);
                }

                break;

            case YamlSequenceNode sequence:
                for (var i = 0; i < sequence.Children.Count; i++)
                {
                    Visit(sequence.Children[i], YamlPath.Index(path, i), visited);
                }

                break;
        }
    }
}
