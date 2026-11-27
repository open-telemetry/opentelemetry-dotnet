// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Bounds YAML collection nesting, node count and expanded tag length, rejects collection-valued
/// keys and unresolved alias keys, and preserves alias-key source positions.
/// </summary>
/// <remarks>
/// Each alias counts as one node here, so this count is never above the count
/// <see cref="YamlLimitsValidator.ThrowIfExceeded(YamlNode)"/> applies once aliases are resolved.
/// Checking it while parsing rejects a large document before its node tree is built.
/// </remarks>
/// <param name="innerParser">The parser supplying YAML events, which must not have been advanced.</param>
internal sealed class YamlDocumentTrackingParser(IParser innerParser) : IParser
{
    private readonly Stack<(ConfigValuePosition? MappingPosition, int DirectChildCount)> collectionFrames = [];

    // Maps each anchor defined so far in the current document to whether its node is a scalar.
    private readonly Dictionary<AnchorName, bool> definedAnchors = [];

    private Dictionary<(ConfigValuePosition MappingPosition, int KeyIndex), ConfigValuePosition>? aliasKeyPositions;

    private int nodeCount;

    /// <inheritdoc/>
    public ParsingEvent? Current => innerParser.Current;

    /// <inheritdoc/>
    public bool MoveNext()
    {
        if (!innerParser.MoveNext())
        {
            return false;
        }

        switch (this.Current)
        {
            case DocumentStart:
                this.definedAnchors.Clear();
                this.nodeCount = 0;
                break;

            case MappingStart mapping:
                YamlLimitsValidator.EnsureDepth(this.collectionFrames.Count, mapping.Start);
                this.TrackNode(mapping);
                this.collectionFrames.Push((new(mapping.Start.Line, mapping.Start.Column), 0));
                break;

            case SequenceStart sequence:
                YamlLimitsValidator.EnsureDepth(this.collectionFrames.Count, sequence.Start);
                this.TrackNode(sequence);
                this.collectionFrames.Push((null, 0));
                break;

            case MappingEnd or SequenceEnd:
                this.collectionFrames.Pop();
                break;

            case Scalar scalar:
                this.TrackNode(scalar);
                break;

            case AnchorAlias alias:
                this.TrackNode(alias);
                break;
        }

        return true;
    }

    /// <summary>
    /// Gets the source position of a mapping key at its occurrence.
    /// </summary>
    /// <param name="mapping">The mapping containing the key.</param>
    /// <param name="index">The key's zero-based index in the mapping's document order.</param>
    /// <param name="key">The resolved key node.</param>
    /// <returns>The key's source position.</returns>
    internal ConfigValuePosition GetKeyPosition(YamlMappingNode mapping, int index, YamlNode key) =>
        this.aliasKeyPositions is not null && this.aliasKeyPositions.TryGetValue((new(mapping.Start.Line, mapping.Start.Column), index), out var position)
            ? position
            : new(key.Start.Line, key.Start.Column);

    private void TrackNode(ParsingEvent node)
    {
        YamlLimitsValidator.EnsureNodeCount(++this.nodeCount, node.Start, aliasesExpanded: false);

        if (node is NodeEvent nodeEvent)
        {
            YamlLimitsValidator.EnsureTagLength(nodeEvent.Tag, nodeEvent.Start);

            if (!nodeEvent.Anchor.IsEmpty)
            {
                this.definedAnchors[nodeEvent.Anchor] = node is Scalar;
            }
        }

        if (this.collectionFrames.Count == 0 || this.collectionFrames.Peek().MappingPosition is not { } mappingPosition)
        {
            return;
        }

        var (mapping, directChildCount) = this.collectionFrames.Pop();
        this.collectionFrames.Push((mapping, directChildCount + 1));

        // Count direct children only: even indices are keys, odd indices are values.
        if (directChildCount % 2 != 0)
        {
            return;
        }

        // A key is a scalar when written as one, or when it is an alias to an anchored scalar.
        var keyIsScalar = node is Scalar;
        if (node is AnchorAlias alias && !this.definedAnchors.TryGetValue(alias.Value, out keyIsScalar))
        {
            throw new YamlException(
                alias.Start,
                alias.End,
                $"YAML mapping key alias '*{alias.Value}' must refer to an anchor defined earlier in the same document.");
        }

        // Reject unsupported collection keys before YamlDotNet recursively hashes them.
        if (!keyIsScalar)
        {
            throw new DeclarativeConfigurationException(
                FormattableString.Invariant(
                    $"YAML mapping keys must be string scalars (line {node.Start.Line}, column {node.Start.Column})."));
        }

        if (node is AnchorAlias)
        {
            this.aliasKeyPositions ??= [];

            this.aliasKeyPositions.Add(
                (mappingPosition, directChildCount / 2),
                new(node.Start.Line, node.Start.Column));
        }
    }
}
