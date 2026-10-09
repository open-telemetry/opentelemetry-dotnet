// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Collections.Frozen;
#else
using System.Collections.ObjectModel;
#endif
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Reads the configuration schema into a <see cref="SchemaNode"/> graph.
/// </summary>
/// <remarks>
/// The reader accepts only the keywords the pinned schema uses. A keyword that could change which
/// properties are allowed, such as <c>patternProperties</c> or <c>allOf</c>, is rejected rather than
/// ignored. Alternatives in <c>oneOf</c> must have explicit non-object types, with array items
/// restricted in the same way, and must not define property constraints. A <c>oneOf</c> cannot
/// have sibling type, item, or property constraints.
/// </remarks>
internal static class ConfigurationSchemaReader
{
    private const string DefinitionReferencePrefix = "#/$defs/";

    /// <summary>
    /// Reads a schema from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">A stream containing the schema as JSON.</param>
    /// <returns>The root node of the schema.</returns>
    /// <exception cref="InvalidOperationException">
    /// The schema is malformed, or uses a keyword or reference form the reader does not support.
    /// </exception>
    internal static SchemaNode Read(Stream stream)
    {
        var yaml = new YamlStream();
        try
        {
            using var reader = new StreamReader(stream);
            yaml.Load(reader);
        }
        catch (YamlException ex)
        {
            throw new InvalidOperationException($"The configuration schema is not valid JSON: {ex.Message}", ex);
        }

        if (yaml.Documents.Count != 1 || yaml.Documents[0].RootNode is not YamlMappingNode root)
        {
            throw new InvalidOperationException("The configuration schema must be a single JSON object.");
        }

        return new GraphBuilder().Build(root);
    }

    private static InvalidOperationException Unsupported(string message) => new($"Unsupported configuration schema: {message}");

    private static string ReadKeyword(YamlNode key) => AsString(key, "keyword");

    private static YamlMappingNode AsSchema(YamlNode node, string keyword) => node as YamlMappingNode
        ?? throw Unsupported($"'{keyword}' must contain a schema object.");

    private static string AsString(YamlNode node, string keyword) =>
        node is YamlScalarNode { Style: ScalarStyle.DoubleQuoted, Tag.IsEmpty: true, Value: { } value }
            ? value
            : throw Unsupported($"'{keyword}' must contain a double-quoted JSON string.");

    private static SchemaValueTypes ReadValueTypes(YamlNode node)
    {
        if (node is not YamlSequenceNode sequence)
        {
            return ParseValueType(AsString(node, "type"));
        }

        if (sequence.Children.Count == 0)
        {
            throw Unsupported("'type' must contain at least one type.");
        }

        var types = SchemaValueTypes.None;

        foreach (var item in sequence.Children)
        {
            var type = ParseValueType(AsString(item, "type"));
            if ((types & type) != 0)
            {
                throw Unsupported("'type' must not contain duplicate types.");
            }

            types |= type;
        }

        return types;
    }

    private static SchemaValueTypes ParseValueType(string name) => name switch
    {
        "array" => SchemaValueTypes.Array,
        "boolean" => SchemaValueTypes.Boolean,
        "integer" => SchemaValueTypes.Integer,
        "null" => SchemaValueTypes.Null,
        "number" => SchemaValueTypes.Number,
        "object" => SchemaValueTypes.Object,
        "string" => SchemaValueTypes.String,
        _ => throw Unsupported($"type '{name}' is not recognized."),
    };

    private sealed class GraphBuilder
    {
        private readonly Dictionary<string, SchemaNode> definitions = new(StringComparer.Ordinal);

        private readonly List<SchemaNode> alternatives = [];

        internal SchemaNode Build(YamlMappingNode root)
        {
            if (root.Children.TryGetValue(new YamlScalarNode("$defs"), out var definitions))
            {
                var schemas = AsSchema(definitions, "$defs");
                foreach (var definition in schemas.Children)
                {
                    this.definitions.Add(ReadKeyword(definition.Key), new SchemaNode());
                }

                foreach (var definition in schemas.Children)
                {
                    this.Populate(
                        this.definitions[ReadKeyword(definition.Key)],
                        AsSchema(definition.Value, "$defs"),
                        isRoot: false);
                }
            }

            var rootNode = new SchemaNode();
            this.Populate(rootNode, root, isRoot: true);

            // References can point to definitions that were not populated when an alternative was read.
            this.ValidateAlternatives();

            return rootNode;
        }

        private SchemaNode Resolve(YamlNode node, string keyword)
        {
            var schema = AsSchema(node, keyword);

            if (!schema.Children.TryGetValue(new YamlScalarNode("$ref"), out var reference))
            {
                var inline = new SchemaNode();
                this.Populate(inline, schema, isRoot: false);
                return inline;
            }

            foreach (var key in schema.Children.Keys)
            {
                if (ReadKeyword(key) is not ("$ref" or "deprecated" or "description"))
                {
                    throw Unsupported($"'$ref' has the sibling keyword '{ReadKeyword(key)}'.");
                }
            }

            var target = AsString(reference, "$ref");
            if (!target.StartsWith(DefinitionReferencePrefix, StringComparison.Ordinal)
                || !this.definitions.TryGetValue(target.Substring(DefinitionReferencePrefix.Length), out var definition))
            {
                throw Unsupported($"reference '{target}' does not name a local definition.");
            }

            return definition;
        }

        private void Populate(SchemaNode node, YamlMappingNode schema, bool isRoot)
        {
            var properties = new Dictionary<string, SchemaNode>(StringComparer.Ordinal);

            foreach (var entry in schema.Children)
            {
                var keyword = ReadKeyword(entry.Key);
                switch (keyword)
                {
                    case "$defs" or "$schema" when isRoot:
                        break;

                    case "additionalProperties":
                        this.ReadAdditionalProperties(node, entry.Value);
                        break;

                    case "items":
                        node.Items = this.Resolve(entry.Value, keyword);
                        break;

                    case "oneOf":
                        node.HasOneOf = true;
                        this.ReadAlternatives(entry.Value);
                        break;

                    case "properties":
                        foreach (var property in AsSchema(entry.Value, keyword).Children)
                        {
                            properties.Add(ReadKeyword(property.Key), this.Resolve(property.Value, keyword));
                        }

                        break;

                    case "type":
                        node.ValueTypes = ReadValueTypes(entry.Value);
                        break;

                    case "deprecated" or "description" or "enum" or "exclusiveMinimum"
                        or "maxProperties" or "maximum" or "minItems" or "minProperties"
                        or "minimum" or "required" or "title":
                        break;

                    default:
                        throw Unsupported($"keyword '{keyword}' is not supported.");
                }
            }

#if NET
            node.Properties = properties.ToFrozenDictionary(StringComparer.Ordinal);
#else
            node.Properties = new ReadOnlyDictionary<string, SchemaNode>(properties);
#endif

            if (node.HasOneOf)
            {
                foreach (var key in schema.Children.Keys)
                {
                    if (ReadKeyword(key) is "type" or "items" or "properties" or "additionalProperties")
                    {
                        throw Unsupported($"'oneOf' has the sibling keyword '{ReadKeyword(key)}'.");
                    }
                }
            }
        }

        private void ReadAlternatives(YamlNode node)
        {
            if (node is not YamlSequenceNode sequence || sequence.Children.Count == 0)
            {
                throw Unsupported("'oneOf' must contain a non-empty array.");
            }

            foreach (var alternative in sequence.Children)
            {
                this.alternatives.Add(this.Resolve(alternative, "oneOf"));
            }
        }

        private void ValidateAlternatives()
        {
            var visited = new HashSet<SchemaNode>();

            foreach (var alternative in this.alternatives)
            {
                var node = alternative;
                while (visited.Add(node))
                {
                    if (node.ValueTypes == SchemaValueTypes.None
                        || (node.ValueTypes & SchemaValueTypes.Object) != 0
                        || node.Properties.Count != 0
                        || node.AdditionalProperties != AdditionalPropertiesKind.Allowed
                        || node.HasOneOf)
                    {
                        throw Unsupported("'oneOf' alternatives must have explicit non-object types and no property constraints.");
                    }

                    if ((node.ValueTypes & SchemaValueTypes.Array) != 0 && node.Items is null)
                    {
                        throw Unsupported("'oneOf' array alternatives must define non-object items.");
                    }

                    if (node.Items is not { } items)
                    {
                        break;
                    }

                    node = items;
                }
            }
        }

        private void ReadAdditionalProperties(SchemaNode node, YamlNode value)
        {
            if (value is YamlMappingNode)
            {
                node.AdditionalProperties = AdditionalPropertiesKind.Schema;
                node.AdditionalPropertiesSchema = this.Resolve(value, "additionalProperties");
                return;
            }

            node.AdditionalProperties = value switch
            {
                YamlScalarNode { Style: ScalarStyle.Plain, Tag.IsEmpty: true, Value: "false" } =>
                    AdditionalPropertiesKind.Forbidden,
                YamlScalarNode { Style: ScalarStyle.Plain, Tag.IsEmpty: true, Value: "true" } =>
                    AdditionalPropertiesKind.Allowed,
                _ => throw Unsupported("'additionalProperties' must be a boolean or a schema object."),
            };
        }
    }
}
