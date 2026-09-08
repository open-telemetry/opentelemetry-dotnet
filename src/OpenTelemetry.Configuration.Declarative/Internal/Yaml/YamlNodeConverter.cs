// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Converts a YAML document into a <see cref="ConfigProperties"/> rooted at the document itself.
/// </summary>
internal static class YamlNodeConverter
{
    /// <summary>
    /// Converts <paramref name="root"/> and everything below it.
    /// </summary>
    /// <param name="root">The document root mapping.</param>
    /// <param name="context">The context for this parse.</param>
    /// <returns>A <see cref="ConfigProperties"/> containing every key in the document.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when the document contains a mapping with duplicate or non-string keys, a collection
    /// whose explicit tag contradicts its node kind, a scalar whose value is invalid for its
    /// explicit tag, an invalid <c>${...}</c> substitution reference, or an alias cycle.
    /// </exception>
    internal static ConfigProperties ConvertDocument(
        YamlMappingNode root,
        YamlParseContext context) =>
        new DocumentWalk(context).ConvertRoot(root);

    private sealed class DocumentWalk(YamlParseContext context)
    {
        private Dictionary<YamlNode, ConfigValue>? converted;

        private HashSet<YamlNode>? inProgress;

        internal ConfigProperties ConvertRoot(YamlMappingNode root)
        {
            this.inProgress = new(YamlNodeReferenceEqualityComparer.Instance) { root };
            return this.ConvertMapping(root, YamlPath.Root);
        }

        private static ConfigValuePosition GetPosition(YamlNode node) =>
            new(node.Start.Line, node.Start.Column);

        private ConfigValue ConvertScalar(YamlScalarNode scalar) =>
            YamlScalarConverter.Convert(context.ResolveScalar(scalar)).WithPosition(GetPosition(scalar));

        // Call sites handle scalars separately; this method converts mappings and sequences.
        private ConfigValue ConvertCollection(YamlNode node, string path)
        {
            var shareable = !node.Anchor.IsEmpty;

            if (shareable && this.converted is not null && this.converted.TryGetValue(node, out var existing))
            {
                return existing;
            }

            if (!this.inProgress!.Add(node))
            {
                throw new DeclarativeConfigurationException(
                    $"YAML alias at '{path}' refers to a node that contains it. A declarative " +
                    "configuration document cannot contain a cycle.");
            }

            var value = node switch
            {
                YamlMappingNode mapping => ConfigValue.Mapping(this.ConvertMapping(mapping, path)),
                YamlSequenceNode sequence => this.ConvertSequence(sequence, path),
                _ => throw new DeclarativeConfigurationException(
                    $"YAML node '{path}' has unsupported node type {node.NodeType}."),
            };

            value = value.WithPosition(GetPosition(node));

            this.inProgress.Remove(node);

            if (shareable)
            {
                this.converted ??= new(YamlNodeReferenceEqualityComparer.Instance);
                this.converted.Add(node, value);
            }

            return value;
        }

        private ConfigProperties ConvertMapping(YamlMappingNode mapping, string path)
        {
            mapping.EnsureCoreCollectionTag(path);

            // Keys are added under their resolved YAML string, so `key` and `!!str key` land
            // identically. Keys are never substituted.
            var entries = context.ResolveMappingKeys(mapping, path);

            var builder = new ConfigPropertiesBuilder();
            foreach (var entry in entries)
            {
                var value = entry.Value is YamlScalarNode scalar
                    ? this.ConvertScalar(scalar)
                    : this.ConvertCollection(entry.Value, YamlPath.Child(path, entry.Key));

                builder.Add(entry.Key, value);
            }

            return builder.Build();
        }

        private ConfigValue ConvertSequence(YamlSequenceNode sequence, string path)
        {
            sequence.EnsureCoreCollectionTag(path);

            var items = new ConfigValue[sequence.Children.Count];
            for (var i = 0; i < items.Length; i++)
            {
                var item = sequence.Children[i];
                items[i] = item is YamlScalarNode scalar
                    ? this.ConvertScalar(scalar)
                    : this.ConvertCollection(item, YamlPath.Index(path, i));
            }

            return ConfigValue.Sequence(items);
        }
    }
}
