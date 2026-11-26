// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Enforces YAML parsing limits and bounds resolved collection nesting and node count.
/// </summary>
internal static class YamlLimitsValidator
{
    /// <summary>
    /// The maximum mapping or sequence depth, with the document root at depth zero.
    /// </summary>
    internal const int MaximumDepth = 128;

    /// <summary>
    /// The maximum number of nodes in the resolved document, where each alias counts every node it refers to.
    /// </summary>
    /// <remarks>
    /// Anchored nodes are converted once, but a consumer can still build one object for each place an
    /// alias refers to them (for example, one sampler for each child of a <c>parent_based</c> sampler).
    /// Without this limit, a few kilobytes of aliases can describe billions of objects. The largest
    /// published example documents have fewer than 200 nodes, so the limit leaves ample headroom.
    /// </remarks>
    internal const int MaximumNodeCount = 5_000;

    /// <summary>
    /// The maximum length of an expanded node tag, in characters.
    /// </summary>
    /// <remarks>
    /// YamlDotNet expands a <c>%TAG</c> prefix for each tagged node. Checking the first overlong tag
    /// prevents a long prefix from being copied throughout the document before semantic validation.
    /// YAML 1.2 core tags have fewer than 32 characters, so the limit leaves ample headroom.
    /// </remarks>
    internal const int MaximumTagLength = 1024;

    private static readonly Subtree InProgress = new(-1, 0);

    /// <summary>
    /// Rejects excessive nesting or size in the resolved document, including paths through aliases.
    /// </summary>
    /// <param name="root">The document root.</param>
    /// <exception cref="DeclarativeConfigurationException">
    /// The document exceeds the depth or node count limit, or contains an alias cycle.
    /// </exception>
    internal static void ThrowIfExceeded(YamlNode root)
    {
        var subtrees = new Dictionary<YamlNode, Subtree>(YamlNodeReferenceEqualityComparer.Instance);
        _ = Visit(root, YamlPath.Root, 0, subtrees);
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
                FormattableString.Invariant(
                    $"YAML collection nesting exceeds the maximum depth of {MaximumDepth} (line {position.Line}, column {position.Column}). The document root has depth 0."));
        }
    }

    /// <summary>
    /// Rejects a node count above the limit.
    /// </summary>
    /// <param name="nodeCount">The node count so far.</param>
    /// <param name="position">The source position of the node that reached the count.</param>
    /// <param name="aliasesExpanded">
    /// <see langword="true"/> when each alias counts every node it refers to, which the message then
    /// explains; <see langword="false"/> when each alias counts as one node.
    /// </param>
    /// <exception cref="DeclarativeConfigurationException">The node count exceeds the limit.</exception>
    internal static void EnsureNodeCount(int nodeCount, Mark position, bool aliasesExpanded)
    {
        if (nodeCount > MaximumNodeCount)
        {
            var aliasNote = aliasesExpanded ? " Each alias counts every node it refers to." : string.Empty;
            throw new DeclarativeConfigurationException(
                FormattableString.Invariant(
                    $"YAML document exceeds the maximum of {MaximumNodeCount} nodes (line {position.Line}, column {position.Column}).{aliasNote}"));
        }
    }

    /// <summary>
    /// Rejects an expanded node tag whose length exceeds the limit.
    /// </summary>
    /// <param name="tag">The expanded tag, which may be empty or non-specific.</param>
    /// <param name="position">The tagged node's source position.</param>
    /// <exception cref="DeclarativeConfigurationException">The expanded tag exceeds the length limit.</exception>
    internal static void EnsureTagLength(TagName tag, Mark position)
    {
        if (!tag.IsEmpty && tag.Value.Length > MaximumTagLength)
        {
            throw new DeclarativeConfigurationException(
                FormattableString.Invariant(
                    $"YAML tag exceeds the maximum length of {MaximumTagLength} characters (line {position.Line}, column {position.Column})."));
        }
    }

    private static Subtree Visit(
        YamlNode node,
        string path,
        int depth,
        Dictionary<YamlNode, Subtree> subtrees)
    {
        if (!IsCollection(node))
        {
            return new(0, 1);
        }

        // Guard before descending so even an excessive alias chain cannot exhaust the call stack.
        EnsureDepth(depth, node.Start);

        if (subtrees.TryGetValue(node, out var subtree))
        {
            if (subtree == InProgress)
            {
                throw new DeclarativeConfigurationException(
                    $"YAML alias at '{path}' refers to a node that contains it. A declarative " +
                    "configuration document cannot contain a cycle.");
            }

            EnsureDepth(depth + subtree.Height, node.Start);
            return subtree;
        }

        // In-progress nodes are ancestors; completed entries hold their non-negative height and node count.
        subtrees.Add(node, InProgress);

        var height = 0;
        var nodeCount = 1 + CountScalarChildren(node);
        EnsureNodeCount(nodeCount, node.Start, aliasesExpanded: true);

        foreach (var (child, childPath) in CollectionChildren(node, path))
        {
            var childSubtree = Visit(child, childPath, depth + 1, subtrees);
            height = Math.Max(height, childSubtree.Height + 1);

            // Both values are within the limit, so checking after each child cannot overflow.
            nodeCount += childSubtree.NodeCount;
            EnsureNodeCount(nodeCount, node.Start, aliasesExpanded: true);
        }

        EnsureDepth(depth + height, node.Start);
        subtree = new(height, nodeCount);
        subtrees[node] = subtree;
        return subtree;
    }

    private static bool IsCollection(YamlNode node) => node is YamlMappingNode or YamlSequenceNode;

    private static int CountScalarChildren(YamlNode node)
    {
        var count = 0;
        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (var entry in mapping.Children)
                {
                    count += (IsCollection(entry.Key) ? 0 : 1) + (IsCollection(entry.Value) ? 0 : 1);
                }

                break;

            case YamlSequenceNode sequence:
                foreach (var child in sequence.Children)
                {
                    count += IsCollection(child) ? 0 : 1;
                }

                break;
        }

        return count;
    }

    private static IEnumerable<(YamlNode Node, string Path)> CollectionChildren(YamlNode node, string path)
    {
        switch (node)
        {
            case YamlMappingNode mapping:
                foreach (var entry in mapping.Children)
                {
                    if (IsCollection(entry.Key))
                    {
                        yield return (entry.Key, YamlPath.Child(path, "<key>"));
                    }

                    if (IsCollection(entry.Value))
                    {
                        var key = (entry.Key as YamlScalarNode)?.Value;
                        yield return (entry.Value, key is null ? path : YamlPath.Child(path, key));
                    }
                }

                break;

            case YamlSequenceNode sequence:
                for (var i = 0; i < sequence.Children.Count; i++)
                {
                    if (IsCollection(sequence.Children[i]))
                    {
                        yield return (sequence.Children[i], YamlPath.Index(path, i));
                    }
                }

                break;
        }
    }

    /// <summary>
    /// The height and node count of a resolved collection.
    /// </summary>
    /// <param name="Height">The collection nesting below the node.</param>
    /// <param name="NodeCount">The node itself and every node below it, with each alias counting every node it refers to.</param>
    private readonly record struct Subtree(int Height, int NodeCount);
}
