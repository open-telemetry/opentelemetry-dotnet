// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Bounds collection nesting before recursive document processing.
/// </summary>
internal static class YamlDepthValidator
{
    /// <summary>
    /// The maximum mapping or sequence depth, with the document root at depth zero.
    /// </summary>
    internal const int MaximumDepth = 128;

    private const int InProgressHeight = -1;

    /// <summary>
    /// Rejects excessive nesting in the resolved document, including paths through aliases.
    /// </summary>
    /// <param name="root">The document root.</param>
    /// <exception cref="DeclarativeConfigurationException">
    /// The document exceeds the depth limit or contains an alias cycle.
    /// </exception>
    internal static void ThrowIfExceeded(YamlNode root)
    {
        var subtreeHeights = new Dictionary<YamlNode, int>(YamlNodeReferenceEqualityComparer.Instance);
        _ = Visit(root, YamlPath.Root, 0, subtreeHeights);
    }

    /// <summary>
    /// Rejects a collection whose depth exceeds the limit.
    /// </summary>
    /// <param name="depth">The collection depth.</param>
    /// <param name="position">The collection's source position.</param>
    /// <exception cref="DeclarativeConfigurationException">The depth exceeds the limit.</exception>
    internal static void EnsureDepth(int depth, Mark position)
    {
        if (depth > MaximumDepth)
        {
            throw new DeclarativeConfigurationException(
                $"YAML collection nesting exceeds the maximum depth of {MaximumDepth.ToString(CultureInfo.InvariantCulture)} " +
                $"(line {position.Line.ToString(CultureInfo.InvariantCulture)}, " +
                $"column {position.Column.ToString(CultureInfo.InvariantCulture)}). The document root has depth 0.");
        }
    }

    private static int Visit(
        YamlNode node,
        string path,
        int depth,
        Dictionary<YamlNode, int> subtreeHeights)
    {
        if (node is not (YamlMappingNode or YamlSequenceNode))
        {
            return 0;
        }

        // Guard before descending so even an excessive alias chain cannot exhaust the call stack.
        EnsureDepth(depth, node.Start);

        if (subtreeHeights.TryGetValue(node, out var subtreeHeight))
        {
            if (subtreeHeight == InProgressHeight)
            {
                throw new DeclarativeConfigurationException(
                    $"YAML alias at '{path}' refers to a node that contains it. A declarative " +
                    "configuration document cannot contain a cycle.");
            }

            EnsureDepth(depth + subtreeHeight, node.Start);
            return subtreeHeight;
        }

        // In-progress nodes are ancestors; completed entries hold their non-negative subtree height.
        subtreeHeights.Add(node, InProgressHeight);
        subtreeHeight = 0;
        foreach (var (child, childPath) in CollectionChildren(node, path))
        {
            subtreeHeight = Math.Max(subtreeHeight, Visit(child, childPath, depth + 1, subtreeHeights) + 1);
        }

        EnsureDepth(depth + subtreeHeight, node.Start);
        subtreeHeights[node] = subtreeHeight;
        return subtreeHeight;
    }

    private static IEnumerable<(YamlNode Node, string Path)> CollectionChildren(YamlNode node, string path)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (var entry in mapping.Children)
                {
                    if (entry.Key is YamlMappingNode or YamlSequenceNode)
                    {
                        yield return (entry.Key, YamlPath.Child(path, "<key>"));
                    }

                    if (entry.Value is YamlMappingNode or YamlSequenceNode)
                    {
                        var key = (entry.Key as YamlScalarNode)?.Value;
                        yield return (entry.Value, key is null ? path : YamlPath.Child(path, key));
                    }
                }

                break;

            case YamlSequenceNode sequence:
                for (var i = 0; i < sequence.Children.Count; i++)
                {
                    if (sequence.Children[i] is YamlMappingNode or YamlSequenceNode)
                    {
                        yield return (sequence.Children[i], YamlPath.Index(path, i));
                    }
                }

                break;
        }
    }
}
