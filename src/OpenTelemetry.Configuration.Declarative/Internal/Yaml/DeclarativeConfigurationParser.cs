// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Walks a declarative-configuration YAML AST and parses it into the typed <see cref="DeclarativeConfiguration"/> model.
/// </summary>
/// <remarks>
/// This is the only place that depends on the YamlDotNet representation model and that applies the spec's
/// absent / present-null / present distinction (alongside environment-variable substitution performed by
/// <see cref="YamlNodeReader"/>).
/// </remarks>
internal static class DeclarativeConfigurationParser
{
    private static readonly HashSet<string> KnownResourceKeys =
    [
        with(StringComparer.Ordinal),
        YamlKeys.Attributes,
        YamlKeys.AttributesList,
    ];

    private static readonly HashSet<string> KnownAttributeKeys =
    [
        with(StringComparer.Ordinal),
        YamlKeys.Name,
        YamlKeys.Value,
        YamlKeys.Type,
    ];

    /// <summary>
    /// Builds the typed model from the (already validated) document root.
    /// </summary>
    /// <param name="root">The document root mapping <see cref="YamlMappingNode"/>.</param>
    /// <param name="fileFormat">The validated <c>file_format</c> value.</param>
    /// <returns>The typed <see cref="DeclarativeConfiguration"/>.</returns>
    internal static DeclarativeConfiguration Parse(YamlMappingNode root, string fileFormat) =>
        new(fileFormat)
        {
            Disabled = ReadDisabled(root),
            Resource = ReadResource(root),
        };

    private static ConfigProperty<bool> ReadDisabled(YamlMappingNode node) =>
        node.ReadBoolean(YamlKeys.Disabled);

    private static ConfigProperty<ResourceConfiguration> ReadResource(YamlMappingNode node) =>
        node.ReadMapping(YamlKeys.Resource, ReadResourceConfiguration);

    private static ResourceConfiguration ReadResourceConfiguration(YamlMappingNode node)
    {
        // Reported before the known fields are read so that a misspelling is still surfaced when a
        // sibling field goes on to fail validation - the misspelling is often the actual cause.
        node.EnsureNoUnrecognizedProperties(YamlKeys.Resource, KnownResourceKeys);

        return new()
        {
            AttributesList = ReadAttributesList(node),
            Attributes = ReadAttributes(node),
        };
    }

    private static ConfigProperty<string> ReadAttributesList(YamlMappingNode node) =>
        node.ReadString(YamlKeys.AttributesList);

    private static ConfigProperty<IReadOnlyList<ResourceAttributeEntry>> ReadAttributes(YamlMappingNode node)
    {
        var valueNode = node.GetValueNode(YamlKeys.Attributes);
        if (valueNode is null)
        {
            return ConfigProperty<IReadOnlyList<ResourceAttributeEntry>>.Absent;
        }

        if (valueNode is not YamlSequenceNode sequence)
        {
            throw new DeclarativeConfigurationException(
                $"Field '{YamlKeys.Attributes}' must be a non-null YAML sequence but resolved to {valueNode.NodeType}.");
        }

        sequence.EnsureCoreCollectionTag($"{YamlKeys.Resource}.{YamlKeys.Attributes}");

        if (sequence.Children.Count == 0)
        {
            throw new DeclarativeConfigurationException("Field 'attributes' must contain at least one item.");
        }

        var entries = new List<ResourceAttributeEntry>();
        for (var i = 0; i < sequence.Children.Count; i++)
        {
            var item = sequence.Children[i];
            if (item is not YamlMappingNode attributeNode)
            {
                throw CreateInvalidResourceAttributeException(
                    $"resource.attributes[{i}] must be a YAML mapping but found {item.NodeType}.");
            }

            var path = $"{YamlKeys.Resource}.{YamlKeys.Attributes}[{i}]";
            attributeNode.EnsureCoreCollectionTag(path);
            attributeNode.EnsureUniqueStringKeys(path);
            attributeNode.EnsureNoUnrecognizedProperties(path, KnownAttributeKeys);

            var nameProperty = attributeNode.ReadString(YamlKeys.Name);
            if (!nameProperty.TryGetValue(out var name))
            {
                var reason = nameProperty.IsNull
                    ? $"has a null '{YamlKeys.Name}' field"
                    : $"is missing the required '{YamlKeys.Name}' field";
                throw CreateInvalidResourceAttributeException($"resource.attributes[{i}] {reason}.");
            }

            var type = ReadAttributeType(attributeNode, name);
            entries.Add(ReadAttributeValue(attributeNode, name, type));
        }

        return ConfigProperty<IReadOnlyList<ResourceAttributeEntry>>.Create(entries);
    }

    private static ResourceAttributeType ReadAttributeType(YamlMappingNode node, string entryName)
    {
        var property = node.ReadString(YamlKeys.Type);

        // Only an absent type uses the schema default. AttributeType's enum excludes null, so a
        // present YAML null does not select the default string type.
        if (property.IsAbsent)
        {
            return ResourceAttributeType.String;
        }

        if (property.IsNull)
        {
            throw CreateInvalidResourceAttributeException(
                $"A resource.attributes entry for '{entryName}' has a null 'type' field.");
        }

        var value = property.Value;
        return value switch
        {
            "string" => ResourceAttributeType.String,
            "bool" => ResourceAttributeType.Boolean,
            "int" => ResourceAttributeType.Integer,
            "double" => ResourceAttributeType.Double,
            "string_array" => ResourceAttributeType.StringArray,
            "bool_array" => ResourceAttributeType.BooleanArray,
            "int_array" => ResourceAttributeType.IntegerArray,
            "double_array" => ResourceAttributeType.DoubleArray,
            _ => throw CreateInvalidResourceAttributeException(
                $"A resource.attributes entry for '{entryName}' has an unrecognised 'type' value '{value}'."),
        };
    }

    private static ResourceAttributeEntry ReadAttributeValue(
        YamlMappingNode attributeNode,
        string entryName,
        ResourceAttributeType type)
    {
        var valueNode = attributeNode.GetValueNode(YamlKeys.Value)
            ?? throw CreateInvalidResourceAttributeException(
                $"A resource.attributes entry for '{entryName}' is missing the required 'value' field.");

        if (valueNode is YamlScalarNode scalar)
        {
            var resolved = scalar.ResolveScalar();
            if (resolved.Kind == YamlScalarKind.Null)
            {
                // Present-null: schema nullBehavior is "the entry is ignored" (handled by the converter).
                return new(
                    Name: entryName,
                    ScalarValue: null,
                    SequenceValues: null,
                    ValueNodeKind: AttributeValueNodeKind.NullScalar,
                    ScalarKind: null,
                    Type: type);
            }

            if (!ScalarMatchesAttributeType(type, resolved.Kind))
            {
                throw CreateInvalidResourceAttributeException(
                    $"A resource.attributes entry for '{entryName}' has a YAML {GetYamlKindName(resolved.Kind)} value but its declared type is '{type.GetSchemaName()}'.");
            }

            return new(
                Name: entryName,
                ScalarValue: resolved.Value,
                SequenceValues: null,
                ValueNodeKind: AttributeValueNodeKind.Scalar,
                ScalarKind: resolved.Kind,
                Type: type);
        }

        if (valueNode is YamlSequenceNode sequenceNode)
        {
            sequenceNode.EnsureCoreCollectionTag(
                $"{YamlKeys.Resource}.{YamlKeys.Attributes}.{YamlKeys.Value}");

            return new(
                Name: entryName,
                ScalarValue: null,
                SequenceValues: ReadAttributeSequence(type, sequenceNode, entryName),
                ValueNodeKind: AttributeValueNodeKind.Sequence,
                ScalarKind: null,
                Type: type);
        }

        // YamlMappingNode or any other node type - not permitted by the schema.
        throw CreateInvalidResourceAttributeException(
            $"A resource.attributes entry for '{entryName}' has a YAML mapping as its 'value', which is not permitted by the schema.");
    }

    private static bool ScalarMatchesAttributeType(ResourceAttributeType type, YamlScalarKind kind) => type switch
    {
        ResourceAttributeType.Boolean => kind == YamlScalarKind.Boolean,
        ResourceAttributeType.Double => kind is YamlScalarKind.Integer or YamlScalarKind.Float,
        ResourceAttributeType.Integer => kind == YamlScalarKind.Integer,
        ResourceAttributeType.String => kind == YamlScalarKind.String,
        ResourceAttributeType.BooleanArray or
        ResourceAttributeType.DoubleArray or
        ResourceAttributeType.IntegerArray or
        ResourceAttributeType.StringArray or
        _ => false,
    };

    private static List<ResolvedYamlScalar> ReadAttributeSequence(
        ResourceAttributeType type,
        YamlSequenceNode sequence,
        string entryName)
    {
        var expectedKind = type switch
        {
            ResourceAttributeType.BooleanArray => YamlScalarKind.Boolean,
            ResourceAttributeType.DoubleArray => YamlScalarKind.Float,
            ResourceAttributeType.IntegerArray => YamlScalarKind.Integer,
            ResourceAttributeType.StringArray => YamlScalarKind.String,
            ResourceAttributeType.Boolean or
            ResourceAttributeType.Double or
            ResourceAttributeType.Integer or
            ResourceAttributeType.String or
            _ => throw CreateInvalidResourceAttributeException(
                $"A resource.attributes entry for '{entryName}' has a sequence value but its declared type is '{type.GetSchemaName()}'."),
        };

        if (sequence.Children.Count == 0)
        {
            throw CreateInvalidResourceAttributeException(
                $"A resource.attributes entry for '{entryName}' has an empty sequence value; the schema requires at least one item.");
        }

        var values = new List<ResolvedYamlScalar>();
        foreach (var item in sequence.Children)
        {
            if (item is not YamlScalarNode scalar)
            {
                throw CreateInvalidResourceAttributeException(
                    $"A resource.attributes entry for '{entryName}' has a sequence containing a non-scalar item; the configuration does not conform to the schema.");
            }

            var resolved = scalar.ResolveScalar();
            var matches = resolved.Kind == expectedKind ||
                (type == ResourceAttributeType.DoubleArray && resolved.Kind == YamlScalarKind.Integer);
            if (!matches)
            {
                throw CreateInvalidResourceAttributeException(
                    $"A resource.attributes entry for '{entryName}' has a sequence with a {GetYamlKindName(resolved.Kind)} item but declared type is '{type.GetSchemaName()}'.");
            }

            values.Add(resolved);
        }

        return values;
    }

    private static DeclarativeConfigurationException CreateInvalidResourceAttributeException(string message)
    {
        OpenTelemetryDeclarativeConfigurationEventSource.Log.InvalidResourceAttribute(message);
        return new DeclarativeConfigurationException(message);
    }

    private static string GetYamlKindName(YamlScalarKind kind) => kind switch
    {
        YamlScalarKind.Boolean => "boolean",
        YamlScalarKind.Float => "float",
        YamlScalarKind.Integer => "integer",
        YamlScalarKind.Null => "null",
        YamlScalarKind.String or _ => "string",
    };
}
